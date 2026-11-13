using ChatApp.Data;
using ChatApp.Models;
using MongoDB.Driver;

namespace ChatApp.Repositories
{
    public class RefreshTokenRepository : IRefreshTokenRepository
    {
        private readonly MongoDbContext _context;

        public RefreshTokenRepository(MongoDbContext context)
        {
            _context = context;
        }

        public async Task CreateAsync(RefreshToken token)
        {
            await _context.RefreshTokens.InsertOneAsync(token);
        }

        public async Task<RefreshToken?> GetByTokenAsync(string token)
        {
            return await _context.RefreshTokens.Find(t => t.Token == token).FirstOrDefaultAsync();
        }

        public async Task RevokeTokenAsync(string token, string? revokedByIp = null, string? replacedByToken = null)
        {
            var update = Builders<RefreshToken>.Update
                .Set(t => t.RevokedAt, DateTime.UtcNow)
                .Set(t => t.RevokedByIp, revokedByIp)
                .Set(t => t.ReplacedByToken, replacedByToken);

            await _context.RefreshTokens.UpdateOneAsync(t => t.Token == token, update);
        }

        public async Task RevokeAllUserTokensAsync(string userId, string? revokedByIp = null)
        {
            var update = Builders<RefreshToken>.Update
                .Set(t => t.RevokedAt, DateTime.UtcNow)
                .Set(t => t.RevokedByIp, revokedByIp);

            await _context.RefreshTokens.UpdateManyAsync(t => t.UserId == userId && t.RevokedAt == null, update);
        }
    }
}

