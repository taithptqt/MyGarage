using System.ComponentModel.DataAnnotations;
namespace MyGarage.DTOs.Requests.Auth
{
    public class ForgotPasswordRequestDto
    {
        [Required]
        [EmailAddress(ErrorMessage = ValidationMessages.Email)]
        public string Email { get; set; } = string.Empty;
    }
}
