using ChatApp.Models;

namespace ChatApp.Repositories
{
    public interface IRefreshTokenRepository
    {
        Task CreateAsync(RefreshToken token);
        Task<RefreshToken?> GetByTokenAsync(string token);
        Task RevokeTokenAsync(string token, string? revokedByIp = null, string? replacedByToken = null);
        Task RevokeAllUserTokensAsync(string userId, string? revokedByIp = null);
    }
}

