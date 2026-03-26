using System;

namespace MyGarage.DTOs.Requests
{
    public class CustomerCreateDto
    {
        public string? FullName { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? GoogleId { get; set; }
        public string? Type { get; set; }
        public string? Password { get; set; }
        public long? TierId { get; set; }
        public string? Note { get; set; }
    }
}
