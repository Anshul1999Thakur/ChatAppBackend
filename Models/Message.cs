using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ChatApp.Models
{
    public class Message
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }

        public string SenderId { get; set; }

        public string ReceiverId { get; set; }

        public string EncryptedText { get; set; }

        public string? StickerUrl { get; set; }

        public bool IsSticker { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public bool IsSeen { get; set; }

        public bool IsDelivered { get; set; }
    }
}