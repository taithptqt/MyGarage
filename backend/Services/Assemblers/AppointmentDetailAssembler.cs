using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Data;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services
{
    public class AppointmentDetailAssembler : IAppointmentDetailAssembler
    {
        private readonly IGarageContext _context;

        public AppointmentDetailAssembler(IGarageContext context)
        {
            _context = context;
        }

        public async Task<AppointmentDetailResponseDto> AssembleAsync(Appointment appointment)
        {
            var items = await AssembleManyAsync(new[] { appointment });
            return items.First();
        }

        public async Task<IEnumerable<AppointmentDetailResponseDto>> AssembleManyAsync(IEnumerable<Appointment> appointments)
        {
            var list = appointments?.ToList() ?? new List<Appointment>();
            if (list.Count == 0) return new List<AppointmentDetailResponseDto>();

            var serviceIds = list
                .SelectMany(a => ParseServiceIds(a.RequestedServiceIds))
                .Distinct()
                .ToList();

            var serviceLookup = serviceIds.Count == 0
                ? new Dictionary<long, string>()
                : await _context.StandardServices
                    .AsNoTracking()
                    .Where(s => serviceIds.Contains(s.Id))
                    .ToDictionaryAsync(s => s.Id, s => s.Name ?? $"#{s.Id}");

            return list.Select(a =>
            {
                var dto = new AppointmentDetailResponseDto
                {
                    Id = a.Id,
                    BranchId = a.BranchId,
                    BranchName = a.Branch?.Name ?? "",
                    CustomerId = a.CustomerId,
                    CustomerName = a.Customer?.FullName ?? "",
                    CustomerPhone = a.Customer?.Phone,
                    VehicleId = a.VehicleId,
                    VehicleLicensePlate = a.Vehicle?.LicensePlate,
                    VehicleModelName = a.Vehicle?.Model?.Brand?.Name != null && a.Vehicle?.Model?.ModelName != null
                        ? $"{a.Vehicle.Model.Brand.Name} {a.Vehicle.Model.ModelName}"
                        : null,
                    GroupSessionId = a.GroupSessionId,
                    GroupSessionCode = a.GroupSession?.GroupCode,
                    GroupSessionStatus = a.GroupSession?.Status,
                    CreatedBy = a.CreatedBy,
                    StartTime = a.StartTime,
                    EndTime = a.EndTime,
                    CreatedAt = a.CreatedAt,
                    RequestedServiceIds = a.RequestedServiceIds,
                    CustomerNotes = a.CustomerNotes,
                    IsReminderSent = a.IsReminderSent,
                    Status = a.Status,
                    CancelReason = a.CancelReason,
                    UpdatedAt = a.UpdatedAt,
                };

                foreach (var sid in ParseServiceIds(a.RequestedServiceIds))
                {
                    dto.ServiceNames.Add(serviceLookup.TryGetValue(sid, out var name) ? name : $"#{sid}");
                }

                return dto;
            }).ToList();
        }

        private static IEnumerable<long> ParseServiceIds(string? csv)
        {
            if (string.IsNullOrWhiteSpace(csv)) return Enumerable.Empty<long>();
            return csv
                .Split(',', System.StringSplitOptions.RemoveEmptyEntries)
                .Select(s => long.TryParse(s.Trim(), out var id) ? id : 0)
                .Where(id => id > 0);
        }
    }
}

