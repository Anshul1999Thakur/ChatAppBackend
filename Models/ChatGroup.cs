namespace ChatApp.Models
{
    public class ChatGroup
    {
        public int Id { get; set; }

        public string GroupName { get; set; }

        public string CreatedBy { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
