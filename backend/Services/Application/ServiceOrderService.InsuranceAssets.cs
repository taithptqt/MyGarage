using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Constants;
using MyGarage.Models;

namespace MyGarage.Services
{
    public partial class ServiceOrderService
    {
        private const int MaxServiceOrderInsuranceCardImageCount = 2;
        private const int MaxServiceOrderInsuranceDamageImageCount = 6;

        private async Task PersistServiceOrderInsuranceAssetsAsync(
            long serviceOrderId,
            IEnumerable<string>? insuranceCardImageUrls,
            IEnumerable<string>? insuranceDamageImageUrls)
        {
            if (serviceOrderId <= 0)
            {
                return;
            }

            var cardUrls = SanitizeServiceOrderInsuranceAssetUrls(
                insuranceCardImageUrls,
                MaxServiceOrderInsuranceCardImageCount);
            var damageUrls = SanitizeServiceOrderInsuranceAssetUrls(
                insuranceDamageImageUrls,
                MaxServiceOrderInsuranceDamageImageCount);

            if (cardUrls.Count == 0 && damageUrls.Count == 0)
            {
                return;
            }

            var rows = new List<ServiceOrderInsuranceAsset>(cardUrls.Count + damageUrls.Count);
            rows.AddRange(cardUrls.Select((url, index) => new ServiceOrderInsuranceAsset
            {
                ServiceOrderId = serviceOrderId,
                AssetType = AppointmentInsuranceAssetTypes.Card,
                FileUrl = url,
                SortOrder = index
            }));
            rows.AddRange(damageUrls.Select((url, index) => new ServiceOrderInsuranceAsset
            {
                ServiceOrderId = serviceOrderId,
                AssetType = AppointmentInsuranceAssetTypes.Damage,
                FileUrl = url,
                SortOrder = index
            }));

            _context.ServiceOrderInsuranceAssets.AddRange(rows);
            await _context.SaveChangesAsync();
            await MarkServiceOrderInsuranceUploadsAttachedAsync(serviceOrderId, cardUrls.Concat(damageUrls));
        }

        private async Task ReplaceServiceOrderInsuranceAssetsAsync(
            long serviceOrderId,
            IEnumerable<string>? insuranceCardImageUrls,
            IEnumerable<string>? insuranceDamageImageUrls)
        {
            if (serviceOrderId <= 0)
            {
                return;
            }

            var existingRows = await _context.ServiceOrderInsuranceAssets
                .Where(asset => asset.ServiceOrderId == serviceOrderId)
                .ToListAsync();
            if (existingRows.Count > 0)
            {
                _context.ServiceOrderInsuranceAssets.RemoveRange(existingRows);
                await _context.SaveChangesAsync();
            }

            await PersistServiceOrderInsuranceAssetsAsync(
                serviceOrderId,
                insuranceCardImageUrls,
                insuranceDamageImageUrls);
        }

        private async Task MarkServiceOrderInsuranceUploadsAttachedAsync(
            long serviceOrderId,
            IEnumerable<string> fileUrls)
        {
            var normalizedUrls = fileUrls?
                .Where(url => !string.IsNullOrWhiteSpace(url))
                .Select(url => url.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList() ?? new List<string>();

            if (serviceOrderId <= 0 || normalizedUrls.Count == 0)
            {
                return;
            }

            var stagedUploads = await _context.ServiceOrderInsuranceUploads
                .Where(upload =>
                    upload.Status == ServiceOrderInsuranceUploadStatuses.Staged &&
                    upload.ServiceOrderId == null &&
                    normalizedUrls.Contains(upload.FileUrl))
                .ToListAsync();

            if (stagedUploads.Count == 0)
            {
                return;
            }

            foreach (var upload in stagedUploads)
            {
                upload.Status = ServiceOrderInsuranceUploadStatuses.Attached;
                upload.ServiceOrderId = serviceOrderId;
                upload.UpdatedAt = _clock.UtcNow;
            }

            await _context.SaveChangesAsync();
        }

        private static List<string> SanitizeServiceOrderInsuranceAssetUrls(
            IEnumerable<string>? urls,
            int maxCount)
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
