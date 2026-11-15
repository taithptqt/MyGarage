using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MyGarage.Application.Interfaces.Stores.Chatbot;
using MyGarage.DTOs.Responses.Chatbot;
using MyGarage.Enums;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Infrastructure.Persistence.Stores.Chatbot;

public sealed partial class EfChatbotStore : IChatbotStore
{
    private readonly IGarageContext _context;

    public EfChatbotStore(IGarageContext context)
    {
        _context = context;
    }

    public async Task<ChatbotSupportBranch?> ResolveSupportBranchAsync(long? branchId, CancellationToken ct = default)
    {
        if (branchId.HasValue)
        {
            var branch = await _context.Branches
                .AsNoTracking()
                .Where(x => x.Id == branchId.Value && x.IsDeleted != true)
                .Select(x => new ChatbotSupportBranch
                {
                    Name = x.Name,
                    Hotline = x.Hotline
                })
                .FirstOrDefaultAsync(ct);
            if (branch is not null)
            {
                return branch;
            }
        }

        return await _context.Branches
            .AsNoTracking()
            .Where(x => x.IsDeleted != true && !string.IsNullOrWhiteSpace(x.Hotline))
            .OrderBy(x => x.Id)
            .Select(x => new ChatbotSupportBranch
            {
                Name = x.Name,
                Hotline = x.Hotline
            })
            .FirstOrDefaultAsync(ct);
    }

    public async Task EnsureCustomerIsActiveAsync(long customerId, CancellationToken ct = default)
    {
        var customer = await _context.Customers
            .AsNoTracking()
            .Where(x => x.Id == customerId)
            .Select(x => new { x.Id, x.IsActive, x.IsDeleted })
            .FirstOrDefaultAsync(ct);
        if (customer is null || customer.IsDeleted == true)
        {
            throw new KeyNotFoundException("Customer not found.");
        }

        if (customer.IsActive == false)
        {
            throw new InvalidOperationException("Customer is inactive.");
        }
    }

    public async Task<ChatbotSession> CreateSessionWithTransactionAsync(ChatbotSession entity, CancellationToken ct = default)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await TryBeginTransactionAsync(ct);
            await _context.ChatbotSessions.AddAsync(entity, ct);
            await _context.SaveChangesAsync(ct);
            if (tx is not null)
            {
                await tx.CommitAsync(ct);
            }

            return entity;
        });
    }

    public Task<ChatbotSession?> GetSessionByIdAsync(long customerId, long sessionId, bool track = false, CancellationToken ct = default)
    {
        var query = track ? _context.ChatbotSessions : _context.ChatbotSessions.AsNoTracking();
        return query.FirstOrDefaultAsync(x => x.Id == sessionId && x.CustomerId == customerId, ct);
    }

    public Task<ChatbotSession?> GetSessionByTokenAsync(long customerId, string sessionToken, bool track = false, CancellationToken ct = default)
    {
        var query = track ? _context.ChatbotSessions : _context.ChatbotSessions.AsNoTracking();
        return query.FirstOrDefaultAsync(x => x.SessionToken == sessionToken && x.CustomerId == customerId, ct);
    }

    public async Task<IReadOnlyList<ChatbotMessage>> LoadSessionHistoryAsync(long sessionId, int take, CancellationToken ct = default)
    {
        return await _context.ChatbotMessages.AsNoTracking()
            .Where(x => x.SessionId == sessionId)
            .OrderBy(x => x.CreatedAt)
            .ThenBy(x => x.Id)
            .Take(take)
            .ToListAsync(ct);
    }

    public async Task SaveMessagesWithTransactionAsync(
        ChatbotSession session,
        IReadOnlyList<ChatbotMessage> newMessages,
        Action<ChatbotSession> applySessionUpdates,
        CancellationToken ct = default)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await TryBeginTransactionAsync(ct);
            foreach (var message in newMessages)
            {
                await _context.ChatbotMessages.AddAsync(message, ct);
            }

            applySessionUpdates(session);

            await _context.SaveChangesAsync(ct);
            if (tx is not null)
            {
                await tx.CommitAsync(ct);
            }
        });
    }

    public Task SaveChangesAsync(CancellationToken ct = default)
    {
        return _context.SaveChangesAsync(ct);
    }

    public Task<Customer?> GetActiveCustomerEntityAsync(long customerId, CancellationToken ct = default)
    {
        return _context.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == customerId && c.IsDeleted != true, ct);
    }

    public Task<List<ChatbotBookingVehicleOptionDto>> GetBookingVehicleOptionsAsync(long customerId, CancellationToken ct = default)
    {
        return _context.CustomerVehicles.AsNoTracking()
            .Where(cv => cv.CustomerId == customerId && cv.IsDeleted != true && cv.IsCurrentOwner != false)
            .Select(cv => new ChatbotBookingVehicleOptionDto
            {
                VehicleId = cv.VehicleId,
                LicensePlate = cv.Vehicle.LicensePlate,
                VehicleModelName = string.Concat(cv.Vehicle.Model.Brand.Name, " ", cv.Vehicle.Model.ModelName).Trim()
            })
            .OrderBy(x => x.LicensePlate)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ChatbotBookingCategoryRow>> GetActiveServiceCategoriesAsync(CancellationToken ct = default)
    {
        return await _context.StandardServices
            .AsNoTracking()
            .Where(service => service.IsDeleted != true && service.IsActive == true && service.Category.IsDeleted != true)
            .Select(service => new ChatbotBookingCategoryRow
            {
                CategoryId = service.CategoryId,
                CategoryName = service.Category.Name
            })
            .Distinct()
            .OrderBy(item => item.CategoryName)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ChatbotBookingServiceCatalogRow>> GetActiveServiceCatalogAsync(CancellationToken ct = default)
    {
        return await _context.StandardServices
            .AsNoTracking()
            .Where(service => service.IsDeleted != true && service.IsActive == true && service.Category.IsDeleted != true)
            .Select(service => new ChatbotBookingServiceCatalogRow
            {
                ServiceId = service.Id,
                ServiceCode = service.Code,
                ServiceName = service.Name,
                CategoryId = service.CategoryId,
                CategoryCode = service.Category.Code,
                CategoryName = service.Category.Name,
                Description = service.Description
            })
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ChatbotBookingServiceOptionRow>> GetActiveServicesByCategoryAsync(long categoryId, CancellationToken ct = default)
    {
        return await _context.StandardServices
            .AsNoTracking()
            .Where(service =>
                service.IsDeleted != true
                && service.IsActive == true
                && service.CategoryId == categoryId)
            .OrderBy(service => service.Name)
            .Select(service => new ChatbotBookingServiceOptionRow
            {
                ServiceId = service.Id,
                ServiceCode = service.Code,
                ServiceName = service.Name,
                BasePrice = service.BasePrice,
                StandardMinutes = Math.Max(15, service.StandardMinutes)
            })
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ChatbotBookingBranchRow>> GetNonDeletedBranchesAsync(CancellationToken ct = default)
    {
        return await _context.Branches
            .AsNoTracking()
            .Where(branch => branch.IsDeleted != true)
            .OrderBy(branch => branch.Name)
            .Select(branch => new ChatbotBookingBranchRow
            {
                Id = branch.Id,
                Name = branch.Name,
                Status = branch.Status ?? string.Empty,
                Description = branch.Description ?? string.Empty,
                Address = branch.Address,
                Hotline = branch.Hotline
            })
            .ToListAsync(ct);
    }

    public Task<Dictionary<long, int>> GetServiceDurationsAsync(IReadOnlyCollection<long> serviceIds, CancellationToken ct = default)
    {
        return _context.StandardServices
            .AsNoTracking()
            .Where(s => serviceIds.Contains(s.Id))
            .Select(s => new { s.Id, Minutes = Math.Max(15, s.StandardMinutes) })
            .ToDictionaryAsync(x => x.Id, x => x.Minutes, ct);
    }

    public Task<BranchException?> GetBranchExceptionAsync(long branchId, DateOnly date, CancellationToken ct = default)
    {
        return _context.BranchExceptions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.BranchId == branchId && x.ExceptionDate == date, ct);
    }

    public Task<BranchOperatingHour?> GetBranchOperatingHourAsync(long branchId, int dayOfWeek, CancellationToken ct = default)
    {
        return _context.BranchOperatingHours
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.BranchId == branchId && x.DayOfWeek == dayOfWeek, ct);
    }

    public async Task<Vehicle> CreateOrLinkVehicleWithTransactionAsync(
        long customerId,
        string plate,
        long modelId,
        string? vin,
        int? manufactureYear,
        DateTime utcNow,
        DateOnly localToday,
        CancellationToken ct = default)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await TryBeginTransactionAsync(ct);
            try
            {
                var vehicle = await _context.Vehicles.FirstOrDefaultAsync(v => v.LicensePlate == plate && v.IsDeleted != true, ct);
                if (vehicle is null)
                {
                    vehicle = new Vehicle
                    {
                        LicensePlate = plate,
                        ModelId = modelId,
                        Vin = vin,
                        ManufactureYear = manufactureYear,
                        CreatedAt = utcNow,
                        CreatedBy = customerId,
                        IsDeleted = false
                    };
                    _context.Vehicles.Add(vehicle);
                    await _context.SaveChangesAsync(ct);
                }

                var linked = await _context.CustomerVehicles.AnyAsync(cv => cv.CustomerId == customerId && cv.VehicleId == vehicle.Id && cv.IsDeleted != true, ct);
                if (!linked)
                {
                    _context.CustomerVehicles.Add(new CustomerVehicle
                    {
                        CustomerId = customerId,
                        VehicleId = vehicle.Id,
                        IsCurrentOwner = true,
                        OwnershipStartDate = localToday,
                        CreatedAt = utcNow,
                        CreatedBy = customerId,
                        IsDeleted = false
                    });
                    await _context.SaveChangesAsync(ct);
                }

                if (tx is not null)
                {
                    await tx.CommitAsync(ct);
                }

                return vehicle;
            }
            catch
            {
                if (tx is not null)
                {
                    await tx.RollbackAsync(ct);
                }
                throw;
            }
        });
    }

    public Task<bool> VehicleModelExistsAsync(long modelId, CancellationToken ct = default)
    {
        return _context.VehicleModels
            .AsNoTracking()
            .AnyAsync(model => model.Id == modelId && model.IsDeleted != true, ct);
    }

    public async Task<IReadOnlyList<ChatbotVehicleModelCandidate>> GetVehicleModelCandidatesAsync(CancellationToken ct = default)
    {
        return await _context.VehicleModels
            .AsNoTracking()
            .Where(model => model.IsDeleted != true && model.Brand.IsDeleted != true)
            .Select(model => new ChatbotVehicleModelCandidate
            {
                Id = model.Id,
                Label = string.Concat(model.Brand.Name, " ", model.ModelName)
            })
            .ToListAsync(ct);
    }

    public Task<ChatbotVehicleSummary?> GetVehicleSummaryAsync(long vehicleId, CancellationToken ct = default)
    {
        return _context.Vehicles
            .AsNoTracking()
            .Where(vehicle => vehicle.Id == vehicleId && vehicle.IsDeleted != true)
            .Select(vehicle => new ChatbotVehicleSummary
            {
                LicensePlate = vehicle.LicensePlate,
                BrandName = vehicle.Model != null && vehicle.Model.Brand != null ? vehicle.Model.Brand.Name : null,
                ModelName = vehicle.Model != null ? vehicle.Model.ModelName : null
            })
            .FirstOrDefaultAsync(ct);
    }

    public Task<string?> GetBranchNameAsync(long branchId, CancellationToken ct = default)
    {
        return _context.Branches
            .AsNoTracking()
            .Where(branch => branch.Id == branchId && branch.IsDeleted != true)
            .Select(branch => branch.Name)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<ChatbotBookingServiceSummaryRow>> GetActiveServiceSummariesAsync(IReadOnlyCollection<long> serviceIds, CancellationToken ct = default)
    {
        return await _context.StandardServices
            .AsNoTracking()
            .Where(service => serviceIds.Contains(service.Id) && service.IsDeleted != true && service.IsActive == true)
            .Select(service => new ChatbotBookingServiceSummaryRow
            {
                Name = service.Name,
                BasePrice = service.BasePrice,
                Minutes = Math.Max(15, service.StandardMinutes)
            })
            .ToListAsync(ct);
    }

    public async Task AddChatbotActionLogWithTransactionAsync(ChatbotActionLog log, CancellationToken ct = default)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await TryBeginTransactionAsync(ct);
            _context.ChatbotActionLogs.Add(log);
            await _context.SaveChangesAsync(ct);
            if (tx is not null)
            {
                await tx.CommitAsync(ct);
            }
        });
    }

    public async Task<decimal> GetServicesBasePriceSumAsync(IReadOnlyCollection<long> serviceIds, CancellationToken ct = default)
    {
        var baseAmount = await _context.StandardServices
            .AsNoTracking()
            .Where(service => serviceIds.Contains(service.Id) && service.IsDeleted != true && service.IsActive == true)
            .SumAsync(service => (decimal?)service.BasePrice, ct);

        return Math.Max(0m, baseAmount ?? 0m);
    }

    private async Task<IDbContextTransaction?> TryBeginTransactionAsync(CancellationToken cancellationToken)
    {
        if (_context.Database.CurrentTransaction != null)
        {
            return null;
        }

        return await _context.Database.BeginTransactionAsync(cancellationToken);
    }
}
