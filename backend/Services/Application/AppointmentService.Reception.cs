using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Services
{
    public partial class AppointmentService
    {
        private const string ReceptionReplaceReason = "Äiá»u chá»‰nh lá»‹ch háº¹n bá»Ÿi lá»… tÃ¢n";
        private const string ReceptionReplaceWindowPassedMessage = "Không thể sửa lịch hẹn vì đã qua khung giờ đã chọn lúc tạo.";

        public async Task<ReceptionAppointmentSaveResponseDto> SaveReceptionBookingAsync(long actorUserId, ReceptionAppointmentSaveRequestDto dto)
        {
            if (dto == null)
            {
                throw new InvalidOperationException("Dá»¯ liá»‡u Ä‘áº·t lá»‹ch khÃ´ng há»£p lá»‡.");
            }

            if (dto.BranchId <= 0)
            {
                throw new InvalidOperationException("Chi nhÃ¡nh hiá»‡n táº¡i khÃ´ng há»£p lá»‡.");
            }

            if (dto.IsQuickOrder && dto.IsNewVehicle)
            {
                throw new InvalidOperationException("KhÃ´ng thá»ƒ Ä‘á»“ng thá»i chá»n khÃ¡ch vÃ£ng lai vÃ  xe má»›i cá»§a khÃ¡ch hiá»‡n cÃ³.");
            }

            var plans = dto.VehiclePlans?.Where(plan => plan != null).ToList() ?? new List<ReceptionAppointmentVehiclePlanItemDto>();
            if (plans.Count == 0)
            {
                throw new InvalidOperationException("Thiáº¿u danh sÃ¡ch xe cáº§n Ä‘áº·t lá»‹ch.");
            }

            if ((dto.IsQuickOrder || dto.IsNewVehicle) && plans.Count > 1)
            {
                throw new InvalidOperationException("Luá»“ng khÃ¡ch vÃ£ng lai hoáº·c thÃªm xe má»›i chá»‰ há»— trá»£ má»™t xe má»—i láº§n lÆ°u.");
            }

            var replaceAppointmentId = dto.ReplaceAppointmentId.GetValueOrDefault();
            if (replaceAppointmentId > 0)
            {
                await EnsureReceptionAppointmentStillEditableAsync(replaceAppointmentId);
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var actorStaffId = await _groupSessionService.ResolveStaffActorIdAsync(actorUserId);
                var resolvedCustomerId = await ResolveReceptionCustomerIdAsync(dto, actorStaffId);

                long? groupSessionId = null;
                if (plans.Count > 1)
                {
                    groupSessionId = (await _groupSessionService.CreateAsync(
                        resolvedCustomerId,
                        dto.BranchId,
                        NormalizeReceptionText(dto.CustomerNotes),
                        actorStaffId)).Id;
                }

                var createdAppointments = new List<Appointment>(plans.Count);
                foreach (var plan in plans)
                {
                    var request = await BuildReceptionBookRequestAsync(dto, plan, resolvedCustomerId, actorStaffId);
                    var created = await BookCoreAsync(actorUserId, request, groupSessionId, transaction);
                    createdAppointments.Add(created);
                }

                if (replaceAppointmentId > 0)
                {
                    var canceled = await CancelAsync(replaceAppointmentId, ReceptionReplaceReason, actorUserId);
                    if (canceled == null)
                    {
                        throw new InvalidOperationException("KhÃ´ng tÃ¬m tháº¥y lá»‹ch háº¹n cáº§n thay tháº¿.");
                    }
                }

                await transaction.CommitAsync();

                var primary = createdAppointments[0];
                var customer = await _context.Customers
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Id == resolvedCustomerId);
                var vehicle = await _context.Vehicles
                    .AsNoTracking()
                    .FirstOrDefaultAsync(v => v.Id == primary.VehicleId);

                return new ReceptionAppointmentSaveResponseDto
                {
                    GroupSessionId = groupSessionId,
                    CreatedCount = createdAppointments.Count,
                    UpdateMode = replaceAppointmentId > 0,
                    PrimaryAppointmentId = primary.Id,
                    CustomerName = customer?.FullName,
                    CustomerPhone = customer?.Phone,
                    VehicleLicensePlate = vehicle?.LicensePlate,
                    StartTime = primary.StartTime
                };
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        private async Task EnsureReceptionAppointmentStillEditableAsync(long appointmentId)
        {
            var appointment = await _context.Appointments
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == appointmentId);
            if (appointment == null)
            {
                throw new InvalidOperationException("Không tìm thấy lịch hẹn cần thay thế.");
            }

            if (DateTime.Now >= appointment.EndTime)
            {
                throw new InvalidOperationException(ReceptionReplaceWindowPassedMessage);
            }
        }

        private async Task<long> ResolveReceptionCustomerIdAsync(ReceptionAppointmentSaveRequestDto dto, long? actorStaffId)
        {
            if (dto.IsQuickOrder)
            {
                return await ResolveOrCreateReceptionGuestCustomerAsync(dto.GuestName, dto.GuestPhone, actorStaffId);
            }

            var customerId = dto.CustomerId.GetValueOrDefault();
            if (customerId <= 0)
            {
                throw new InvalidOperationException("KhÃ¡ch hÃ ng khÃ´ng há»£p lá»‡.");
            }

            var exists = await _context.Customers
                .AsNoTracking()
                .AnyAsync(c => c.Id == customerId && c.IsDeleted != true);
            if (!exists)
            {
                throw new InvalidOperationException("KhÃ´ng tÃ¬m tháº¥y khÃ¡ch hÃ ng Ä‘á»ƒ Ä‘áº·t lá»‹ch.");
            }

            return customerId;
        }

        private async Task<long> ResolveOrCreateReceptionGuestCustomerAsync(string? rawName, string? rawPhone, long? actorStaffId)
        {
            var guestName = NormalizeReceptionText(rawName);
            if (string.IsNullOrWhiteSpace(guestName))
            {
                throw new InvalidOperationException("Vui lÃ²ng nháº­p há» tÃªn khÃ¡ch vÃ£ng lai.");
            }

            var phone = NormalizeReceptionPhone(rawPhone);
            if (string.IsNullOrWhiteSpace(phone))
            {
                throw new InvalidOperationException("Vui lÃ²ng nháº­p sá»‘ Ä‘iá»‡n thoáº¡i khÃ¡ch vÃ£ng lai.");
            }

            var existing = await _context.Customers
                .FirstOrDefaultAsync(c =>
                    c.IsDeleted != true &&
                    c.Phone != null &&
                    c.Phone.Replace(" ", string.Empty) == phone);

            if (existing != null)
            {
                var changed = false;
                if (!string.Equals(existing.FullName, guestName, StringComparison.Ordinal))
                {
                    existing.FullName = guestName;
                    changed = true;
                }

                if (!string.Equals(NormalizeReceptionPhone(existing.Phone), phone, StringComparison.Ordinal))
                {
                    existing.Phone = phone;
                    changed = true;
                }

                if (existing.IsActive == false)
                {
                    existing.IsActive = true;
                    changed = true;
                }

                if (existing.IsDeleted == true)
                {
                    existing.IsDeleted = false;
                    existing.DeletedAt = null;
                    existing.DeletedBy = null;
                    changed = true;
                }

                if (changed)
                {
                    existing.UpdatedAt = _clock.UtcNow;
                    existing.UpdatedBy = actorStaffId;
                    await _context.SaveChangesAsync();
                }

                return existing.Id;
            }

            var now = _clock.UtcNow;
            var created = new Customer
            {
                FullName = guestName,
                Phone = phone,
                Type = "GUEST",
                IsActive = true,
                IsDeleted = false,
                CreatedAt = now,
                UpdatedAt = now,
                CreatedBy = actorStaffId,
                UpdatedBy = actorStaffId
            };

            _context.Customers.Add(created);
            await _context.SaveChangesAsync();
            return created.Id;
        }

        private async Task<BookAppointmentRequestDto> BuildReceptionBookRequestAsync(
            ReceptionAppointmentSaveRequestDto dto,
            ReceptionAppointmentVehiclePlanItemDto plan,
            long customerId,
            long? actorStaffId)
        {
            if (!plan.StartTime.HasValue)
            {
                throw new InvalidOperationException("NgÃ y giá» háº¹n khÃ´ng há»£p lá»‡.");
            }

            var start = plan.StartTime.Value;
            var totalMinutes = plan.TotalMinutes.GetValueOrDefault();
            if (totalMinutes <= 0 && plan.EndTime.HasValue && plan.EndTime.Value > start)
            {
                totalMinutes = (int)Math.Ceiling((plan.EndTime.Value - start).TotalMinutes);
            }
            if (totalMinutes <= 0)
            {
                totalMinutes = 30;
            }
            totalMinutes = Math.Max(totalMinutes, 30);

            var customerNotes = NormalizeReceptionText(plan.CustomerNotes) ?? NormalizeReceptionText(dto.CustomerNotes);
            var requestedServiceIds = NormalizeRequestedServiceIds(plan.RequestedServiceIds);
            var booking = new BookAppointmentRequestDto
            {
                BranchId = dto.BranchId,
                CustomerId = customerId,
                CustomerFullName = dto.IsQuickOrder
                    ? NormalizeReceptionText(dto.GuestName)
                    : NormalizeReceptionText(dto.CustomerName),
                CustomerPhone = dto.IsQuickOrder
                    ? NormalizeReceptionPhone(dto.GuestPhone)
                    : NormalizeReceptionPhone(dto.CustomerPhone),
                BookingFlow = "FIXED",
                StartTime = start,
                EndTime = start.AddMinutes(totalMinutes),
                RequestedServiceIds = requestedServiceIds,
                CustomerNotes = customerNotes,
                SlotHoldToken = NormalizeReceptionText(plan.SlotHoldToken),
                CreatedBy = actorStaffId
            };

            if (dto.IsQuickOrder)
            {
                booking.VehicleId = null;
                booking.VehicleModelId = plan.VehicleModelId ?? dto.GuestVehicleModelId;
                booking.LicensePlate = NormalizeReceptionPlate(plan.LicensePlate ?? dto.GuestVehiclePlate);
                booking.Vin = NormalizeReceptionText(plan.Vin ?? dto.GuestVehicleVin);
                booking.Color = NormalizeReceptionText(plan.Color ?? dto.GuestVehicleColor);
            }
            else if (dto.IsNewVehicle)
            {
                booking.VehicleId = null;
                booking.VehicleModelId = plan.VehicleModelId ?? dto.VehicleModelId;
                booking.LicensePlate = NormalizeReceptionPlate(plan.LicensePlate ?? dto.VehicleLicensePlate);
                booking.Vin = NormalizeReceptionText(plan.Vin ?? dto.VehicleVin);
                booking.Color = NormalizeReceptionText(plan.Color ?? dto.VehicleColor);
            }
            else
            {
                var vehicleId = plan.VehicleId.GetValueOrDefault();
                if (vehicleId <= 0)
                {
                    throw new InvalidOperationException("Vui lÃ²ng chá»n xe cá»§a khÃ¡ch.");
                }
                booking.VehicleId = vehicleId;
            }

            return booking;
        }

        private static string? NormalizeRequestedServiceIds(string? raw)
        {
            var ids = string.IsNullOrWhiteSpace(raw)
                ? new List<long>()
                : raw
                    .Split(new[] { ',', '|' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(token => long.TryParse(token.Trim(), out var id) ? id : 0)
                    .Where(id => id > 0)
                    .Distinct()
                    .ToList();

            return ids.Count > 0 ? string.Join(",", ids) : null;
        }

        private static string NormalizeReceptionPhone(string? raw)
        {
            return string.Concat((raw ?? string.Empty).Where(ch => !char.IsWhiteSpace(ch))).Trim();
        }

        private static string? NormalizeReceptionText(string? raw)
        {
            var text = raw?.Trim();
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }

        private static string? NormalizeReceptionPlate(string? raw)
        {
            var value = NormalizeReceptionText(raw);
            return string.IsNullOrWhiteSpace(value) ? null : value.ToUpperInvariant();
        }
    }
}
