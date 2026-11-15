using System;
using System.Collections.Generic;

namespace MyGarage.DTOs.Responses
{
    public sealed class MaterialRequestSummaryDto
    {
        public long Id { get; init; }
        public long? ServiceOrderId { get; init; }
        public string? ServiceOrderCode { get; init; }
        public long? TechnicianId { get; init; }
        public string? RequesterName { get; init; }
        public long? StorekeeperId { get; init; }
        public string? StorekeeperName { get; init; }
        public DateTime? RequestDate { get; init; }
        public DateTime? CreatedAt { get; init; }
        public string? Status { get; init; }
        public bool? IsRework { get; init; }
        public string? Note { get; init; }
        public string? Notes { get; init; }
        public IReadOnlyList<MaterialRequestSummaryItemDto> Items { get; init; } = Array.Empty<MaterialRequestSummaryItemDto>();
    }

    public sealed class MaterialRequestSummaryItemDto
    {
        public long Id { get; init; }
        public long ProductId { get; init; }
        public long WarehouseId { get; init; }
        public string? ProductName { get; init; }
        public string? ProductCode { get; init; }
        public string? Sku { get; init; }
        public string? Unit { get; init; }
        public decimal Quantity { get; init; }
        public decimal RequestedQuantity { get; init; }
        public decimal IssuedQuantity { get; init; }
        public string? Status { get; init; }
        public decimal CurrentStock { get; init; }
        public decimal UnitCost { get; init; }
    }

    public enum MaterialRequestCommandStatus
    {
        Success = 0,
        NotFound = 1,
        Forbidden = 2,
        BadRequest = 3
    }

    public sealed record MaterialRequestCommandResult<T>(
        MaterialRequestCommandStatus Status,
        T? Data = default,
        string? ErrorMessage = null)
    {
        public static MaterialRequestCommandResult<T> Ok(T data)
            => new(MaterialRequestCommandStatus.Success, data, null);

        public static MaterialRequestCommandResult<T> NotFound(string? errorMessage = null)
            => new(MaterialRequestCommandStatus.NotFound, default, errorMessage);

        public static MaterialRequestCommandResult<T> Forbidden()
            => new(MaterialRequestCommandStatus.Forbidden, default, null);

        public static MaterialRequestCommandResult<T> BadRequest(string errorMessage)
            => new(MaterialRequestCommandStatus.BadRequest, default, errorMessage);
    }
}
