using System.ComponentModel.DataAnnotations;
namespace MyGarage.DTOs.Requests.Auth
{
    public class LoginRequestDto
    {
        public string Identifier { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? Phone { get; set; }

        [Required]
        [MinLength(6)]
        public string Password { get; set; } = string.Empty;

        public decimal? Lat { get; set; }
        public decimal? Lng { get; set; }
    }
}
