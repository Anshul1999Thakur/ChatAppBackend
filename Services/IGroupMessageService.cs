using ChatApp.DTOs;

namespace ChatApp.Services
{
    public interface IGroupMessageService
    {
        Task<GroupMessageDto> SendGroupMessageAsync(string groupId, string senderId, string text);
        Task<GroupMessageDto> SendGroupStickerAsync(string groupId, string senderId, string stickerUrl);
        Task<GroupMessageDto> EditGroupMessageAsync(string groupId, string messageId, string currentUserId, string newText);
        Task<List<GroupMessageDto>> GetGroupMessagesAsync(string groupId, string currentUserId, int page, int pageSize);
        Task ClearGroupChatAsync(string groupId, string currentUserId, bool clearForEveryone);
        Task<ConversationSettingDto> SetGroupDisappearingMessagesAsync(string groupId, string currentUserId, int hours);
        Task<ConversationSettingDto> GetGroupDisappearingMessagesAsync(string groupId, string currentUserId);
    }
}
