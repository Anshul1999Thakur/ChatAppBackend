using ChatApp.DTOs;

namespace ChatApp.Services
{
    public interface IGroupService
    {
        Task<GroupSummaryDto> CreateGroupAsync(string creatorId, CreateGroupDto dto);
        Task<List<GroupSummaryDto>> GetUserGroupsAsync(string userId);
        Task<GroupDetailDto> GetGroupDetailsAsync(string groupId, string currentUserId);
        Task<GroupDetailDto> UpdateGroupAsync(string groupId, string currentUserId, UpdateGroupDto dto);
        Task DeleteGroupAsync(string groupId, string currentUserId);
        Task<List<GroupMemberDetailDto>> AddMembersAsync(string groupId, string currentUserId, AddGroupMembersDto dto);
        Task RemoveMemberAsync(string groupId, string currentUserId, string targetUserId);
        Task LeaveGroupAsync(string groupId, string currentUserId);
        Task<GroupMemberDetailDto> UpdateMemberRoleAsync(string groupId, string currentUserId, string targetUserId, string newRole);
        Task TransferOwnershipAsync(string groupId, string currentUserId, string newOwnerId);
        Task<string> UploadGroupAvatarAsync(string? groupId, string currentUserId, IFormFile file);
    }
}

