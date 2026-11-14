using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services
{
    public class AppointmentReminderBackgroundService : BackgroundService
    {
        private const string Reminder24h = "24h";
        private const string Reminder2h = "2h";
        private static readonly TimeSpan ScanInterval = TimeSpan.FromMinutes(10);
        private static readonly string[] EligibleStatuses =
        {
            "PENDING",
            "CONFIRMED",
            "LATE"
        };

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<AppointmentReminderBackgroundService> _logger;
        private readonly ISystemClock _clock;

        public AppointmentReminderBackgroundService(
            IServiceScopeFactory scopeFactory,
            ILogger<AppointmentReminderBackgroundService> logger,
            ISystemClock clock)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _clock = clock;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<IGarageContext>();
                    var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

                    await ProcessReminderStageAsync(db, emailService, Reminder24h, stoppingToken);
                    await ProcessReminderStageAsync(db, emailService, Reminder2h, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Appointment reminder cycle failed.");
                }

                try
                {
                    await Task.Delay(ScanInterval, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }

        private async Task ProcessReminderStageAsync(
            IGarageContext db,
            IEmailService emailService,
            string stage,
            CancellationToken cancellationToken)
        {
            var now = _clock.LocalNow;
            var candidates = await LoadCandidatesAsync(db, now, stage, cancellationToken);

            foreach (var appointment in candidates)
            {
                try
                {
                    var marker = BuildReminderMarker(appointment.Id, stage);
                    var alreadySent = await db.Notifications
                        .AsNoTracking()
                        .AnyAsync(
                            n => n.CustomerId == appointment.CustomerId &&
                                 n.LinkAction == marker,
                            cancellationToken);

                    if (alreadySent)
                    {
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(appointment.Customer?.Email))
                    {
                        continue;
                    }

                    var body = BuildReminderEmailBody(appointment, stage, now);
                    var subject = stage == Reminder24h
                        ? "MyGarage - Nhắc lịch hẹn ngày mai"
                        : "MyGarage - Nhắc lịch hẹn sắp diễn ra";

                    await emailService.SendEmailAsync(appointment.Customer.Email!, subject, body);

                    db.Notifications.Add(new Notification
                    {
                        CustomerId = appointment.CustomerId,
                        Title = stage == Reminder24h ? "Nhắc lịch hẹn trước 1 ngày" : "Nhắc lịch hẹn trước 2 giờ",
                        Content = $"Lịch hẹn {BuildAppointmentReference(appointment.Id)} sẽ diễn ra vào {appointment.StartTime:HH:mm dd/MM/yyyy}.",
                        IsRead = false,
                        SentAt = now,
                        LinkAction = marker
                    });

                    appointment.IsReminderSent = true;
                    await db.SaveChangesAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send appointment reminder email for Appointment #{AppointmentId}", appointment.Id);
                }
            }
        }

        private async Task<List<Appointment>> LoadCandidatesAsync(
            IGarageContext db,
            DateTime now,
            string stage,
            CancellationToken cancellationToken)
        {
            var query = db.Appointments
                .Include(a => a.Customer)
                .Include(a => a.Branch)
                .Include(a => a.Vehicle)
                    .ThenInclude(v => v!.Model)
                        .ThenInclude(m => m.Brand)
                .AsQueryable();

            query = query.Where(a =>
                a.StartTime > now &&
                EligibleStatuses.Contains(a.Status) &&
                a.Customer != null &&
                a.Customer.Email != null);

            if (stage == Reminder24h)
            {
                var from = now.AddHours(22);
                var to = now.AddHours(24);
                query = query.Where(a => a.StartTime >= from && a.StartTime <= to);
            }
            else
            {
                var from = now;
                var to = now.AddHours(2);
                query = query.Where(a => a.StartTime >= from && a.StartTime <= to);
            }

            return await query
                .OrderBy(a => a.StartTime)
                .ToListAsync(cancellationToken);
        }

        private string BuildReminderEmailBody(Appointment appointment, string stage, DateTime now)
        {
            var vehicleLabel = FormatVehicleLabel(appointment);
            var branchName = appointment.Branch?.Name ?? "MyGarage";
            var branchAddress = appointment.Branch?.Address;
            var hotline = appointment.Branch?.Hotline;
            var heading = stage == Reminder24h
                ? "Lịch hẹn của bạn sẽ diễn ra vào ngày mai"
                : "Lịch hẹn của bạn sẽ diễn ra trong khoảng 2 giờ tới";
            var helperText = stage == Reminder24h
                ? "MyGarage gửi bạn lời nhắc sớm để chủ động thời gian và chuẩn bị xe."
                : "Đây là lời nhắc gần giờ hẹn để bạn thuận tiện di chuyển đến gara.";

            var builder = new StringBuilder();
            builder.AppendLine("<div style=\"font-family:Arial,sans-serif;background:#eef4ff;padding:24px;color:#1f2a37;\">");
            builder.AppendLine("<div style=\"max-width:680px;margin:0 auto;background:#ffffff;border-radius:20px;overflow:hidden;border:1px solid #dbe7ff;box-shadow:0 18px 40px rgba(29,78,216,0.12);\">");
            builder.AppendLine("<div style=\"background:linear-gradient(135deg,#0f4c81,#1d4ed8 55%,#2563eb);padding:28px 24px;color:#ffffff;\">");
            builder.AppendLine("<p style=\"margin:0 0 8px;font-size:12px;letter-spacing:0.16em;text-transform:uppercase;opacity:0.85;\">MyGarage Reminder</p>");
            builder.AppendLine($"<h2 style=\"margin:0;font-size:26px;line-height:1.25;\">{Encode(heading)}</h2>");
            builder.AppendLine($"<p style=\"margin:10px 0 0;font-size:15px;opacity:0.92;\">{Encode(helperText)}</p>");
            builder.AppendLine("</div>");
            builder.AppendLine("<div style=\"padding:24px;line-height:1.65;\">");
            builder.AppendLine($"<p>Xin chào <strong>{Encode(appointment.Customer?.FullName)}</strong>,</p>");
            builder.AppendLine("<div style=\"border:1px solid #e5ecf6;border-radius:16px;padding:18px;background:#ffffff;\">");
            builder.AppendLine($"<div style=\"font-size:12px;color:#5b6b84;text-transform:uppercase;letter-spacing:0.08em;\">Mã lịch hẹn</div>");
            builder.AppendLine($"<div style=\"margin-top:4px;font-size:18px;font-weight:700;color:#102a43;\">{BuildAppointmentReference(appointment.Id)}</div>");
            builder.AppendLine($"<div style=\"margin-top:12px;font-size:15px;\"><strong>Biển số / xe:</strong> {Encode(vehicleLabel)}</div>");
            builder.AppendLine($"<div style=\"margin-top:8px;font-size:15px;\"><strong>Thời gian:</strong> {Encode($"{appointment.StartTime:HH:mm dd/MM/yyyy} - {appointment.EndTime:HH:mm dd/MM/yyyy}")}</div>");
            builder.AppendLine($"<div style=\"margin-top:8px;font-size:15px;\"><strong>Chi nhánh:</strong> {Encode(branchName)}</div>");
            if (!string.IsNullOrWhiteSpace(branchAddress))
            {
                builder.AppendLine($"<div style=\"margin-top:8px;font-size:15px;\"><strong>Địa chỉ:</strong> {Encode(branchAddress)}</div>");
            }
            if (!string.IsNullOrWhiteSpace(hotline))
            {
                builder.AppendLine($"<div style=\"margin-top:8px;font-size:15px;\"><strong>Hotline:</strong> {Encode(hotline)}</div>");
            }
            builder.AppendLine("</div>");
            builder.AppendLine("<div style=\"margin-top:18px;background:#fffaf0;border:1px solid #fde7b2;border-radius:14px;padding:16px 18px;\">");
            builder.AppendLine("<div style=\"font-size:14px;color:#6b4f00;\">Vui lòng đến sớm 10-15 phút. Nếu cần đổi lịch, hãy liên hệ garage càng sớm càng tốt để được hỗ trợ.</div>");
            builder.AppendLine("</div>");
            builder.AppendLine("<p style=\"margin-top:24px;\">Trân trọng,<br><strong>Đội ngũ MyGarage</strong></p>");
            builder.AppendLine("</div>");
            builder.AppendLine($"<div style=\"background:#f8fafc;color:#64748b;font-size:12px;padding:16px 24px;text-align:center;\">Email nhắc lịch được gửi tự động lúc {Encode(now.ToString("dd/MM/yyyy HH:mm"))}.</div>");
            builder.AppendLine("</div>");
            builder.AppendLine("</div>");
            return builder.ToString();
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

        private static string BuildAppointmentReference(long appointmentId)
        {
            return $"AP-{appointmentId:D6}";
        }

        private static string BuildReminderMarker(long appointmentId, string stage)
        {
            return $"/appointments/{appointmentId}/email-reminders/{stage}";
        }

        private static string Encode(string? value)
        {
            return WebUtility.HtmlEncode(value ?? string.Empty);
        }
    }
}
