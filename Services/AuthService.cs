using ChatApp.Common;
using ChatApp.DTOs;
using ChatApp.Helpers;
using ChatApp.Models;
using ChatApp.Repositories;

namespace ChatApp.Services
{
    public class AuthService : IAuthService
    {
        private readonly IUserRepository _userRepo;
        private readonly IRefreshTokenRepository _refreshRepo;
        private readonly IConfiguration _config;
        private readonly ILogger<AuthService> _logger;

        public AuthService(
            IUserRepository userRepo,
            IRefreshTokenRepository refreshRepo,
            IConfiguration config,
            ILogger<AuthService> logger)
        {
            _userRepo = userRepo;
            _refreshRepo = refreshRepo;
            _config = config;
            _logger = logger;
        }

        public async Task<AuthResponseDto> RegisterAsync(RegisterDto dto, string? ipAddress)
        {
            if (string.IsNullOrWhiteSpace(dto.FullName))
                throw new BadRequestException("Full name cannot be empty or whitespace");

            if (string.IsNullOrWhiteSpace(dto.Email))
                throw new BadRequestException("Email cannot be empty");

            if (string.IsNullOrWhiteSpace(dto.Password) || dto.Password.Length < 6)
                throw new BadRequestException("Password must be at least 6 characters");

            var normalizedEmail = dto.Email.Trim().ToLowerInvariant();

            var existingUser = await _userRepo.GetByEmailAsync(normalizedEmail);
            if (existingUser != null)
                throw new BadRequestException("A user with this email address already exists");

            var user = new User
            {
                FullName = dto.FullName.Trim(),
                Email = normalizedEmail,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                IsOnline = false,
                CreatedAt = DateTime.UtcNow
            };

            await _userRepo.CreateAsync(user);
            _logger.LogInformation("New user registered: {UserId} ({Email})", user.Id, user.Email);

            return await GenerateAuthResponseAsync(user, ipAddress);
        }

        public async Task<AuthResponseDto> LoginAsync(LoginDto dto, string? ipAddress)
        {
            if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Password))
                throw new BadRequestException("Email and password are required");

            var normalizedEmail = dto.Email.Trim().ToLowerInvariant();

            var user = await _userRepo.GetByEmailAsync(normalizedEmail);
            if (user == null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
                throw new BadRequestException("Invalid email or password");

            _logger.LogInformation("User logged in: {UserId} from {Ip}", user.Id, ipAddress);
            return await GenerateAuthResponseAsync(user, ipAddress);
        }

        public async Task<AuthResponseDto> RefreshTokenAsync(string refreshToken, string? ipAddress)
        {
            if (string.IsNullOrWhiteSpace(refreshToken))
                throw new BadRequestException("Refresh token is required");

            var tokenRecord = await _refreshRepo.GetByTokenAsync(refreshToken);
            if (tokenRecord == null || !tokenRecord.IsActive)
                throw new UnauthorizedException("Invalid or expired refresh token");

            var user = await _userRepo.GetByIdAsync(tokenRecord.UserId);
            if (user == null)
                throw new UnauthorizedException("User associated with this token no longer exists");

            // Rotate refresh token
            var newRefreshTokenString = JwtHelper.GenerateRefreshToken();
            await _refreshRepo.RevokeTokenAsync(tokenRecord.Token, ipAddress, newRefreshTokenString);

            var newRefreshToken = new RefreshToken
            {
                UserId = user.Id,
                Token = newRefreshTokenString,
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                CreatedAt = DateTime.UtcNow,
                CreatedByIp = ipAddress
            };
            await _refreshRepo.CreateAsync(newRefreshToken);

            var jwtKey = _config["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key is not configured");
            var expiresAt = DateTime.UtcNow.AddHours(2);
            var jwt = JwtHelper.GenerateToken(user.Id, user.FullName, user.Email, jwtKey, TimeSpan.FromHours(2));

            return new AuthResponseDto
            {
                Token = jwt,
                RefreshToken = newRefreshTokenString,
                ExpiresAt = expiresAt,
                UserId = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                ProfilePictureUrl = user.ProfilePictureUrl
            };
        }

        public async Task LogoutAsync(string userId, string? refreshToken, string? ipAddress)
        {
            if (!string.IsNullOrWhiteSpace(refreshToken))
            {
                await _refreshRepo.RevokeTokenAsync(refreshToken, ipAddress);
            }
            else
            {
                await _refreshRepo.RevokeAllUserTokensAsync(userId, ipAddress);
            }

            await _userRepo.UpdateOnlineStatusAsync(userId, false);
            _logger.LogInformation("User logged out: {UserId}", userId);
        }

        private async Task<AuthResponseDto> GenerateAuthResponseAsync(User user, string? ipAddress)
        {
            var jwtKey = _config["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key is not configured");
            var expiresAt = DateTime.UtcNow.AddHours(2);
            var jwt = JwtHelper.GenerateToken(user.Id, user.FullName, user.Email, jwtKey, TimeSpan.FromHours(2));

            var refreshToken = new RefreshToken
            {
                UserId = user.Id,
                Token = JwtHelper.GenerateRefreshToken(),
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                CreatedAt = DateTime.UtcNow,
                CreatedByIp = ipAddress
            };
            await _refreshRepo.CreateAsync(refreshToken);

            return new AuthResponseDto
            {
                Token = jwt,
                RefreshToken = refreshToken.Token,
                ExpiresAt = expiresAt,
                UserId = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                ProfilePictureUrl = user.ProfilePictureUrl
            };
        }
    }
}

