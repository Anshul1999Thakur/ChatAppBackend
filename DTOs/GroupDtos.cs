using System.ComponentModel.DataAnnotations;

namespace ChatApp.DTOs
{
    public class CreateGroupDto
    {
        [Required(ErrorMessage = "Group name is required")]
        [StringLength(50, MinimumLength = 3, ErrorMessage = "Group name must be between 3 and 50 characters")]
        public string Name { get; set; } = string.Empty;

        [StringLength(250, ErrorMessage = "Description cannot exceed 250 characters")]
        public string? Description { get; set; }

        public string? ImageUrl { get; set; }

        public List<string> MemberIds { get; set; } = new List<string>();
    }

    public class UpdateGroupDto
    {
        [Required(ErrorMessage = "Group name is required")]
        [StringLength(50, MinimumLength = 3, ErrorMessage = "Group name must be between 3 and 50 characters")]
        public string Name { get; set; } = string.Empty;

        [StringLength(250, ErrorMessage = "Description cannot exceed 250 characters")]
        public string? Description { get; set; }

        public string? ImageUrl { get; set; }
    }

    public class AddGroupMembersDto
    {
        [Required(ErrorMessage = "Member IDs are required")]
        [MinLength(1, ErrorMessage = "At least one member ID must be provided")]
        public List<string> MemberIds { get; set; } = new List<string>();
    }

    public class UpdateMemberRoleDto
    {
        [Required(ErrorMessage = "Role is required")]
        public string Role { get; set; } = string.Empty;
    }

    public class TransferOwnershipDto
    {
        [Required(ErrorMessage = "New owner ID is required")]
        public string NewOwnerId { get; set; } = string.Empty;
    }

    public class SendGroupMessageDto
    {
        [Required(ErrorMessage = "Message text is required")]
        [StringLength(2000, MinimumLength = 1, ErrorMessage = "Message must be between 1 and 2000 characters")]
        public string Text { get; set; } = string.Empty;
    }

    public class EditGroupMessageDto
    {
        [Required(ErrorMessage = "New message text is required")]
        [StringLength(2000, MinimumLength = 1, ErrorMessage = "Message must be between 1 and 2000 characters")]
        public string NewText { get; set; } = string.Empty;
    }

    public class SendGroupStickerDto
    {
        [Required(ErrorMessage = "Sticker URL is required")]
        public string StickerUrl { get; set; } = string.Empty;
    }

    public class GroupSummaryDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? ImageUrl { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string CurrentUserRole { get; set; } = string.Empty;
        public int MemberCount { get; set; }
        public string? LastMessage { get; set; }
        public DateTime? LastMessageTime { get; set; }
        public int UnreadCount { get; set; }
    }

    public class GroupDetailDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? ImageUrl { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string CurrentUserRole { get; set; } = string.Empty;
        public List<GroupMemberDetailDto> Members { get; set; } = new List<GroupMemberDetailDto>();
    }

    public class GroupMemberDetailDto
    {
        public string UserId { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? ProfilePictureUrl { get; set; }
        public string Role { get; set; } = string.Empty;
        public bool IsOnline { get; set; }
        public DateTime JoinedAt { get; set; }
    }

    public class GroupMessageDto
    {
        public string Id { get; set; } = string.Empty;
        public string GroupId { get; set; } = string.Empty;
        public string SenderId { get; set; } = string.Empty;
        public string SenderName { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public string? StickerUrl { get; set; }
        public bool IsSticker { get; set; }
        public bool IsSystemMessage { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsEdited { get; set; }
        public DateTime? EditedAt { get; set; }
    }
}
