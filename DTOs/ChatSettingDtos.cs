using System.ComponentModel.DataAnnotations;

namespace ChatApp.DTOs
{
    public class ClearChatRequestDto
    {
        public bool ClearForEveryone { get; set; } = false;
    }

    public class SetDisappearingMessagesDto
    {
        [Range(0, 8760, ErrorMessage = "Duration must be between 0 (Off) and 8760 hours (1 year)")]
        public int AutoDeleteDurationHours { get; set; } = 0;
    }

    public class ConversationSettingDto
    {
        public int AutoDeleteDurationHours { get; set; } = 0;
        public string? AutoDeleteSetBy { get; set; }
        public DateTime? AutoDeleteSetAt { get; set; }
    }
}
