using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using MyGarage.Application.Interfaces.Stores.Appointments;
using MyGarage.Application.Interfaces.Stores.Chatbot;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services
{
    public class AppointmentNotificationService : IAppointmentNotificationService
    {
        private readonly IAppointmentScheduleQueryStore _scheduleStore;
        private readonly IAppointmentCatalogStore _catalogStore;
        private readonly ISystemClock _clock;
        private readonly IEmailService _emailService;
        private readonly IAppLogger<AppointmentNotificationService> _logger;

        public AppointmentNotificationService(
            IAppointmentScheduleQueryStore scheduleStore,
            IAppointmentCatalogStore catalogStore,
            ISystemClock clock,
            IEmailService emailService,
            IAppLogger<AppointmentNotificationService> logger)
        {
            _scheduleStore = scheduleStore;
            _catalogStore = catalogStore;
            _clock = clock;
            _emailService = emailService;
            _logger = logger;
        }

        public async Task TrySendBookingSuccessEmailAsync(long appointmentId)
        {
            try
            {
                var appointment = await LoadAppointmentsForEmailAsync(new[] { appointmentId });
                await TrySendBookingSuccessEmailAsync(appointment);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AppointmentNotificationService] Failed to send booking success email for Appointment #{AppointmentId}", appointmentId);
            }
        }

        public async Task TrySendBookingSuccessEmailAsync(IReadOnlyCollection<long> appointmentIds)
        {
            try
            {
                if (appointmentIds == null || appointmentIds.Count == 0)
                {
                    return;
                }

                var appointments = await LoadAppointmentsForEmailAsync(appointmentIds);
                await TrySendBookingSuccessEmailAsync(appointments);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AppointmentNotificationService] Failed to send batch booking success email.");
            }
        }

        private async Task<List<Appointment>> LoadAppointmentsForEmailAsync(IEnumerable<long> appointmentIds)
        {
            var ids = appointmentIds?
                .Where(id => id > 0)
                .Distinct()
                .ToList() ?? new List<long>();

            if (ids.Count == 0)
            {
                return new List<Appointment>();
            }

            return await _scheduleStore.GetAppointmentsForEmailAsync(ids);
        }

        private async Task TrySendBookingSuccessEmailAsync(IReadOnlyCollection<Appointment> appointments)
        {
            if (appointments == null || appointments.Count == 0)
            {
                return;
            }

            var primary = appointments
                .OrderBy(a => a.StartTime)
                .FirstOrDefault();

            if (primary?.Customer == null || string.IsNullOrWhiteSpace(primary.Customer.Email))
            {
                _logger.LogWarning(
                    "[AppointmentNotificationService] Booking email skipped because customer email is missing. AppointmentIds={AppointmentIds}",
                    string.Join(",", appointments.Select(a => a.Id)));
                return;
            }

            var serviceNameMap = await LoadServiceNameMapAsync(appointments);
            var packageNameMap = await LoadPackageNameMapAsync(appointments);

            var subject = appointments.Count > 1
                ? "MyGarage - Xác nhận đặt lịch thành công"
                : "MyGarage - Xác nhận lịch hẹn";

            var body = BuildBookingSuccessEmailBody(
                appointments.OrderBy(a => a.StartTime).ToList(),
                primary.Customer,
                primary.Branch,
                serviceNameMap,
                packageNameMap);

            QueueEmailSend(primary.Customer.Email!, subject, body);
        }

        private void QueueEmailSend(string toEmail, string subject, string body)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await _emailService.SendEmailAsync(toEmail, subject, body);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[AppointmentNotificationService] Background email send failed for {ToEmail}", toEmail);
                }
            });
        }

        private async Task<Dictionary<long, string>> LoadServiceNameMapAsync(IReadOnlyCollection<Appointment> appointments)
        {
            var ids = appointments
                .SelectMany(a => AppointmentSchedulingHelpers.ParseIds(a.RequestedServiceIds))
                .Distinct()
                .ToList();

            if (ids.Count == 0)
            {
                return new Dictionary<long, string>();
            }

            return await _catalogStore.GetStandardServiceNamesByIdsAsync(ids);
        }

        private async Task<Dictionary<long, string>> LoadPackageNameMapAsync(IReadOnlyCollection<Appointment> appointments)
        {
            var ids = appointments
                .SelectMany(a => AppointmentSchedulingHelpers.ParseIds(a.RequestedPackageIds))
                .Distinct()
                .ToList();

            if (ids.Count == 0)
            {
                return new Dictionary<long, string>();
            }

            return await _catalogStore.GetPackageNamesByIdsAsync(ids);
        }

        private string BuildBookingSuccessEmailBody(
            IReadOnlyList<Appointment> appointments,
            Customer customer,
            Branch? branch,
            IReadOnlyDictionary<long, string> serviceNameMap,
            IReadOnlyDictionary<long, string> packageNameMap)
        {
            var primary = appointments[0];
            var groupCode = primary.GroupSession?.GroupCode;

            var builder = new StringBuilder();
            builder.AppendLine("<div style=\"font-family:Arial,sans-serif;background:#eef4ff;padding:24px;color:#1f2a37;\">");
            builder.AppendLine("<div style=\"max-width:720px;margin:0 auto;background:#ffffff;border-radius:20px;overflow:hidden;border:1px solid #dbe7ff;box-shadow:0 18px 40px rgba(29,78,216,0.12);\">");
            builder.AppendLine("<div style=\"background:linear-gradient(135deg,#0f4c81,#1d4ed8 55%,#2563eb);padding:28px 24px;color:#ffffff;\">");
            builder.AppendLine("<div style=\"display:flex;justify-content:space-between;gap:16px;align-items:flex-start;flex-wrap:wrap;\">");
            builder.AppendLine("<div>");
            builder.AppendLine("<p style=\"margin:0 0 8px;font-size:12px;letter-spacing:0.16em;text-transform:uppercase;opacity:0.85;\">MyGarage Booking Desk</p>");
            builder.AppendLine("<h2 style=\"margin:0;font-size:26px;line-height:1.25;\">Xác nhận đặt lịch thành công</h2>");
            builder.AppendLine("<p style=\"margin:10px 0 0;font-size:15px;opacity:0.92;\">Garage đã nhận lịch hẹn của bạn và đang chuẩn bị tiếp nhận xe.</p>");
            builder.AppendLine("</div>");
            builder.AppendLine("<div style=\"min-width:180px;background:rgba(255,255,255,0.12);border:1px solid rgba(255,255,255,0.24);border-radius:14px;padding:14px 16px;\">");
            builder.AppendLine($"<div style=\"font-size:12px;opacity:0.82;\">Mã tham chiếu</div><div style=\"margin-top:4px;font-size:18px;font-weight:700;\">{Encode(string.IsNullOrWhiteSpace(groupCode) ? BuildAppointmentReference(primary.Id) : groupCode)}</div>");
            builder.AppendLine("</div>");
            builder.AppendLine("</div>");
            builder.AppendLine("</div>");

            builder.AppendLine("<div style=\"padding:24px;line-height:1.65;\">");
            builder.AppendLine($"<p>Xin chào <strong>{Encode(customer.FullName)}</strong>,</p>");
            builder.AppendLine("<p>Lịch hẹn của bạn đã được ghi nhận thành công trên hệ thống MyGarage. Dưới đây là thông tin chi tiết để bạn tiện theo dõi và xuất trình khi đến gara.</p>");

            builder.AppendLine("<div style=\"display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:14px;margin:20px 0;\">");
            builder.AppendLine("<div style=\"background:#f8fbff;border:1px solid #d9e7ff;border-radius:14px;padding:16px 18px;\">");
            builder.AppendLine("<div style=\"font-size:12px;color:#5b6b84;text-transform:uppercase;letter-spacing:0.08em;\">Chi nhánh tiếp nhận</div>");
            builder.AppendLine($"<div style=\"margin-top:6px;font-size:18px;font-weight:700;color:#102a43;\">{Encode(branch?.Name ?? "MyGarage")}</div>");
            if (!string.IsNullOrWhiteSpace(branch?.Address))
            {
                builder.AppendLine($"<div style=\"margin-top:8px;font-size:14px;color:#486581;\">{Encode(branch.Address)}</div>");
            }
            if (!string.IsNullOrWhiteSpace(branch?.Hotline))
            {
                builder.AppendLine($"<div style=\"margin-top:8px;font-size:14px;color:#1d4ed8;\"><strong>Hotline:</strong> {Encode(branch.Hotline)}</div>");
            }
            builder.AppendLine("</div>");
            builder.AppendLine("<div style=\"background:#fffaf0;border:1px solid #fde7b2;border-radius:14px;padding:16px 18px;\">");
            builder.AppendLine("<div style=\"font-size:12px;color:#8a6d1d;text-transform:uppercase;letter-spacing:0.08em;\">Lưu ý trước khi đến</div>");
            builder.AppendLine("<div style=\"margin-top:8px;font-size:14px;color:#6b4f00;\">Vui lòng đến sớm 10-15 phút, mang theo giấy tờ xe nếu cần kiểm tra bảo hiểm hoặc làm thủ tục tiếp nhận nhanh.</div>");
            builder.AppendLine("</div>");
            builder.AppendLine("</div>");

            builder.AppendLine("<h3 style=\"margin:24px 0 12px;font-size:18px;color:#0f172a;\">Chi tiết từng lịch hẹn</h3>");

            foreach (var appointment in appointments)
            {
                var appointmentServices = ResolveAppointmentServiceLabels(appointment, serviceNameMap, packageNameMap);
                builder.AppendLine("<div style=\"border:1px solid #e5ecf6;border-radius:16px;padding:18px;margin-top:14px;background:#ffffff;\">");
                builder.AppendLine("<div style=\"display:flex;justify-content:space-between;gap:12px;align-items:flex-start;flex-wrap:wrap;\">");
                builder.AppendLine("<div>");
                builder.AppendLine("<div style=\"font-size:12px;color:#5b6b84;text-transform:uppercase;letter-spacing:0.08em;\">Mã lịch hẹn</div>");
                builder.AppendLine($"<div style=\"margin-top:4px;font-size:18px;font-weight:700;color:#102a43;\">{Encode(BuildAppointmentReference(appointment.Id))}</div>");
                builder.AppendLine("</div>");
                builder.AppendLine($"<div style=\"padding:8px 12px;border-radius:999px;background:#e8f1ff;color:#1d4ed8;font-size:13px;font-weight:700;\">{Encode((appointment.Status ?? "PENDING").ToUpperInvariant())}</div>");
                builder.AppendLine("</div>");
                builder.AppendLine($"<div style=\"margin-top:14px;font-size:15px;\"><strong>Biển số / xe:</strong> {Encode(FormatVehicleLabel(appointment))}</div>");
                builder.AppendLine($"<div style=\"margin-top:8px;font-size:15px;\"><strong>Thời gian:</strong> {Encode(FormatAppointmentWindow(appointment))}</div>");
                builder.AppendLine($"<div style=\"margin-top:8px;font-size:15px;\"><strong>Dịch vụ:</strong> {Encode(appointmentServices.Count > 0 ? string.Join(", ", appointmentServices) : "Kiểm tra theo yêu cầu khi tiếp nhận")}</div>");
                if (!string.IsNullOrWhiteSpace(appointment.CustomerNotes))
                {
                    builder.AppendLine($"<div style=\"margin-top:8px;font-size:15px;\"><strong>Ghi chú:</strong> {Encode(appointment.CustomerNotes)}</div>");
                }
                builder.AppendLine("</div>");
            }

            builder.AppendLine("<div style=\"margin-top:22px;background:#f8fafc;border:1px dashed #cbd5e1;border-radius:14px;padding:16px 18px;\">");
            builder.AppendLine("<div style=\"font-size:14px;color:#334155;\">Nếu bạn cần đổi lịch, vui lòng liên hệ chi nhánh trước giờ hẹn hoặc thao tác trong tài khoản MyGarage để gara kịp sắp xếp.</div>");
            builder.AppendLine("</div>");
            builder.AppendLine("<p style=\"margin-top:24px;\">Trân trọng,<br><strong>Đội ngũ MyGarage</strong></p>");
            builder.AppendLine("</div>");

            builder.AppendLine("<div style=\"background:#f8fafc;color:#64748b;font-size:12px;padding:16px 24px;text-align:center;\">");
            builder.AppendLine($"Email này được gửi tự động lúc {Encode(_clock.LocalNow.ToString("dd/MM/yyyy HH:mm"))}.");
            builder.AppendLine("</div>");
            builder.AppendLine("</div>");
            builder.AppendLine("</div>");

            return builder.ToString();
        }

        private static List<string> ResolveAppointmentServiceLabels(
            Appointment appointment,
            IReadOnlyDictionary<long, string> serviceNameMap,
            IReadOnlyDictionary<long, string> packageNameMap)
        {
            return AppointmentSchedulingHelpers.ParseIds(appointment.RequestedServiceIds)
                .Select(id => serviceNameMap.TryGetValue(id, out var name) ? name : null)
                .Concat(AppointmentSchedulingHelpers.ParseIds(appointment.RequestedPackageIds)
                    .Select(id => packageNameMap.TryGetValue(id, out var name) ? name : null))
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name!.Trim())
                .Distinct()
                .ToList();
        }

        private static string FormatVehicleLabel(Appointment appointment)
        {
            if (appointment.Vehicle == null)
            {
                return "Xe chưa cập nhật";
            }

            var brand = appointment.Vehicle.Model?.Brand?.Name;
            var model = appointment.Vehicle.Model?.ModelName;
            var plate = appointment.Vehicle.LicensePlate;
            var parts = new[] { plate, brand, model }
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!.Trim());

            var label = string.Join(" - ", parts);
            return string.IsNullOrWhiteSpace(label) ? $"Xe #{appointment.VehicleId}" : label;
        }

        private static string FormatAppointmentWindow(Appointment appointment)
        {
            return $"{appointment.StartTime:HH:mm dd/MM/yyyy} - {appointment.EndTime:HH:mm dd/MM/yyyy}";
        }

        private static string BuildAppointmentReference(long appointmentId)
        {
            return $"AP-{appointmentId:D6}";
        }

        private static string Encode(string? value)
        {
            return WebUtility.HtmlEncode(value ?? string.Empty);
        }
    }
}
