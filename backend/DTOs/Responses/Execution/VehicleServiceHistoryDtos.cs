using System;
using System.Collections.Generic;

namespace MyGarage.DTOs.Responses.Execution
{
    public sealed class VehicleServiceHistoryItemDto
    {
        public long ServiceOrderId { get; set; }

        public string ServiceOrderCode { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;

        public DateTime? CheckInTime { get; set; }

        public DateTime? ActualHandover { get; set; }

        public decimal TotalAmount { get; set; }

        public decimal PaidAmount { get; set; }

        public string? PaymentStatus { get; set; }

        public string? QcStatus { get; set; }

        public int DiagnosisCount { get; set; }

        public int ServiceLineCount { get; set; }

        public int PartLineCount { get; set; }

        public List<string> HighlightItems { get; set; } = new();
    }

    public sealed class VehicleServiceHistoryResponseDto
    {
        public long VehicleId { get; set; }

        public string? LicensePlate { get; set; }

        public string? Vin { get; set; }

        public int TotalServiceOrders { get; set; }

        public int Page { get; set; }

        public int PageSize { get; set; }

        public int TotalPages { get; set; }

        public List<VehicleServiceHistoryItemDto> Items { get; set; } = new();
    }
}
