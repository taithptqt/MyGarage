<<<<<<< HEAD
using System.ComponentModel.DataAnnotations;

=======
>>>>>>> 7a2ef8fc6b783a0c6fe30341339f36ac0c9edd08
namespace MyGarage.DTOs.Requests.Auth
{
    public class ChangePasswordRequestDto
    {
<<<<<<< HEAD
        [Required]
        public string OldPassword { get; set; } = string.Empty;

        [Required]
        [MinLength(6)]
=======
        public string OldPassword { get; set; } = string.Empty;
>>>>>>> 7a2ef8fc6b783a0c6fe30341339f36ac0c9edd08
        public string NewPassword { get; set; } = string.Empty;
    }
}
