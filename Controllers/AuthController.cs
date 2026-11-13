using ChatApp.Common;
using ChatApp.DTOs;
using ChatApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChatApp.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            var ip = GetClientIp();
            var result = await _authService.RegisterAsync(dto, ip);

            // Backward compatibility with frontend expecting token and string
            return Ok(new
            {
                success = true,
                message = "Registered Successfully",
                token = result.Token,
                refreshToken = result.RefreshToken,
                userId = result.UserId,
                name = result.FullName,
                data = result
            });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            var ip = GetClientIp();
            var result = await _authService.LoginAsync(dto, ip);

            // Returns format expected by frontend while providing all rich data
            return Ok(new
            {
                success = true,
                message = "Login successful",
                token = result.Token,
                refreshToken = result.RefreshToken,
                userId = result.UserId,
                name = result.FullName,
                data = result
            });
        }

        [HttpPost("refresh-token")]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequestDto dto)
        {
            var ip = GetClientIp();
            var result = await _authService.RefreshTokenAsync(dto.RefreshToken, ip);
            return Ok(ApiResponse<AuthResponseDto>.Ok(result, "Token refreshed successfully"));
        }

        [Authorize]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout([FromBody] RefreshTokenRequestDto? dto)
        {
            var userId = User.GetUserId();
            var ip = GetClientIp();
            await _authService.LogoutAsync(userId, dto?.RefreshToken, ip);
            return Ok(ApiResponse.Ok("Logged out successfully"));
        }

        private string? GetClientIp()
        {
            return HttpContext.Connection.RemoteIpAddress?.ToString();
        }
    }
}
