using ChatApp.DTOs;

namespace ChatApp.Services
{
    public interface IMessageService
    {
        Task<MessageResponseDto> SendMessageAsync(string senderId, string receiverId, string text);
        Task<MessageResponseDto> SendStickerAsync(string senderId, string receiverId, string stickerUrl);
        Task<List<MessageResponseDto>> GetConversationHistoryAsync(string currentUserId, string otherUserId, int page, int pageSize);
        Task MarkSeenAsync(string currentUserId, string senderId);
    }
}

