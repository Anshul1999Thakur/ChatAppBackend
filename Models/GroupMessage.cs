using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ChatApp.Models
{
    public class GroupMessage
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        [BsonRepresentation(BsonType.ObjectId)]
        public string GroupId { get; set; } = string.Empty;

        public string SenderId { get; set; } = string.Empty;

        public string SenderName { get; set; } = string.Empty;

        public string EncryptedText { get; set; } = string.Empty;

        public string? StickerUrl { get; set; }

        public bool IsSticker { get; set; } = false;

        public bool IsSystemMessage { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public bool IsEdited { get; set; } = false;

        public DateTime? EditedAt { get; set; }
    }
}
