using ChatApp.Data;
using ChatApp.Models;
using MongoDB.Driver;

namespace ChatApp.Repositories
{
    public class ConversationSettingRepository : IConversationSettingRepository
    {
        private readonly MongoDbContext _context;

        public ConversationSettingRepository(MongoDbContext context)
        {
            _context = context;
        }

        public async Task<ConversationSetting?> GetSettingAsync(string user1, string user2)
        {
            var (u1, u2) = ConversationSetting.NormalizeUsers(user1, user2);
            return await _context.ConversationSettings
                .Find(s => s.User1Id == u1 && s.User2Id == u2)
                .FirstOrDefaultAsync();
        }

        public async Task<ConversationSetting> GetOrCreateSettingAsync(string user1, string user2)
        {
            var (u1, u2) = ConversationSetting.NormalizeUsers(user1, user2);
            var existing = await _context.ConversationSettings
                .Find(s => s.User1Id == u1 && s.User2Id == u2)
                .FirstOrDefaultAsync();

            if (existing != null) return existing;

            var setting = new ConversationSetting
            {
                User1Id = u1,
                User2Id = u2,
                AutoDeleteDurationHours = 0,
                UpdatedAt = DateTime.UtcNow
            };

            try
            {
                await _context.ConversationSettings.InsertOneAsync(setting);
                return setting;
            }
            catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
            {
                return (await GetSettingAsync(user1, user2))!;
            }
        }

        public async Task UpdateAutoDeleteDurationAsync(string user1, string user2, int hours, string setBy)
        {
            var setting = await GetOrCreateSettingAsync(user1, user2);
            var update = Builders<ConversationSetting>.Update
                .Set(s => s.AutoDeleteDurationHours, hours)
                .Set(s => s.AutoDeleteSetBy, setBy)
                .Set(s => s.AutoDeleteSetAt, DateTime.UtcNow)
                .Set(s => s.UpdatedAt, DateTime.UtcNow);

            await _context.ConversationSettings.UpdateOneAsync(s => s.Id == setting.Id, update);
        }

        public async Task SetUserClearedAtAsync(string user1, string user2, string userId, DateTime clearedAt)
        {
            var setting = await GetOrCreateSettingAsync(user1, user2);
            var update = Builders<ConversationSetting>.Update
                .Set($"ClearedAt.{userId}", clearedAt)
                .Set(s => s.UpdatedAt, DateTime.UtcNow);

            await _context.ConversationSettings.UpdateOneAsync(s => s.Id == setting.Id, update);
        }
    }
}
