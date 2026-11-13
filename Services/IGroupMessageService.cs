using ChatApp.DTOs;

namespace ChatApp.Services
{
    public interface IGroupMessageService
    {
        Task<GroupMessageDto> SendGroupMessageAsync(string groupId, string senderId, string text);
        Task<GroupMessageDto> SendGroupStickerAsync(string groupId, string senderId, string stickerUrl);
        Task<List<GroupMessageDto>> GetGroupMessagesAsync(string groupId, string currentUserId, int page, int pageSize);
    }
}

