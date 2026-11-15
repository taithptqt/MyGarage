using System;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MyGarage.Interfaces;
using MyGarage.DTOs.Requests.Auth;
using MyGarage.DTOs.Responses.Auth;
using MyGarage.Security;

namespace MyGarage.Controllers
{
    [Route("api/auth")]
    [Route("api/v1/auth")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;
        private readonly ITokenBlacklistService _tokenBlacklistService;

        public AuthController(
            IAuthService authService,
            IConfiguration configuration,
            IWebHostEnvironment environment,
            ITokenBlacklistService tokenBlacklistService)
        {
            _authService = authService;
            _configuration = configuration;
            _environment = environment;
            _tokenBlacklistService = tokenBlacklistService;
        }

        [AllowAnonymous]
        [EnableRateLimiting("auth")]
        [HttpPost("sessions")]
        public async Task<ActionResult<AuthResponseDto>> Login([FromBody] LoginRequestDto request)
        {
            try
            {
                var response = await _authService.LoginAsync(request);
                AuthCookie.Append(Response, response.Token, _configuration, _environment);
                return Ok(response);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [AllowAnonymous]
        [EnableRateLimiting("auth")]
        [HttpPost("accounts")]
        public async Task<ActionResult<AuthResponseDto>> Register([FromBody] RegisterRequestDto request)
        {
            try
            {
                var response = await _authService.RegisterAsync(request);
                AuthCookie.Append(Response, response.Token, _configuration, _environment);
                return Ok(response);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.InnerException != null ? ex.InnerException.Message : ex.Message });
            }
        }

        [AllowAnonymous]
        [EnableRateLimiting("auth")]
        [HttpPost("external-sessions/google")]
        public async Task<ActionResult<AuthResponseDto>> GoogleLogin([FromBody] GoogleLoginRequestDto request)
        {
            try
            {
                var response = await _authService.GoogleLoginAsync(request);
                AuthCookie.Append(Response, response.Token, _configuration, _environment);
                return Ok(response);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.InnerException != null ? ex.InnerException.Message : ex.Message });
            }
        }

        [AllowAnonymous]
        [HttpGet("session")]
        public async Task<IActionResult> GetSession()
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return NoContent();
            }

            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var role = User.FindFirst(ClaimTypes.Role)?.Value;
            if (string.IsNullOrEmpty(userIdStr) || !long.TryParse(userIdStr, out var userId) || string.IsNullOrEmpty(role))
            {
                return NoContent();
            }

            try
            {
                var profile = await _authService.GetProfileAsync(userId, role);
                return Ok(profile);
            }
            catch (UnauthorizedAccessException)
            {
                AuthCookie.Delete(Response, _environment);
                return NoContent();
            }
        }

        [AllowAnonymous]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            var authHeader = Request.Headers.Authorization.ToString();
            var rawToken = authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? authHeader[7..].Trim()
                : Request.Cookies[AuthCookie.Name];

            if (User.Identity?.IsAuthenticated == true && !string.IsNullOrWhiteSpace(rawToken))
            {
                await _tokenBlacklistService.RevokeTokenAsync(rawToken);
            }

            AuthCookie.Delete(Response, _environment);
            return Ok(new { message = "Đăng xuất thành công." });
        }

        [AllowAnonymous]
        [EnableRateLimiting("auth")]
        [HttpPost("credential-recovery-requests")]
        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequestDto request)
        {
            try
            {
                await _authService.ForgotPasswordAsync(request);
                return Ok(new { Message = "Đã gửi liên kết thành công" });
            }
            catch (ValidationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [AllowAnonymous]
        [EnableRateLimiting("auth")]
        [HttpPost("credential-recovery-finalizations")]
        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequestDto request)
        {
            try
            {
                await _authService.ResetPasswordAsync(request);
                return Ok(new { Message = "Password has been successfully reset." });
            }
            catch (ValidationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
        }

        [Authorize]
        [HttpPatch("password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequestDto request)
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var role = User.FindFirst(ClaimTypes.Role)?.Value;

            if (string.IsNullOrEmpty(userIdStr) || !long.TryParse(userIdStr, out long userId) || string.IsNullOrEmpty(role))
            {
                return Unauthorized(new { Message = "Invalid user claims." });
            }

            try
            {
                await _authService.ChangePasswordAsync(userId, role, request);
                AuthCookie.Delete(Response, _environment);
                return Ok(new { Message = "Password has been successfully changed.", requiresReauthentication = true });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
        }

        [Authorize]
        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile()
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var role = User.FindFirst(ClaimTypes.Role)?.Value;

            if (string.IsNullOrEmpty(userIdStr) || !long.TryParse(userIdStr, out long userId) || string.IsNullOrEmpty(role))
            {
                return Unauthorized(new { Message = "Invalid user claims." });
            }

            try
            {
                var profile = await _authService.GetProfileAsync(userId, role);
                return Ok(profile);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
        }
    }
}
