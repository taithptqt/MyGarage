using System;
using System.Collections.Generic;

namespace MyGarage.DTOs.Responses.Chatbot;

public class ChatbotCustomerSelfServiceLookupResponseDto
{
    public string LookupType { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public IReadOnlyList<string> Highlights { get; set; } = Array.Empty<string>();

    public string Disclaimer { get; set; } = string.Empty;

    public ChatbotServiceHistoryLookupDto? ServiceHistory { get; set; }

    public ChatbotInvoicePaymentStatusLookupDto? InvoicePaymentStatus { get; set; }

    public ChatbotServiceOrderProgressLookupDto? ServiceOrderProgress { get; set; }

    public ChatbotLoyaltyStatusLookupDto? LoyaltyStatus { get; set; }

    public ChatbotWarrantyCheckLookupDto? WarrantyCheck { get; set; }

    public ChatbotVoucherStatusLookupDto? VoucherStatus { get; set; }
}

public class ChatbotServiceHistoryLookupDto
{
    public IReadOnlyList<ChatbotServiceHistoryItemDto> RecentItems { get; set; } = Array.Empty<ChatbotServiceHistoryItemDto>();
}

public class ChatbotServiceHistoryItemDto
{
    public long ServiceOrderId { get; set; }

    public string ServiceOrderCode { get; set; } = string.Empty;

    public string LicensePlate { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public decimal? FinalAmount { get; set; }

    public DateTime? CompletedAt { get; set; }
}

public class ChatbotInvoicePaymentStatusLookupDto
{
    public IReadOnlyList<ChatbotInvoicePaymentStatusItemDto> Invoices { get; set; } = Array.Empty<ChatbotInvoicePaymentStatusItemDto>();
}

public class ChatbotInvoicePaymentStatusItemDto
{
    public long InvoiceId { get; set; }

    public string ReferenceType { get; set; } = string.Empty;

    public string ReferenceCode { get; set; } = string.Empty;

    public decimal TotalAmount { get; set; }

    public decimal PaidAmount { get; set; }

    public decimal OutstandingAmount { get; set; }

    public string PaymentStatus { get; set; } = string.Empty;

    public DateTime? IssuedAt { get; set; }
}

public class ChatbotServiceOrderProgressLookupDto
{
    public long ServiceOrderId { get; set; }

    public string ServiceOrderCode { get; set; } = string.Empty;

    public string CurrentStatus { get; set; } = string.Empty;

    public string CurrentStage { get; set; } = string.Empty;

    public IReadOnlyList<ChatbotProgressStageDto> ProgressStages { get; set; } = Array.Empty<ChatbotProgressStageDto>();

    public DateTime? EstimatedHandover { get; set; }

    public DateTime? ActualHandover { get; set; }
}

public class ChatbotProgressStageDto
{
    public string Stage { get; set; } = string.Empty;

    public bool IsCompleted { get; set; }
}

public class ChatbotLoyaltyStatusLookupDto
{
    public string? CurrentTierName { get; set; }

    public decimal TotalSpending { get; set; }

    public decimal CurrentTierDiscountPercent { get; set; }

    public string? CurrentTierBenefits { get; set; }

    public string? NextTierName { get; set; }

    public decimal? AmountToNextTier { get; set; }
}

public class ChatbotWarrantyCheckLookupDto
{
    public IReadOnlyList<ChatbotWarrantyCheckItemDto> ActiveOrRecentWarranties { get; set; } = Array.Empty<ChatbotWarrantyCheckItemDto>();
}

public class ChatbotWarrantyCheckItemDto
{
    public string WarrantyType { get; set; } = string.Empty;

    public string ItemName { get; set; } = string.Empty;

    public string LicensePlate { get; set; } = string.Empty;

    public DateTime? WarrantyExpiryDate { get; set; }

    public int? WarrantyKmLimit { get; set; }

    public int? ServiceOdo { get; set; }

    public int? CurrentOdo { get; set; }

    public int? RemainingKm { get; set; }

    public bool IsValid { get; set; }
}

public class ChatbotVoucherStatusLookupDto
{
    public IReadOnlyList<ChatbotVoucherStatusItemDto> AvailableVouchers { get; set; } = Array.Empty<ChatbotVoucherStatusItemDto>();
}

public class ChatbotVoucherStatusItemDto
{
    public long VoucherId { get; set; }

    public string VoucherCode { get; set; } = string.Empty;

    public string VoucherName { get; set; } = string.Empty;

    public string VoucherType { get; set; } = string.Empty;

    public decimal VoucherValue { get; set; }

    public decimal? MinOrderValue { get; set; }

    public decimal? MaxDiscountAmount { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public int? RemainingUsesPerCustomer { get; set; }

    public int? RemainingIssuedCount { get; set; }

    public string Source { get; set; } = string.Empty;

    public bool IsCurrentlyActive { get; set; }
}
