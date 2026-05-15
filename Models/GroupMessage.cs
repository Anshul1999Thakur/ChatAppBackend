namespace ChatApp.Models
{
    public class GroupMessage
    {
        public int Id { get; set; }

        public int GroupId { get; set; }

        public int SenderId { get; set; }

        public string Message { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
