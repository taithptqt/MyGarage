using System.ComponentModel.DataAnnotations;
namespace MyGarage.DTOs.Requests.Auth
{
    public class RegisterRequestDto
    {
        [Required]
        [MaxLength(255)]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [Phone]
        public string Phone { get; set; } = string.Empty;

        [EmailAddress]
        public string? Email { get; set; }

        [Required]
        [MinLength(6)]
        public string Password { get; set; } = string.Empty;

        public decimal? Lat { get; set; }
        public decimal? Lng { get; set; }
        public string? City { get; set; }
    }
}
