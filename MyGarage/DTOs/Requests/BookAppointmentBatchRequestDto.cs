using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace MyGarage.DTOs.Requests
{
    public class BookAppointmentBatchRequestDto
    {
        [Required]
        public long BranchId { get; set; }

        public string? CustomerNote { get; set; }

        [Required]
        public List<BookAppointmentRequestDto> Items { get; set; } = new();
    }
}

