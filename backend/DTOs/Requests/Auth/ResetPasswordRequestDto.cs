using System.ComponentModel.DataAnnotations;
namespace MyGarage.DTOs.Requests.Auth
{
    public class ResetPasswordRequestDto
    {
        [Required]
        [EmailAddress(ErrorMessage = ValidationMessages.Email)]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Token { get; set; } = string.Empty;

        [Required]
        [MinLength(6)]
        public string NewPassword { get; set; } = string.Empty;
    }
}
