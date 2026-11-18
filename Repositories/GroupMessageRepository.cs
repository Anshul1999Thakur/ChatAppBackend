using ChatApp.Data;
using ChatApp.Models;
using MongoDB.Driver;

namespace ChatApp.Repositories
{
    public class GroupMessageRepository : IGroupMessageRepository
    {
        private readonly MongoDbContext _context;

        public GroupMessageRepository(MongoDbContext context)
        {
            _context = context;
        }

        public async Task CreateAsync(GroupMessage message)
        {
            await _context.GroupMessages.InsertOneAsync(message);
        }

        public async Task<GroupMessage?> GetByIdAsync(string id)
        {
            var now = DateTime.UtcNow;
            return await _context.GroupMessages
                .Find(m => m.Id == id && (m.ExpiresAt == null || m.ExpiresAt > now))
                .FirstOrDefaultAsync();
        }

        public async Task UpdateAsync(GroupMessage message)
        {
            await _context.GroupMessages.ReplaceOneAsync(m => m.Id == message.Id, message);
        }

        public async Task<List<GroupMessage>> GetGroupHistoryAsync(string groupId, int page, int pageSize, DateTime? clearedAfter = null)
        {
            var now = DateTime.UtcNow;
            var builder = Builders<GroupMessage>.Filter;
            var filter = builder.Eq(m => m.GroupId, groupId)
                       & (builder.Eq(m => m.ExpiresAt, null) | builder.Gt(m => m.ExpiresAt, now));

            if (clearedAfter.HasValue)
            {
                filter &= builder.Gt(m => m.CreatedAt, clearedAfter.Value);
            }

            var list = await _context.GroupMessages
                .Find(filter)
                .SortByDescending(m => m.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Limit(pageSize)
                .ToListAsync();

            list.Reverse();
            return list;
        }

        public async Task<GroupMessage?> GetLastMessageByGroupIdAsync(string groupId, DateTime? clearedAfter = null)
        {
            var now = DateTime.UtcNow;
            var builder = Builders<GroupMessage>.Filter;
            var filter = builder.Eq(m => m.GroupId, groupId)
                       & (builder.Eq(m => m.ExpiresAt, null) | builder.Gt(m => m.ExpiresAt, now));

            if (clearedAfter.HasValue)
            {
                filter &= builder.Gt(m => m.CreatedAt, clearedAfter.Value);
            }

            return await _context.GroupMessages
                .Find(filter)
                .SortByDescending(m => m.CreatedAt)
                .FirstOrDefaultAsync();
        }

        public async Task DeleteAllByGroupIdAsync(string groupId)
        {
            await _context.GroupMessages.DeleteManyAsync(m => m.GroupId == groupId);
        }
    }
}
