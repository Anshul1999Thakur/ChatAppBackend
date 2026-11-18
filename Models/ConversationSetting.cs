using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ChatApp.Models
{
    public class ConversationSetting
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        public string User1Id { get; set; } = string.Empty;

        public string User2Id { get; set; } = string.Empty;

        public int AutoDeleteDurationHours { get; set; } = 0; // 0 = off, 24 = 24h, 168 = 7d, 2160 = 90d

        public string? AutoDeleteSetBy { get; set; }

        public DateTime? AutoDeleteSetAt { get; set; }

        // ClearedAt timestamps per user: dictionary of userId -> ClearedAt
        public Dictionary<string, DateTime> ClearedAt { get; set; } = new();

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public static (string u1, string u2) NormalizeUsers(string a, string b)
        {
            return string.CompareOrdinal(a, b) < 0 ? (a, b) : (b, a);
        }
    }
}
