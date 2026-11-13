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
            return await _context.Messages.Find(m => m.Id == id).FirstOrDefaultAsync();
        }

        public async Task<List<Message>> GetConversationHistoryAsync(string user1, string user2, int page, int pageSize)
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

            var list = await _context.Messages
                .Find(filter)
                .SortByDescending(m => m.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Limit(pageSize)
                .ToListAsync();

            list.Reverse();
            return list;
        }

        public async Task<Message?> GetLastMessageBetweenUsersAsync(string user1, string user2)
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

            return await _context.Messages
                .Find(filter)
                .SortByDescending(m => m.CreatedAt)
                .FirstOrDefaultAsync();
        }

        public async Task<long> GetUnreadCountAsync(string senderId, string receiverId)
        {
            return await _context.Messages.CountDocumentsAsync(m =>
                m.SenderId == senderId &&
                m.ReceiverId == receiverId &&
                m.IsSeen == false);
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
            return await _context.Messages.Find(m =>
                m.ReceiverId == receiverId &&
                m.IsDelivered == false).ToListAsync();
        }
    }
}

