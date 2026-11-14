using System;
namespace MyGarage.DTOs.Requests
{
    public class RetailOrderUpdateDto
    {
        public long BranchId { get; set; }
        public string? OrderCode { get; set; }
        public long? CustomerId { get; set; }
        public long? VoucherId { get; set; }
        public string? CustomerNotes { get; set; }
    }
}
