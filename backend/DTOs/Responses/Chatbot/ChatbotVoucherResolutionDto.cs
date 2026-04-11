using System;

namespace MyGarage.DTOs.Responses.Chatbot;

public class ChatbotVoucherResolutionDto
{
    public long VoucherId { get; set; }

    public string VoucherCode { get; set; } = string.Empty;

    public decimal DiscountAmount { get; set; }

    public DateTime ExpiresAt { get; set; }
}
