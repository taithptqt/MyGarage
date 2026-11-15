using System.ComponentModel.DataAnnotations;

namespace MyGarage.Application.Payments.DTOs;

public class OrderCreateRequest
{
    public long? ServiceOrderId { get; set; }

    public long? InvoiceId { get; set; }

    public long? RetailOrderId { get; set; }

    public string? PayerType { get; set; }

    public long? PayerId { get; set; }

    public string? PaymentType { get; set; }

    [Required]
    public long TotalAmount { get; set; }

    public string? Description { get; set; }

    public string? ReturnUrl { get; set; }

    public string? CancelUrl { get; set; }

    public string? BuyerName { get; set; }

    public string? BuyerCompanyName { get; set; }

    [EmailAddress(ErrorMessage = ValidationMessages.Email)]
    public string? BuyerEmail { get; set; }

    [RegularExpression(ValidationPatterns.VietnamPhone10, ErrorMessage = ValidationMessages.VietnamPhone10)]
    public string? BuyerPhone { get; set; }

    public string? BuyerAddress { get; set; }

    public DateTimeOffset? ExpiredAt { get; set; }

    public bool? BuyerNotGetInvoice { get; set; }

    public PayOsTaxPercentage? TaxPercentage { get; set; }

    [Required]
    public List<OrderItemCreateRequest> Items { get; set; } = [];
}

public class OrderItemCreateRequest
{
    [Required]
    public string? Name { get; set; }

    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1")]
    public int Quantity { get; set; }

    [Required]
    public long Price { get; set; }

    public string? Unit { get; set; }

    public PayOsTaxPercentage? TaxPercentage { get; set; }
}
