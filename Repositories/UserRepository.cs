using ChatApp.Data;
using ChatApp.Models;
using MongoDB.Driver;

namespace ChatApp.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly MongoDbContext _context;

        public UserRepository(MongoDbContext context)
        {
            _context = context;
        }

        public async Task<User?> GetByIdAsync(string id)
        {
            return await _context.Users.Find(u => u.Id == id).FirstOrDefaultAsync();
        }

        public async Task<User?> GetByEmailAsync(string email)
        {
            return await _context.Users.Find(u => u.Email.ToLower() == email.Trim().ToLower()).FirstOrDefaultAsync();
        }

        public async Task<List<User>> GetByIdsAsync(IEnumerable<string> ids)
        {
            var idList = ids.Distinct().ToList();
            if (idList.Count == 0) return new List<User>();

            return await _context.Users.Find(u => idList.Contains(u.Id)).ToListAsync();
        }

        public async Task<List<User>> GetAllExceptAsync(string currentUserId)
        {
            return await _context.Users.Find(u => u.Id != currentUserId).ToListAsync();
        }

        public async Task CreateAsync(User user)
        {
            await _context.Users.InsertOneAsync(user);
        }

        public async Task UpdateAsync(User user)
        {
            user.UpdatedAt = DateTime.UtcNow;
            await _context.Users.ReplaceOneAsync(u => u.Id == user.Id, user);
        }

        public async Task UpdateOnlineStatusAsync(string userId, bool isOnline)
        {
            var update = Builders<User>.Update
                .Set(u => u.IsOnline, isOnline)
                .Set(u => u.LastSeen, DateTime.UtcNow);

            await _context.Users.UpdateOneAsync(u => u.Id == userId, update);
        }
    }
}

