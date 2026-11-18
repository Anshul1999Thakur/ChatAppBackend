using ChatApp.Data;
using ChatApp.Models;
using MongoDB.Driver;

namespace ChatApp.Repositories
{
    public class MessageRepository : IMessageRepository
    {
        private readonly MongoDbContext _context;

        public MessageRepository(MongoDbContext context)
        {
            _context = context;
        }

        public async Task CreateAsync(Message message)
        {
            await _context.Messages.InsertOneAsync(message);
        }

        public async Task<Message?> GetByIdAsync(string id)
        {
            var now = DateTime.UtcNow;
            return await _context.Messages.Find(m => m.Id == id && (m.ExpiresAt == null || m.ExpiresAt > now)).FirstOrDefaultAsync();
        }

        public async Task UpdateAsync(Message message)
        {
            await _context.Messages.ReplaceOneAsync(m => m.Id == message.Id, message);
        }

        public async Task<List<Message>> GetConversationHistoryAsync(string user1, string user2, int page, int pageSize, DateTime? clearedAfter = null)
        {
            var now = DateTime.UtcNow;

            var userFilter = Builders<Message>.Filter.Or(
                Builders<Message>.Filter.And(
                    Builders<Message>.Filter.Eq(m => m.SenderId, user1),
                    Builders<Message>.Filter.Eq(m => m.ReceiverId, user2)
                ),
                Builders<Message>.Filter.And(
                    Builders<Message>.Filter.Eq(m => m.SenderId, user2),
                    Builders<Message>.Filter.Eq(m => m.ReceiverId, user1)
                )
            );

            var notExpiredFilter = Builders<Message>.Filter.Or(
                Builders<Message>.Filter.Eq(m => m.ExpiresAt, null),
                Builders<Message>.Filter.Gt(m => m.ExpiresAt, now)
            );

            var combinedFilter = Builders<Message>.Filter.And(userFilter, notExpiredFilter);

            if (clearedAfter.HasValue)
            {
                var clearedFilter = Builders<Message>.Filter.Gt(m => m.CreatedAt, clearedAfter.Value);
                combinedFilter = Builders<Message>.Filter.And(combinedFilter, clearedFilter);
            }

            var list = await _context.Messages
                .Find(combinedFilter)
                .SortByDescending(m => m.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Limit(pageSize)
                .ToListAsync();

            list.Reverse();
            return list;
        }

        public async Task<Message?> GetLastMessageBetweenUsersAsync(string user1, string user2, DateTime? clearedAfter = null)
        {
            var now = DateTime.UtcNow;

            var userFilter = Builders<Message>.Filter.Or(
                Builders<Message>.Filter.And(
                    Builders<Message>.Filter.Eq(m => m.SenderId, user1),
                    Builders<Message>.Filter.Eq(m => m.ReceiverId, user2)
                ),
                Builders<Message>.Filter.And(
                    Builders<Message>.Filter.Eq(m => m.SenderId, user2),
                    Builders<Message>.Filter.Eq(m => m.ReceiverId, user1)
                )
            );

            var notExpiredFilter = Builders<Message>.Filter.Or(
                Builders<Message>.Filter.Eq(m => m.ExpiresAt, null),
                Builders<Message>.Filter.Gt(m => m.ExpiresAt, now)
            );

            var combinedFilter = Builders<Message>.Filter.And(userFilter, notExpiredFilter);

            if (clearedAfter.HasValue)
            {
                var clearedFilter = Builders<Message>.Filter.Gt(m => m.CreatedAt, clearedAfter.Value);
                combinedFilter = Builders<Message>.Filter.And(combinedFilter, clearedFilter);
            }

            return await _context.Messages
                .Find(combinedFilter)
                .SortByDescending(m => m.CreatedAt)
                .FirstOrDefaultAsync();
        }

        public async Task<long> GetUnreadCountAsync(string senderId, string receiverId, DateTime? clearedAfter = null)
        {
            var now = DateTime.UtcNow;
            var filter = Builders<Message>.Filter.And(
                Builders<Message>.Filter.Eq(m => m.SenderId, senderId),
                Builders<Message>.Filter.Eq(m => m.ReceiverId, receiverId),
                Builders<Message>.Filter.Eq(m => m.IsSeen, false),
                Builders<Message>.Filter.Or(
                    Builders<Message>.Filter.Eq(m => m.ExpiresAt, null),
                    Builders<Message>.Filter.Gt(m => m.ExpiresAt, now)
                )
            );

            if (clearedAfter.HasValue)
            {
                var clearedFilter = Builders<Message>.Filter.Gt(m => m.CreatedAt, clearedAfter.Value);
                filter = Builders<Message>.Filter.And(filter, clearedFilter);
            }

            return await _context.Messages.CountDocumentsAsync(filter);
        }

        public async Task MarkMessagesAsSeenAsync(string senderId, string receiverId)
        {
            var update = Builders<Message>.Update.Set(m => m.IsSeen, true);
            await _context.Messages.UpdateManyAsync(m =>
                m.SenderId == senderId &&
                m.ReceiverId == receiverId &&
                m.IsSeen == false,
                update);
        }

        public async Task MarkMessagesAsDeliveredAsync(string receiverId)
        {
            var update = Builders<Message>.Update.Set(m => m.IsDelivered, true);
            await _context.Messages.UpdateManyAsync(m =>
                m.ReceiverId == receiverId &&
                m.IsDelivered == false,
                update);
        }

        public async Task<List<Message>> GetPendingDeliveredMessagesAsync(string receiverId)
        {
            var now = DateTime.UtcNow;
            return await _context.Messages.Find(m =>
                m.ReceiverId == receiverId &&
                m.IsDelivered == false &&
                (m.ExpiresAt == null || m.ExpiresAt > now)).ToListAsync();
        }

        public async Task ClearMessagesAsync(string user1, string user2)
        {
            var filter = Builders<Message>.Filter.Or(
                Builders<Message>.Filter.And(
                    Builders<Message>.Filter.Eq(m => m.SenderId, user1),
                    Builders<Message>.Filter.Eq(m => m.ReceiverId, user2)
                ),
                Builders<Message>.Filter.And(
                    Builders<Message>.Filter.Eq(m => m.SenderId, user2),
                    Builders<Message>.Filter.Eq(m => m.ReceiverId, user1)
                )
            );

            await _context.Messages.DeleteManyAsync(filter);
        }
    }
}
