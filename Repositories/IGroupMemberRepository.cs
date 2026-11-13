using ChatApp.Models;

namespace ChatApp.Repositories
{
    public interface IGroupMemberRepository
    {
        Task AddMemberAsync(GroupMember member);
        Task AddMembersAsync(IEnumerable<GroupMember> members);
        Task<GroupMember?> GetMembershipAsync(string groupId, string userId);
        Task<List<GroupMember>> GetMembersByGroupIdAsync(string groupId);
        Task<List<GroupMember>> GetMembershipsByUserIdAsync(string userId);
        Task<int> CountMembersAsync(string groupId);
        Task RemoveMemberAsync(string groupId, string userId);
        Task UpdateRoleAsync(string groupId, string userId, string newRole);
        Task DeleteAllMembersByGroupIdAsync(string groupId);
    }
}

