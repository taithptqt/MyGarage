using System.ComponentModel.DataAnnotations;

namespace MyGarage.DTOs.Requests.Auth
{
    public class GoogleLoginRequestDto
    {
        [Required]
        public string IdToken { get; set; } = string.Empty;
    }
}
