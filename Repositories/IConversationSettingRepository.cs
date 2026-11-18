using ChatApp.Models;

namespace ChatApp.Repositories
{
    public interface IConversationSettingRepository
    {
        Task<ConversationSetting?> GetSettingAsync(string user1, string user2);
        Task<ConversationSetting> GetOrCreateSettingAsync(string user1, string user2);
        Task UpdateAutoDeleteDurationAsync(string user1, string user2, int hours, string setBy);
        Task SetUserClearedAtAsync(string user1, string user2, string userId, DateTime clearedAt);
    }
}
