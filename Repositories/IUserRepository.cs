using ChatApp.Models;

namespace ChatApp.Repositories
{
    public interface IUserRepository
    {
        Task<User?> GetByIdAsync(string id);
        Task<User?> GetByEmailAsync(string email);
        Task<List<User>> GetByIdsAsync(IEnumerable<string> ids);
        Task<List<User>> GetAllExceptAsync(string currentUserId);
        Task CreateAsync(User user);
        Task UpdateAsync(User user);
        Task UpdateOnlineStatusAsync(string userId, bool isOnline);
    }
}

