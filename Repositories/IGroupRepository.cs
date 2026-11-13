using ChatApp.Models;

namespace ChatApp.Repositories
{
    public interface IGroupRepository
    {
        Task CreateAsync(ChatGroup group);
        Task<ChatGroup?> GetByIdAsync(string id);
        Task<List<ChatGroup>> GetByIdsAsync(IEnumerable<string> ids);
        Task<bool> ExistsByNameForUserAsync(string name, string createdBy);
        Task UpdateAsync(ChatGroup group);
        Task SoftDeleteAsync(string id);
    }
}

