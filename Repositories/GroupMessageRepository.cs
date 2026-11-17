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
            return await _context.GroupMessages.Find(m => m.Id == id).FirstOrDefaultAsync();
        }

        public async Task UpdateAsync(GroupMessage message)
        {
            await _context.GroupMessages.ReplaceOneAsync(m => m.Id == message.Id, message);
        }

        public async Task<List<GroupMessage>> GetGroupHistoryAsync(string groupId, int page, int pageSize)
        {
            var list = await _context.GroupMessages
                .Find(m => m.GroupId == groupId)
                .SortByDescending(m => m.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Limit(pageSize)
                .ToListAsync();

            list.Reverse();
            return list;
        }

        public async Task<GroupMessage?> GetLastMessageByGroupIdAsync(string groupId)
        {
            return await _context.GroupMessages
                .Find(m => m.GroupId == groupId)
                .SortByDescending(m => m.CreatedAt)
                .FirstOrDefaultAsync();
        }

        public async Task DeleteAllByGroupIdAsync(string groupId)
        {
            await _context.GroupMessages.DeleteManyAsync(m => m.GroupId == groupId);
        }
    }
}
