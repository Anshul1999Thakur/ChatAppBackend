using System.ComponentModel.DataAnnotations;

namespace ChatApp.DTOs
{
    public class SendMessageRequestDto
    {
        [Required(ErrorMessage = "Receiver ID is required")]
        public string ReceiverId { get; set; } = string.Empty;

        [Required(ErrorMessage = "Message text is required")]
        [StringLength(2000, MinimumLength = 1, ErrorMessage = "Message must be between 1 and 2000 characters")]
        public string Text { get; set; } = string.Empty;
    }

    public class EditMessageRequestDto
    {
        [Required(ErrorMessage = "New message text is required")]
        [StringLength(2000, MinimumLength = 1, ErrorMessage = "Message must be between 1 and 2000 characters")]
        public string NewText { get; set; } = string.Empty;
    }

    public class SendStickerRequestDto
    {
        [Required(ErrorMessage = "Receiver ID is required")]
        public string ReceiverId { get; set; } = string.Empty;

        [Required(ErrorMessage = "Sticker URL is required")]
        public string StickerUrl { get; set; } = string.Empty;
    }

    public class MessageResponseDto
    {
        public string Id { get; set; } = string.Empty;
        public string SenderId { get; set; } = string.Empty;
        public string ReceiverId { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public string? StickerUrl { get; set; }
        public bool IsSticker { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsSeen { get; set; }
        public bool IsDelivered { get; set; }
        public bool IsEdited { get; set; }
        public DateTime? EditedAt { get; set; }
    }

    public class UserContactDto
    {
        public string Id { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? ProfilePictureUrl { get; set; }
        public bool IsOnline { get; set; }
        public DateTime? LastSeen { get; set; }
        public long UnreadCount { get; set; }
        public string LastMessage { get; set; } = string.Empty;
        public DateTime? LastMessageTime { get; set; }
    }
}
