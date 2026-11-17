using System;
using System.ComponentModel.DataAnnotations;
namespace MyGarage.DTOs.Requests
{
    public class InvoiceCreateDto
    {
        public long? ServiceOrderId { get; set; }
        public long? RetailOrderId { get; set; }
        public long? VehicleId { get; set; }
        [RegularExpression(ValidationPatterns.VietnamPhone10, ErrorMessage = ValidationMessages.VietnamPhone10)]
        public string? Phone { get; set; }
        public string? Address { get; set; }
        public string? InvoiceUrl { get; set; }
        public string? CompanyName { get; set; }
        public string? CompanyAddress { get; set; }
        [EmailAddress(ErrorMessage = ValidationMessages.Email)]
        public string? ReceiveEmail { get; set; }
        public string? TaxCode { get; set; }
        public string? PayerType { get; set; }
        public long? PayerId { get; set; }
        public string? CreditNote { get; set; }
        public DateTime? IssuedAt { get; set; }
        public DateTime? DueDate { get; set; }
    }
}
