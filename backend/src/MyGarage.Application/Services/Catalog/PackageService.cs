using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services
{
    public class PackageService : GenericService<Package>, IPackageService
    {
        private readonly IPackageRepository _packageRepository;

        public PackageService(IPackageRepository repository) : base(repository)
        {
            _packageRepository = repository;
        }

        public Task<IEnumerable<Package>> GetAllWithItemsAsync()
        {
            return _packageRepository.GetAllWithItemsAsync();
        }

        public Task<Package?> GetByIdWithItemsAsync(long id)
        {
            return _packageRepository.GetByIdWithItemsAsync(id);
        }

        public async Task<IEnumerable<PackageResponseDto>> GetAllResponseAsync()
        {
            var items = await GetAllWithItemsAsync();
            return items.Select(ToResponseDto).ToList();
        }

        public async Task<PackageResponseDto?> GetByIdResponseAsync(long id)
        {
            var item = await GetByIdWithItemsAsync(id);
            return item == null ? null : ToResponseDto(item);
        }

        public async Task<PackageResponseDto> CreateFromDtoAsync(PackageCreateDto dto)
        {
            ArgumentNullException.ThrowIfNull(dto);

            var entity = ToEntity(dto);
            var created = await AddAsync(entity);
            var loaded = await GetByIdWithItemsAsync(created.Id) ?? created;
            return ToResponseDto(loaded);
        }

        public async Task<PackageResponseDto?> UpdateFromDtoAsync(long id, PackageUpdateDto dto)
        {
            ArgumentNullException.ThrowIfNull(dto);

            var existing = await GetByIdAsync(id);
            if (existing == null)
            {
                return null;
            }

            ApplyUpdate(existing, dto);

            var updated = await UpdateAsync(id, existing);
            var loaded = updated == null ? null : await GetByIdWithItemsAsync(updated.Id);
            return loaded == null ? null : ToResponseDto(loaded);
        }

        internal static Package ToEntity(PackageCreateDto dto)
        {
            var now = DateTime.UtcNow;
            return new Package
            {
                Code = (dto.Code ?? string.Empty).Trim(),
                Name = (dto.Name ?? string.Empty).Trim(),
                CategoryId = dto.CategoryId,
                BasePrice = dto.BasePrice,
                StandardMinutes = dto.StandardMinutes,
                Description = dto.Description,
                ImageUrl = dto.ImageUrl,
                IsActive = dto.IsActive ?? true,
                IsDeleted = false,
                CreatedAt = now,
                UpdatedAt = now,
                PackageItems = (dto.Items ?? Enumerable.Empty<PackageItemRequestDto>())
                    .Where(item => item.StandardServiceId > 0)
                    .Select(item => new PackageItem
                    {
                        StandardServiceId = item.StandardServiceId,
                        Quantity = item.Quantity <= 0 ? 1m : item.Quantity,
                        SortOrder = item.SortOrder ?? 0,
                        IsDefault = item.IsDefault ?? true,
                        IsActive = true,
                        IsDeleted = false,
                        CreatedAt = now,
                        UpdatedAt = now
                    })
                    .ToList()
            };
        }

        internal static void ApplyUpdate(Package existing, PackageUpdateDto dto)
        {
            existing.Code = (dto.Code ?? string.Empty).Trim();
            existing.Name = (dto.Name ?? string.Empty).Trim();
            existing.CategoryId = dto.CategoryId;
            existing.BasePrice = dto.BasePrice;
            existing.StandardMinutes = dto.StandardMinutes;
            existing.Description = dto.Description;
            existing.ImageUrl = dto.ImageUrl;
            existing.IsActive = dto.IsActive ?? true;
            existing.UpdatedAt = DateTime.UtcNow;
        }

        internal static PackageResponseDto ToResponseDto(Package item)
        {
            return new PackageResponseDto
            {
                Id = item.Id,
                Code = item.Code,
                Name = item.Name,
                CategoryId = item.CategoryId,
                CategoryCode = item.Category?.Code,
                CategoryName = item.Category?.Name,
                BasePrice = item.BasePrice,
                StandardMinutes = item.StandardMinutes,
                Description = item.Description,
                ImageUrl = item.ImageUrl,
                IsActive = item.IsActive,
                CreatedAt = item.CreatedAt,
                UpdatedAt = item.UpdatedAt,
                Items = item.PackageItems
                    .OrderBy(packageItem => packageItem.SortOrder ?? 0)
                    .ThenBy(packageItem => packageItem.Id)
                    .Select(packageItem => new PackageItemResponseDto
                    {
                        Id = packageItem.Id,
                        PackageId = packageItem.PackageId,
                        StandardServiceId = packageItem.StandardServiceId,
                        StandardServiceCode = packageItem.StandardService?.Code,
                        StandardServiceName = packageItem.StandardService?.Name,
                        Quantity = packageItem.Quantity,
                        SortOrder = packageItem.SortOrder,
                        IsDefault = packageItem.IsDefault,
                        IsActive = packageItem.IsActive
                    })
                    .ToList()
            };
        }
    }
}
