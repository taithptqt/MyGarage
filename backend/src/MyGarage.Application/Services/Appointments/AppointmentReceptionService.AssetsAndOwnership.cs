using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyGarage.Constants;
using MyGarage.DTOs.Requests;
using MyGarage.Models;

namespace MyGarage.Services
{
    public partial class AppointmentReceptionService
    {
        private const int MaxInsuranceCardImageCount = 2;
        private const int MaxInsuranceDamageImageCount = 6;

        public async Task EnsureInsuranceCompanyExistsAsync(long? insuranceCompanyId)
        {
            if (!insuranceCompanyId.HasValue)
            {
                return;
            }

            if (insuranceCompanyId.Value <= 0)
            {
                throw new InvalidOperationException("Công ty bảo hiểm không hợp lệ.");
            }

            var exists = await _store.ActiveInsuranceCompanyExistsAsync(insuranceCompanyId.Value);

            if (!exists)
            {
                throw new InvalidOperationException("Không tìm thấy công ty bảo hiểm đã chọn.");
            }
        }

        public async Task<long?> ResolveVehicleModelIdForBookingAsync(long customerId, BookAppointmentRequestDto dto)
        {
            if (dto.VehicleModelId.HasValue && dto.VehicleModelId.Value > 0)
            {
                return dto.VehicleModelId.Value;
            }

            if (!dto.VehicleId.HasValue || dto.VehicleId.Value <= 0)
            {
                return null;
            }

            await ValidateSelectedVehicleOwnershipAsync(customerId, dto.VehicleId);
            return await _store.GetVehicleModelIdAsync(dto.VehicleId.Value, requireNotDeleted: true);
        }

        public async Task ValidateSelectedVehicleOwnershipAsync(long customerId, long? vehicleId)
        {
            if (!vehicleId.HasValue) return;

            var existingVehicle = await _store.GetVehicleByIdNoTrackingAsync(vehicleId.Value);
            if (existingVehicle == null)
                throw new InvalidOperationException("Kh\u00f4ng t\u00ecm th\u1ea5y xe \u0111\u00e3 ch\u1ecdn.");

            var customerOwnsVehicle = await _store.CustomerOwnsVehicleAsync(customerId, vehicleId.Value);
            if (!customerOwnsVehicle)
                throw new InvalidOperationException("Xe \u0111\u00e3 ch\u1ecdn kh\u00f4ng thu\u1ed9c t\u00e0i kho\u1ea3n kh\u00e1ch h\u00e0ng.");
        }

        public void UpdateCustomerContact(Customer customer, BookAppointmentRequestDto dto)
        {
            var customerFullName = dto.CustomerFullName?.Trim();
            if (!string.IsNullOrWhiteSpace(customerFullName) &&
                !string.Equals(customer.FullName, customerFullName, StringComparison.Ordinal))
            {
                customer.FullName = customerFullName;
            }

            var customerPhone = dto.CustomerPhone?.Trim();
            if (!string.IsNullOrWhiteSpace(customerPhone) &&
                !string.Equals(customer.Phone, customerPhone, StringComparison.Ordinal))
            {
                customer.Phone = customerPhone;
            }
        }

        public async Task<long?> ResolveVehicleForBookingAsync(
            long customerId,
            long? actorStaffId,
            BookAppointmentRequestDto dto)
        {
            long? vehicleId = dto.VehicleId;
            if (vehicleId != null || string.IsNullOrWhiteSpace(dto.LicensePlate) || !dto.VehicleModelId.HasValue)
                return vehicleId;

            vehicleId = await ResolveOrCreateVehicleAsync(dto, actorStaffId);
            await EnsureCustomerVehicleLinkAsync(customerId, vehicleId.Value, actorStaffId);

            return vehicleId;
        }

        public decimal? NormalizeInsuranceDeductibleAmount(decimal? value)
        {
            if (!value.HasValue) return null;
            if (value.Value < 0m) throw new InvalidOperationException("Mức miễn thường không hợp lệ.");
            return decimal.Round(value.Value, 2, MidpointRounding.AwayFromZero);
        }

        public string? NormalizeInsuranceToken(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            var normalized = value.Trim().ToUpperInvariant();
            return normalized.Length <= 30 ? normalized : normalized.Substring(0, 30);
        }

        public async Task PersistAppointmentInsuranceAssetsAsync(Appointment appointment, BookAppointmentRequestDto dto)
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

            _store.AddAppointmentInsuranceAssets(rows);
            await _store.SaveChangesInTransactionAsync();
            await MarkInsuranceUploadsAttachedAsync(appointment.Id, cardUrls.Concat(damageUrls));
        }

        private async Task<long> ResolveOrCreateVehicleAsync(BookAppointmentRequestDto dto, long? actorStaffId)
        {
            var plate = dto.LicensePlate!.Trim().ToUpper();
            var existing = await _store.GetVehicleByPlateAsync(plate);
            if (existing != null)
            {
                var changed = false;
                if (existing.Vin == null && !string.IsNullOrWhiteSpace(dto.Vin))
                {
                    existing.Vin = dto.Vin.Trim();
                    changed = true;
                }

                if (!existing.ManufactureYear.HasValue && dto.ManufactureYear.HasValue && dto.ManufactureYear.Value > 0)
                {
                    existing.ManufactureYear = dto.ManufactureYear.Value;
                    changed = true;
                }

                if (changed)
                {
                    existing.UpdatedAt = _clock.UtcNow;
                    existing.UpdatedBy = actorStaffId;
                    await _store.SaveChangesInTransactionAsync();
                }

                return existing.Id;
            }

            var newVehicle = new Vehicle
            {
                LicensePlate = plate,
                Vin = dto.Vin,
                ModelId = dto.VehicleModelId!.Value,
                Color = dto.Color,
                ManufactureYear = dto.ManufactureYear,
                CreatedBy = actorStaffId,
                CreatedAt = _clock.UtcNow
            };
            _store.AddVehicle(newVehicle);
            await _store.SaveChangesInTransactionAsync();
            return newVehicle.Id;
        }

        private async Task EnsureCustomerVehicleLinkAsync(long customerId, long vehicleId, long? actorStaffId)
        {
            var link = await _store.GetCustomerVehicleLinkAsync(customerId, vehicleId);

            if (link == null)
            {
                _store.AddCustomerVehicle(new CustomerVehicle
                {
                    CustomerId = customerId,
                    VehicleId = vehicleId,
                    IsCurrentOwner = true,
                    OwnershipStartDate = _clock.LocalToday,
                    CreatedBy = actorStaffId,
                    CreatedAt = _clock.UtcNow
                });
                await _store.SaveChangesInTransactionAsync();
                return;
            }

            if (link.CreatedBy == null)
            {
                link.CreatedBy = actorStaffId;
                await _store.SaveChangesInTransactionAsync();
            }
        }

        private async Task CopyAppointmentInsuranceAssetsAsync(long sourceAppointmentId, long targetAppointmentId)
        {
            if (sourceAppointmentId <= 0 || targetAppointmentId <= 0 || sourceAppointmentId == targetAppointmentId)
            {
                return;
            }

            var targetHasAssets = await _store.HasAppointmentInsuranceAssetsAsync(targetAppointmentId);
            if (targetHasAssets)
            {
                return;
            }

            var sourceAssets = await _store.GetAppointmentInsuranceAssetsAsync(sourceAppointmentId);
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

            _store.AddAppointmentInsuranceAssets(copiedRows);
            await _store.SaveChangesInTransactionAsync();
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

            var stagedUploads = await _store.GetStagedInsuranceUploadsByUrlsAsync(normalizedUrls);

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

            await _store.SaveChangesInTransactionAsync();
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
