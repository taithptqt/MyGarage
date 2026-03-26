namespace MyGarage.DTOs.Responses.Auth
{
    public class AuthResponseDto
    {
        public long Id { get; set; }
        public string Token { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string RoleCode { get; set; } = string.Empty;
        public string AvatarUrl { get; set; } = string.Empty;
        public long? BranchId { get; set; }
        public string? BranchName { get; set; }
    }
}
