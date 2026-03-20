using System.ComponentModel.DataAnnotations;
namespace MyGarage.DTOs.Requests.Auth
{
    public class GoogleLoginRequestDto
    {
        [Required]
        public string IdToken { get; set; } = string.Empty;

        public decimal? Lat { get; set; }
        public decimal? Lng { get; set; }
    }
}
