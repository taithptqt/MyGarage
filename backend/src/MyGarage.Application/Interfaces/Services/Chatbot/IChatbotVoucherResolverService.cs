using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.DTOs.Responses.Chatbot;

namespace MyGarage.Interfaces;

public interface IChatbotVoucherResolverService
{
    Task<ChatbotVoucherResolveResult> ResolveBestVoucherAsync(
        ChatbotVoucherResolveRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class ChatbotVoucherResolveRequest
{
    public long CustomerId { get; set; }

    public long? BranchId { get; set; }

    public long? VehicleId { get; set; }

    public long? CustomerTierId { get; set; }

    public decimal EligibleAmount { get; set; }

    public decimal TierDiscountAmount { get; set; }

    public ChatbotVoucherOrderType OrderType { get; set; }

    public IReadOnlyList<long> ServiceIds { get; set; } = Array.Empty<long>();

    public IReadOnlyList<long> ProductIds { get; set; } = Array.Empty<long>();
}

public sealed class ChatbotVoucherResolveResult
{
    public bool IsApplied { get; set; }

    public string? ReasonCode { get; set; }

    public string? Message { get; set; }

    public ChatbotVoucherResolutionDto? Voucher { get; set; }
}

public enum ChatbotVoucherOrderType
{
    BOOKING = 0,
    SERVICE_ORDER_DRAFT = 1,
    RETAIL_ORDER_DRAFT = 2
}
