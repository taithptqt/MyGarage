using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Constants;
using MyGarage.DTOs.Requests;
using MyGarage.Models;

namespace MyGarage.Services
{
    public partial class AppointmentService
    {
        private const int MaxInsuranceCardImageCount = 2;
        private const int MaxInsuranceDamageImageCount = 6;

        private async Task PersistAppointmentInsuranceAssetsAsync(Appointment appointment, BookAppointmentRequestDto dto)
        {
            if (appointment == null || appointment.Id <= 0 || dto == null)
            {
                return;
            }

            if (!dto.InsuranceCompanyId.HasValue || dto.InsuranceCompanyId.Value <= 0)
            {
                return;
            }

            var cardUrls = SanitizeInsuranceAssetUrls(dto.InsuranceCardImageUrls, MaxInsuranceCardImageCount);
            var damageUrls = SanitizeInsuranceAssetUrls(dto.InsuranceDamageImageUrls, MaxInsuranceDamageImageCount);
            if (cardUrls.Count == 0 && damageUrls.Count == 0)
            {
                return;
            }

            var rows = new List<AppointmentInsuranceAsset>(cardUrls.Count + damageUrls.Count);
            rows.AddRange(cardUrls.Select((url, index) => new AppointmentInsuranceAsset
            {
                AppointmentId = appointment.Id,
                AssetType = AppointmentInsuranceAssetTypes.Card,
                FileUrl = url,
                SortOrder = index
            }));
            rows.AddRange(damageUrls.Select((url, index) => new AppointmentInsuranceAsset
            {
                AppointmentId = appointment.Id,
                AssetType = AppointmentInsuranceAssetTypes.Damage,
                FileUrl = url,
                SortOrder = index
            }));

            _context.AppointmentInsuranceAssets.AddRange(rows);
            await _context.SaveChangesAsync();
            await MarkInsuranceUploadsAttachedAsync(appointment.Id, cardUrls.Concat(damageUrls));
        }

        private async Task CopyAppointmentInsuranceAssetsAsync(long sourceAppointmentId, long targetAppointmentId)
        {
            if (sourceAppointmentId <= 0 || targetAppointmentId <= 0 || sourceAppointmentId == targetAppointmentId)
            {
                return;
            }

            var targetHasAssets = await _context.AppointmentInsuranceAssets
                .AsNoTracking()
                .AnyAsync(asset => asset.AppointmentId == targetAppointmentId);
            if (targetHasAssets)
            {
                return;
            }

            var sourceAssets = await _context.AppointmentInsuranceAssets
                .AsNoTracking()
                .Where(asset => asset.AppointmentId == sourceAppointmentId)
                .OrderBy(asset => asset.SortOrder)
                .ThenBy(asset => asset.Id)
                .ToListAsync();
            if (sourceAssets.Count == 0)
            {
                return;
            }

            var copiedRows = sourceAssets.Select(asset => new AppointmentInsuranceAsset
            {
                AppointmentId = targetAppointmentId,
                AssetType = asset.AssetType,
                FileUrl = asset.FileUrl,
                SortOrder = asset.SortOrder
            });

            _context.AppointmentInsuranceAssets.AddRange(copiedRows);
            await _context.SaveChangesAsync();
        }

        private async Task CopyAppointmentInsuranceAssetsToServiceOrderAsync(long appointmentId, long serviceOrderId)
        {
            if (appointmentId <= 0 || serviceOrderId <= 0)
            {
                return;
            }

            var targetHasAssets = await _context.ServiceOrderInsuranceAssets
                .AsNoTracking()
                .AnyAsync(asset => asset.ServiceOrderId == serviceOrderId);
            if (targetHasAssets)
            {
                return;
            }

            var sourceAssets = await _context.AppointmentInsuranceAssets
                .AsNoTracking()
                .Where(asset => asset.AppointmentId == appointmentId)
                .OrderBy(asset => asset.SortOrder)
                .ThenBy(asset => asset.Id)
                .ToListAsync();
            if (sourceAssets.Count == 0)
            {
                return;
            }

            var copiedRows = sourceAssets.Select(asset => new ServiceOrderInsuranceAsset
            {
                ServiceOrderId = serviceOrderId,
                AssetType = asset.AssetType,
                FileUrl = asset.FileUrl,
                SortOrder = asset.SortOrder
            });

            _context.ServiceOrderInsuranceAssets.AddRange(copiedRows);
            await _context.SaveChangesAsync();
        }

        private async Task MarkInsuranceUploadsAttachedAsync(long appointmentId, IEnumerable<string> fileUrls)
        {
            var normalizedUrls = fileUrls?
                .Where(url => !string.IsNullOrWhiteSpace(url))
                .Select(url => url.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList() ?? new List<string>();

            if (appointmentId <= 0 || normalizedUrls.Count == 0)
            {
                return;
            }

            var stagedUploads = await _context.AppointmentInsuranceUploads
                .Where(upload =>
                    upload.Status == AppointmentInsuranceUploadStatuses.Staged &&
                    upload.AppointmentId == null &&
                    normalizedUrls.Contains(upload.FileUrl))
                .ToListAsync();

            if (stagedUploads.Count == 0)
            {
                return;
            }

            foreach (var upload in stagedUploads)
            {
                upload.Status = AppointmentInsuranceUploadStatuses.Attached;
                upload.AppointmentId = appointmentId;
                upload.UpdatedAt = _clock.UtcNow;
            }

            await _context.SaveChangesAsync();
        }

        private static List<string> SanitizeInsuranceAssetUrls(IEnumerable<string>? urls, int maxCount)
        {
            var results = new List<string>();
            if (urls == null)
            {
                return results;
            }

            foreach (var rawUrl in urls)
            {
                if (results.Count >= maxCount)
                {
                    break;
                }

                var url = (rawUrl ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(url))
                {
                    continue;
                }

                if (url.Length > 500)
                {
                    throw new InvalidOperationException("Đường dẫn ảnh bảo hiểm không hợp lệ.");
                }

                if (!Uri.TryCreate(url, UriKind.Absolute, out var parsedUrl) ||
                    (parsedUrl.Scheme != Uri.UriSchemeHttp && parsedUrl.Scheme != Uri.UriSchemeHttps))
                {
                    throw new InvalidOperationException("Đường dẫn ảnh bảo hiểm không hợp lệ.");
                }

                if (!results.Contains(url, StringComparer.OrdinalIgnoreCase))
                {
                    results.Add(url);
                }
            }

            return results;
        }
    }
}
