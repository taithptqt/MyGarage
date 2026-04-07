using System;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MyGarage.Enums;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services
{
    public class ServiceOrderService : GenericService<ServiceOrder>, IServiceOrderService
    {
        private readonly IGarageContext _context;
        private const int MaxCodeGenerationAttempts = 8;
        private const string CheckInLogType = "CHECK_IN";
        private const string CheckInPendingStatus = nameof(DiagnosisStatus.PENDING);

        public ServiceOrderService(IServiceOrderRepository repository, IGarageContext context) : base(repository)
        {
            _context = context;
        }

        public override async Task<ServiceOrder> AddAsync(ServiceOrder entity)
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            Exception? lastDuplicateException = null;

            for (var attempt = 0; attempt < MaxCodeGenerationAttempts; attempt++)
            {
                var candidateCode = GenerateServiceOrderCode();

                var exists = await _context.ServiceOrders
                    .AsNoTracking()
                    .AnyAsync(x => x.Code == candidateCode);
                if (exists)
                {
                    continue;
                }

                entity.Code = candidateCode;

                try
                {
                    var createdOrder = await _repository.AddAsync(entity);
                    await EnsureCheckInRequestExistsAsync(createdOrder.Id);
                    return createdOrder;
                }
                catch (DbUpdateException ex) when (IsDuplicateCodeViolation(ex))
                {
                    lastDuplicateException = ex;
                    var entry = _context.Entry(entity);
                    if (entry.State != EntityState.Detached)
                    {
                        entry.State = EntityState.Detached;
                    }
                }
            }

            throw new InvalidOperationException(
                "Khong the tao ma lenh sua chua duy nhat. Vui long thu lai.",
                lastDuplicateException);
        }

        private async Task EnsureCheckInRequestExistsAsync(long serviceOrderId)
        {
            if (serviceOrderId <= 0)
            {
                return;
            }

            var hasAnyCheckInLog = await _context.VehicleConditionLogs
                .AsNoTracking()
                .AnyAsync(x => x.ServiceOrderId == serviceOrderId && x.LogType == CheckInLogType);
            if (hasAnyCheckInLog)
            {
                return;
            }

            var now = DateTime.UtcNow;
            _context.VehicleConditionLogs.Add(new VehicleConditionLog
            {
                ServiceOrderId = serviceOrderId,
                LogType = CheckInLogType,
                DashboardWarningLights = CheckInPendingStatus,
                CreatedAt = now,
                UpdatedAt = now
            });

            await _context.SaveChangesAsync();
        }

        private static string GenerateServiceOrderCode()
        {
            var timestamp = DateTime.UtcNow.ToString("yyMMddHHmmssfff", CultureInfo.InvariantCulture);
            var entropy = Guid.NewGuid().ToString("N")[..4].ToUpperInvariant();
            return $"SO-{timestamp}-{entropy}";
        }

        private static bool IsDuplicateCodeViolation(Exception ex)
        {
            var cursor = ex;
            while (cursor != null)
            {
                if (cursor is SqlException sql && (sql.Number == 2601 || sql.Number == 2627))
                {
                    return true;
                }

                cursor = cursor.InnerException;
            }

            return false;
        }
    }
}
