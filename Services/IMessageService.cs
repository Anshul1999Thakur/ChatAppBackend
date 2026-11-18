using ChatApp.DTOs;

namespace ChatApp.Services
{
    public interface IMessageService
    {
        Task<MessageResponseDto> SendMessageAsync(string senderId, string receiverId, string text);
        Task<MessageResponseDto> SendStickerAsync(string senderId, string receiverId, string stickerUrl);
        Task<MessageResponseDto> EditMessageAsync(string messageId, string currentUserId, string newText);
        Task<List<MessageResponseDto>> GetConversationHistoryAsync(string currentUserId, string otherUserId, int page, int pageSize);
        Task MarkSeenAsync(string currentUserId, string senderId);
        Task ClearChatAsync(string currentUserId, string otherUserId, bool clearForEveryone);
        Task<ConversationSettingDto> GetChatSettingsAsync(string currentUserId, string otherUserId);
        Task<ConversationSettingDto> SetDisappearingMessagesAsync(string currentUserId, string otherUserId, int hours);
    }
}
