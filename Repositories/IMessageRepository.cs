using ChatApp.Models;

namespace ChatApp.Repositories
{
    public interface IMessageRepository
    {
        Task CreateAsync(Message message);
        Task<Message?> GetByIdAsync(string id);
        Task<List<Message>> GetConversationHistoryAsync(string user1, string user2, int page, int pageSize);
        Task<Message?> GetLastMessageBetweenUsersAsync(string user1, string user2);
        Task<long> GetUnreadCountAsync(string senderId, string receiverId);
        Task MarkMessagesAsSeenAsync(string senderId, string receiverId);
        Task MarkMessagesAsDeliveredAsync(string receiverId);
        Task<List<Message>> GetPendingDeliveredMessagesAsync(string receiverId);
    }
}

