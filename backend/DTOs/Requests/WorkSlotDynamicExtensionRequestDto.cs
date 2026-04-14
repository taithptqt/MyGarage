using System;
using System.ComponentModel.DataAnnotations;

namespace MyGarage.DTOs.Requests
{
    public class WorkSlotDynamicExtensionRequestDto
    {
        [Required]
        public string Action { get; set; } = "EXTEND";
        public int? AdditionalMinutes { get; set; }
        public bool SaveOvernight { get; set; }
        public bool TryBorrowDvnBay { get; set; }
        public DateTime? PerformedAt { get; set; }
    }
}
