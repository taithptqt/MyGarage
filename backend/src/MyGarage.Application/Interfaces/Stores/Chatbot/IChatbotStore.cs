using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.DTOs.Responses.Chatbot;
using MyGarage.Models;

namespace MyGarage.Application.Interfaces.Stores.Chatbot;

public sealed class ChatbotSupportBranch
{
    public string? Name { get; init; }
    public string? Hotline { get; init; }
}

public sealed class ChatbotProductMatch
{
    public long Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Sku { get; init; } = string.Empty;
    public decimal? RetailPrice { get; init; }
    public decimal? ServicePrice { get; init; }
}

public sealed class ChatbotInventoryStockRow
{
    public decimal Quantity { get; init; }
    public decimal Reserved { get; init; }
}

public sealed class ChatbotCustomerTierSnapshot
{
    public long Id { get; init; }
    public decimal? TotalSpending { get; init; }
    public string? TierName { get; init; }
    public decimal? TierDiscount { get; init; }
    public string? TierBenefits { get; init; }
}

public sealed class ChatbotNextTierSnapshot
{
    public string TierName { get; init; } = string.Empty;
    public decimal MinSpending { get; init; }
}

public sealed class ChatbotVoucherLookupRow
{
    public long VoucherId { get; init; }
    public string VoucherCode { get; init; } = string.Empty;
    public string VoucherName { get; init; } = string.Empty;
    public string VoucherType { get; init; } = string.Empty;
    public decimal VoucherValue { get; init; }
    public decimal? MinOrderValue { get; init; }
    public decimal? MaxDiscountAmount { get; init; }
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public int MaxUsesPerCustomer { get; init; }
    public int? RemainingIssuedCount { get; set; }
    public string Source { get; init; } = string.Empty;
}

public sealed class ChatbotVehicleContextSnapshot
{
    public long VehicleId { get; init; }
    public long VehicleModelId { get; init; }
    public long VehicleBrandId { get; init; }
}

public sealed class ChatbotToolVehicleRow
{
    public long VehicleId { get; init; }
    public string LicensePlate { get; init; } = string.Empty;
    public string ModelName { get; init; } = string.Empty;
    public string BrandName { get; init; } = string.Empty;
}

public sealed class ChatbotToolBranchRow
{
    public long BranchId { get; init; }
    public string BranchName { get; init; } = string.Empty;
    public string? Status { get; init; }
    public string? Address { get; init; }
    public string? Hotline { get; init; }
}

public sealed class ChatbotToolServiceRow
{
    public long ServiceId { get; init; }
    public string? ServiceCode { get; init; }
    public string? ServiceName { get; init; }
    public decimal BasePrice { get; init; }
    public int StandardMinutes { get; init; }
}

public sealed class ChatbotCustomerContact
{
    public string? FullName { get; init; }
    public string? Phone { get; init; }
}

public sealed class ChatbotToolServiceHistoryRow
{
    public long OrderId { get; init; }
    public string OrderCode { get; init; } = string.Empty;
    public string? Status { get; init; }
    public string? PaymentStatus { get; init; }
    public decimal? FinalAmount { get; init; }
    public DateTime? CreatedAt { get; init; }
    public long VehicleId { get; init; }
    public string LicensePlate { get; init; } = string.Empty;
    public string VehicleModel { get; init; } = string.Empty;
}

public sealed class ChatbotToolServiceOrderStatusRow
{
    public long OrderId { get; init; }
    public string OrderCode { get; init; } = string.Empty;
    public string? Status { get; init; }
    public string? QcStatus { get; init; }
    public string? PaymentStatus { get; init; }
    public DateTime? PromisedHandover { get; init; }
    public DateTime? ActualHandover { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

public sealed class ChatbotToolInvoiceStatusRow
{
    public long InvoiceId { get; init; }
    public string? ServiceOrderCode { get; init; }
    public string? RetailOrderCode { get; init; }
    public decimal? TotalAmount { get; init; }
    public decimal? PaidAmount { get; init; }
    public string? PaymentStatus { get; init; }
    public DateTime? IssuedAt { get; init; }
    public decimal RemainingAmount { get; init; }
}

public sealed class ChatbotToolWarrantyRow
{
    public long ServiceOrderId { get; init; }
    public string ServiceOrderCode { get; init; } = string.Empty;
    public long VehicleId { get; init; }
    public string LicensePlate { get; init; } = string.Empty;
    public string ItemName { get; init; } = string.Empty;
    public bool? IsWarrantyItem { get; init; }
    public int? WarrantyMonths { get; init; }
    public int? WarrantyKm { get; init; }
    public string? ServiceOrderStatus { get; init; }
    public DateTime? CreatedAt { get; init; }
}

public sealed class ChatbotToolServiceDraftRow
{
    public long ServiceId { get; init; }
    public string? ServiceCode { get; init; }
    public string? ServiceName { get; init; }
    public decimal BasePrice { get; init; }
}

public sealed class ChatbotToolProductRow
{
    public long Id { get; init; }
    public string? Sku { get; init; }
    public string? Name { get; init; }
    public decimal UnitPrice { get; init; }
}

public sealed class ChatbotBookingCategoryRow
{
    public long CategoryId { get; init; }
    public string CategoryName { get; init; } = string.Empty;
}

public sealed class ChatbotBookingServiceCatalogRow
{
    public long ServiceId { get; init; }
    public string? ServiceCode { get; init; }
    public string ServiceName { get; init; } = string.Empty;
    public long CategoryId { get; init; }
    public string? CategoryCode { get; init; }
    public string CategoryName { get; init; } = string.Empty;
    public string? Description { get; init; }
}

public sealed class ChatbotBookingServiceOptionRow
{
    public long ServiceId { get; init; }
    public string? ServiceCode { get; init; }
    public string ServiceName { get; init; } = string.Empty;
    public decimal BasePrice { get; init; }
    public int StandardMinutes { get; init; }
}

public sealed class ChatbotBookingBranchRow
{
    public long Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string? Address { get; init; }
    public string? Hotline { get; init; }
}

public sealed class ChatbotVehicleModelCandidate
{
    public long Id { get; init; }
    public string Label { get; init; } = string.Empty;
}

public sealed class ChatbotVehicleSummary
{
    public string? LicensePlate { get; init; }
    public string? BrandName { get; init; }
    public string? ModelName { get; init; }
}

public sealed class ChatbotBookingServiceSummaryRow
{
    public string? Name { get; init; }
    public decimal BasePrice { get; init; }
    public int Minutes { get; init; }
}

public interface IChatbotStore :
    IChatbotSessionStore,
    IChatbotLoyaltyVoucherStore,
    IChatbotToolQueryStore,
    IChatbotBookingStore
{
}
