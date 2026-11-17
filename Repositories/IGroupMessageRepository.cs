using ChatApp.Models;

namespace ChatApp.Repositories
{
    public interface IGroupMessageRepository
    {
        Task CreateAsync(GroupMessage message);
        Task<GroupMessage?> GetByIdAsync(string id);
        Task UpdateAsync(GroupMessage message);
        Task<List<GroupMessage>> GetGroupHistoryAsync(string groupId, int page, int pageSize);
        Task<GroupMessage?> GetLastMessageByGroupIdAsync(string groupId);
        Task DeleteAllByGroupIdAsync(string groupId);
    }
}
