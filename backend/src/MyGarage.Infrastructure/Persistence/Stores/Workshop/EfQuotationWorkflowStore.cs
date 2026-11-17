using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MyGarage.Application.Interfaces.Stores.Workshop;
using MyGarage.Interfaces;
using MyGarage.Enums;
using MyGarage.Models;

namespace MyGarage.Infrastructure.Persistence.Stores.Workshop;

public sealed partial class EfQuotationWorkflowStore : IQuotationWorkflowStore
{
    private readonly IGarageContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<EfQuotationWorkflowStore> _logger;
    private bool _executingTransaction;

    public EfQuotationWorkflowStore(
        IGarageContext context,
        IConfiguration configuration,
        ILogger<EfQuotationWorkflowStore> logger)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
    }

    public int? GetPortalTokenExpiryDays()
    {
        return _configuration.GetValue<int?>("QuotationPortal:TokenExpiryDays");
    }

    public string GetPortalPublicBaseUrl()
    {
        var configuredBase = (_configuration["QuotationPortal:PublicBaseUrl"] ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(configuredBase))
        {
            var frontendBase = (_configuration["Frontend:BaseUrl"] ?? string.Empty).Trim().TrimEnd('/');
            configuredBase = !string.IsNullOrWhiteSpace(frontendBase)
                ? $"{frontendBase}/quote-access"
                : "/quote-access";
        }

        return configuredBase;
    }

    public async Task SaveChangesInTransactionAsync()
    {
        // SaveChanges provides its own transaction when none exists. Do not clear
        // tracked changes or replay a previously mutated context in a retry delegate.
        await _context.SaveChangesAsync();
    }

    public async Task LockServiceOrderForDecisionAsync(long quotationId)
    {
        if (!_context.Database.IsSqlServer()) return;
        if (_context.Database.CurrentTransaction == null)
            throw new InvalidOperationException("Không thể khóa lệnh sửa chữa khi thiếu giao dịch.");
        await _context.Database.ExecuteSqlInterpolatedAsync($"SELECT so.Id FROM dbo.ServiceOrders so WITH (UPDLOCK, HOLDLOCK) INNER JOIN dbo.Quotations q ON q.ServiceOrderId = so.Id WHERE q.Id = {quotationId}");
    }

    public async Task<T> ExecuteTransactionalAsync<T>(Func<Task<T>> action)
    {
        if (_executingTransaction)
        {
            return await action();
        }

        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            _context.ChangeTracker.Clear();
            await using var tx = await TryBeginTransactionAsync();
            _executingTransaction = true;
            try
            {
                var result = await action();
                if (tx != null)
                {
                    await tx.CommitAsync();
                }

                return result;
            }
            catch
            {
                _context.ChangeTracker.Clear();
                if (tx != null)
                {
                    await tx.RollbackAsync();
                }

                throw;
            }
            finally
            {
                _executingTransaction = false;
            }
        });
    }

    public async Task<T> ExecuteCreateQuotationTransactionalAsync<T>(long serviceOrderId, Func<Task<T>> action)
    {
        try
        {
            return await ExecuteTransactionalAsync(action);
        }
        catch (DbUpdateException dbEx) when (dbEx.InnerException is SqlException sqlEx && (sqlEx.Number == 2601 || sqlEx.Number == 2627))
        {
            _logger.LogWarning(dbEx, "Xung đột tạo phiên bản báo giá đồng thời cho ServiceOrderId {ServiceOrderId}", serviceOrderId);
            throw new InvalidOperationException("Phiên bản báo giá bị xung đột do thao tác tạo đồng thời. Vui lòng thử lại.", dbEx);
        }
        catch (DbUpdateException dbEx) when (dbEx.Message.Contains("IX_Quotations_ServiceOrderId_VersionNo", StringComparison.OrdinalIgnoreCase) ||
                                            (dbEx.InnerException?.Message.Contains("IX_Quotations_ServiceOrderId_VersionNo", StringComparison.OrdinalIgnoreCase) ?? false))
        {
            _logger.LogWarning(dbEx, "Xung đột tạo phiên bản báo giá đồng thời cho ServiceOrderId {ServiceOrderId}", serviceOrderId);
            throw new InvalidOperationException("Phiên bản báo giá bị xung đột do thao tác tạo đồng thời. Vui lòng thử lại.", dbEx);
        }
    }

    public Task<T> ExecuteDiagnosisApprovalTransactionalAsync<T>(long serviceOrderId, Func<Task<T>> action) =>
        ExecuteTransactionalAsync(async () =>
        {
            if (_context.Database.IsSqlServer())
            {
                var transaction = _context.Database.CurrentTransaction
                    ?? throw new InvalidOperationException("Không thể khóa lệnh sửa chữa khi thiếu giao dịch.");
                await using var command = _context.Database.GetDbConnection().CreateCommand();
                command.Transaction = transaction.GetDbTransaction();
                command.CommandText = "SELECT Id FROM dbo.ServiceOrders WITH (UPDLOCK, HOLDLOCK) WHERE Id = @id";
                var id = command.CreateParameter();
                id.ParameterName = "@id";
                id.Value = serviceOrderId;
                command.Parameters.Add(id);
                await command.ExecuteScalarAsync();
            }
            return await action();
        });

    private async Task<IDbContextTransaction?> TryBeginTransactionAsync()
    {
        if (_context.Database.CurrentTransaction != null)
        {
            return null;
        }

        return await _context.Database.BeginTransactionAsync();
    }

    public Task<QuotationServiceOrderAdvisorInfo?> GetServiceOrderAdvisorInfoAsync(long serviceOrderId)
    {
        return _context.ServiceOrders
            .AsNoTracking()
            .Where(x => x.Id == serviceOrderId)
            .Select(x => new QuotationServiceOrderAdvisorInfo(x.Id, x.ServiceAdvisorId, x.ReceptionistId))
            .FirstOrDefaultAsync();
    }

    public Task<QuotationDiagnosisAssignmentInfo?> GetDiagnosisAssignmentInfoAsync(long diagnosisAssignmentId)
    {
        return _context.DiagnosisAssignments
            .AsNoTracking()
            .Where(x => x.Id == diagnosisAssignmentId)
            .Select(x => new QuotationDiagnosisAssignmentInfo(x.Id, x.DiagnosisId, x.Diagnosis.ServiceOrderId))
            .FirstOrDefaultAsync();
    }

    public Task<bool> DiagnosisBelongsToServiceOrderAsync(long diagnosisId, long serviceOrderId)
    {
        return _context.Diagnoses
            .AsNoTracking()
            .AnyAsync(x => x.Id == diagnosisId && x.ServiceOrderId == serviceOrderId);
    }

    public Task<long?> GetLatestDiagnosisAssignmentIdAsync(long diagnosisId)
    {
        return _context.DiagnosisAssignments
            .AsNoTracking()
            .Where(x => x.DiagnosisId == diagnosisId)
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Select(x => (long?)x.Id)
            .FirstOrDefaultAsync();
    }

    public async Task<int> GetMaxQuotationVersionNoAsync(long serviceOrderId)
    {
        return await _context.Quotations
            .AsNoTracking()
            .Where(x => x.ServiceOrderId == serviceOrderId)
            .Select(x => (int?)x.VersionNo)
            .MaxAsync() ?? 0;
    }

    public Task<List<Quotation>> GetPriorOpenQuotationsForUpdateAsync(long serviceOrderId, string statusSuperseded, string statusCustomerApproved)
    {
        return _context.Quotations
            .Where(x =>
                x.ServiceOrderId == serviceOrderId &&
                x.Status != statusSuperseded &&
                x.Status != statusCustomerApproved &&
                x.Status != nameof(QuotationStatusCode.CUSTOMER_REJECTED) &&
                x.Status != nameof(QuotationStatusCode.CANCELED))
            .ToListAsync();
    }

    public void AddQuotation(Quotation quotation)
    {
        _context.Quotations.Add(quotation);
    }

    public Task<ServiceOrder?> GetServiceOrderAsNoTrackingAsync(long serviceOrderId)
    {
        return _context.ServiceOrders.AsNoTracking().FirstOrDefaultAsync(x => x.Id == serviceOrderId);
    }

    public Task<bool> ServiceOrderExistsAsync(long serviceOrderId)
    {
        return _context.ServiceOrders.AsNoTracking().AnyAsync(x => x.Id == serviceOrderId);
    }

    public Task<List<Diagnosis>> GetNonCanceledDiagnosesAsync(long serviceOrderId, IReadOnlyList<long>? diagnosisIdFilter)
    {
        var query = _context.Diagnoses.AsNoTracking()
            .Where(x => x.ServiceOrderId == serviceOrderId)
            .Where(x => x.Status != nameof(MyGarage.Enums.DiagnosisStatus.CANCELED));
        if (diagnosisIdFilter != null && diagnosisIdFilter.Count > 0)
        {
            query = query.Where(x => diagnosisIdFilter.Contains(x.Id));
        }

        return query
            .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt ?? DateTime.MinValue)
            .ToListAsync();
    }

    public Task<List<long>> GetNonCanceledDiagnosisIdsAsync(long serviceOrderId, IReadOnlyList<long>? diagnosisIdFilter)
    {
        var query = _context.Diagnoses.AsNoTracking()
            .Where(x => x.ServiceOrderId == serviceOrderId)
            .Where(x => x.Status != nameof(MyGarage.Enums.DiagnosisStatus.CANCELED));
        if (diagnosisIdFilter != null && diagnosisIdFilter.Count > 0)
        {
            query = query.Where(x => diagnosisIdFilter.Contains(x.Id));
        }

        return query.Select(x => x.Id).Distinct().ToListAsync();
    }

    public Task<List<DiagnosisAssignment>> GetDiagnosisAssignmentsOrderedAsync(IReadOnlyList<long> diagnosisIds)
    {
        return _context.DiagnosisAssignments.AsNoTracking()
            .Where(x => diagnosisIds.Contains(x.DiagnosisId))
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .ToListAsync();
    }

    public Task<List<DiagnosisRecommendation>> GetRecommendationsByAssignmentsOrDiagnosesAsync(
        IReadOnlyList<long> assignmentIds,
        IReadOnlyList<long> diagnosisIds,
        string softDeletedStatus)
    {
        return _context.DiagnosisRecommendations.AsNoTracking()
            .Where(x =>
                (x.DiagnosisAssignmentId.HasValue && assignmentIds.Contains(x.DiagnosisAssignmentId.Value)) ||
                (!x.DiagnosisAssignmentId.HasValue && diagnosisIds.Contains(x.DiagnosisId)))
            .Where(x => x.RecommendationStatus != softDeletedStatus)
            .ToListAsync();
    }

    public Task<List<DiagnosisRecommendation>> GetRecommendationsByDiagnosesAsync(
        IReadOnlyList<long> diagnosisIds,
        string softDeletedStatus)
    {
        return _context.DiagnosisRecommendations.AsNoTracking()
            .Where(x => diagnosisIds.Contains(x.DiagnosisId))
            .Where(x => x.RecommendationStatus != softDeletedStatus)
            .ToListAsync();
    }

    public Task<bool> HasRecommendationAsync(
        IReadOnlyList<long> assignmentIds,
        IReadOnlyList<long> diagnosisIds,
        string softDeletedStatus)
    {
        return _context.DiagnosisRecommendations.AsNoTracking()
            .Where(x =>
                (x.DiagnosisAssignmentId.HasValue && assignmentIds.Contains(x.DiagnosisAssignmentId.Value)) ||
                (!x.DiagnosisAssignmentId.HasValue && diagnosisIds.Contains(x.DiagnosisId)))
            .Where(x => x.RecommendationStatus != softDeletedStatus)
            .AnyAsync();
    }

    public Task<bool> HasRecommendationByDiagnosesAsync(
        IReadOnlyList<long> diagnosisIds,
        string softDeletedStatus)
    {
        return _context.DiagnosisRecommendations.AsNoTracking()
            .Where(x => diagnosisIds.Contains(x.DiagnosisId))
            .Where(x => x.RecommendationStatus != softDeletedStatus)
            .AnyAsync();
    }

    public Task<Dictionary<long, StandardService>> GetStandardServicesMapAsync(IReadOnlyList<long> serviceIds)
    {
        return _context.StandardServices.AsNoTracking()
            .Where(x => serviceIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x);
    }

    public Task<Dictionary<long, Product>> GetProductsMapAsync(IReadOnlyList<long> productIds)
    {
        return _context.Products.AsNoTracking()
            .Where(x => productIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x);
    }

    public Task<Quotation> GetQuotationRequiredForUpdateAsync(long quotationId)
    {
        return _context.Quotations.FirstAsync(x => x.Id == quotationId);
    }
}
