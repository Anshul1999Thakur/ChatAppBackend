using ChatApp.DTOs;

namespace ChatApp.Services
{
    public interface IAuthService
    {
        Task<AuthResponseDto> RegisterAsync(RegisterDto dto, string? ipAddress);
        Task<AuthResponseDto> LoginAsync(LoginDto dto, string? ipAddress);
        Task<AuthResponseDto> RefreshTokenAsync(string refreshToken, string? ipAddress);
        Task LogoutAsync(string userId, string? refreshToken, string? ipAddress);
    }
}

