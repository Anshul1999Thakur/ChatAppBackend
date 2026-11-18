using ChatApp.Models;

namespace ChatApp.Repositories
{
    public interface IMessageRepository
    {
        Task CreateAsync(Message message);
        Task<Message?> GetByIdAsync(string id);
        Task UpdateAsync(Message message);
        Task<List<Message>> GetConversationHistoryAsync(string user1, string user2, int page, int pageSize, DateTime? clearedAfter = null);
        Task<Message?> GetLastMessageBetweenUsersAsync(string user1, string user2, DateTime? clearedAfter = null);
        Task<long> GetUnreadCountAsync(string senderId, string receiverId, DateTime? clearedAfter = null);
        Task MarkMessagesAsSeenAsync(string senderId, string receiverId);
        Task MarkMessagesAsDeliveredAsync(string receiverId);
        Task<List<Message>> GetPendingDeliveredMessagesAsync(string receiverId);
        Task ClearMessagesAsync(string user1, string user2);
    }
}
