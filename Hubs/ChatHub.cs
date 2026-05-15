using ChatApp.Data;
using ChatApp.Helpers;
using ChatApp.Models;
using Microsoft.AspNetCore.SignalR;
using MongoDB.Driver;
using System.Collections.Concurrent;

namespace ChatApp.Hubs
{
    public class ChatHub : Hub
    {
        private readonly MongoDbContext _context;
        private readonly EncryptionHelper _crypto;

        // userId(string) => connectionId
        public static ConcurrentDictionary<string, string> OnlineUsers
            = new ConcurrentDictionary<string, string>();

        public ChatHub(
            MongoDbContext context,
            EncryptionHelper crypto)
        {
            _context = context;
            _crypto = crypto;
        }

        public override async Task OnConnectedAsync()
        {
            await base.OnConnectedAsync();
        }

        // FRONTEND:
        // connection.invoke("RegisterUser", userId)

        public async Task RegisterUser(string userId)
        {
            OnlineUsers[userId] = Context.ConnectionId;

            var update = Builders<User>.Update
                .Set(x => x.IsOnline, true)
                .Set(x => x.LastSeen, DateTime.UtcNow);

            await _context.Users.UpdateOneAsync(
                x => x.Id == userId,
                update);

            // user online broadcast
            await Clients.All.SendAsync("UserOnline", userId);

            // ✅ old undelivered messages delivered
            var pendingFilter = Builders<Message>.Filter.And(
                Builders<Message>.Filter.Eq(x => x.ReceiverId, userId),
                Builders<Message>.Filter.Eq(x => x.IsDelivered, false)
            );

            var pendingMessages = await _context.Messages
                .Find(pendingFilter)
                .ToListAsync();

            if (pendingMessages.Count > 0)
            {
                await _context.Messages.UpdateManyAsync(
                    pendingFilter,
                    Builders<Message>.Update.Set(x => x.IsDelivered, true)
                );

                foreach (var msg in pendingMessages)
                {
                    if (OnlineUsers.TryGetValue(msg.SenderId, out var senderConn))
                    {
                        await Clients.Client(senderConn)
                            .SendAsync("MessageDelivered", msg.Id);
                    }
                }
            }
        }

        // FRONTEND:
        // connection.invoke("SendMessage", receiverId, text)

        public async Task SendMessage(
            string receiverId,
            string text)
        {
            var senderId = OnlineUsers
                .FirstOrDefault(x =>
                    x.Value == Context.ConnectionId)
                .Key;

            if (string.IsNullOrEmpty(senderId))
                return;

            var msg = new Message
            {
                SenderId = senderId,
                ReceiverId = receiverId,
                EncryptedText = _crypto.Encrypt(text),
                CreatedAt = DateTime.UtcNow,
                IsDelivered =
                    OnlineUsers.ContainsKey(receiverId),
                IsSeen = false
            };

            await _context.Messages
                .InsertOneAsync(msg);

            var payload = new
            {
                Id = msg.Id,
                SenderId = msg.SenderId,
                ReceiverId = msg.ReceiverId,
                Text = text,
                CreatedAt = msg.CreatedAt,
                IsDelivered = msg.IsDelivered,
                IsSeen = msg.IsSeen
            };

            // Receiver
            if (OnlineUsers.TryGetValue(
                receiverId,
                out var receiverConnId))
            {
                await Clients.Client(receiverConnId)
                    .SendAsync(
                        "ReceiveMessage",
                        payload);
            }

            // Sender
            await Clients.Caller.SendAsync(
                "ReceiveMessage",
                payload);
        }

        public async Task SendSticker(
    string receiverId,
    string stickerUrl)
        {
            var senderId = OnlineUsers
                .FirstOrDefault(x =>
                    x.Value == Context.ConnectionId)
                .Key;

            if (string.IsNullOrEmpty(senderId))
                return;

            var msg = new Message
            {
                SenderId = senderId,
                ReceiverId = receiverId,

                StickerUrl = stickerUrl,
                IsSticker = true,

                EncryptedText =
                    _crypto.Encrypt("Sticker"),

                CreatedAt = DateTime.UtcNow,

                IsDelivered =
                    OnlineUsers.ContainsKey(receiverId),

                IsSeen = false
            };

            await _context.Messages
                .InsertOneAsync(msg);

            var payload = new
            {
                Id = msg.Id,

                SenderId = msg.SenderId,

                ReceiverId = msg.ReceiverId,

                Text = "Sticker",

                StickerUrl = msg.StickerUrl,

                IsSticker = msg.IsSticker,

                CreatedAt = msg.CreatedAt,

                IsDelivered = msg.IsDelivered,

                IsSeen = msg.IsSeen
            };

            // Receiver
            if (OnlineUsers.TryGetValue(
                receiverId,
                out var receiverConnId))
            {
                await Clients.Client(receiverConnId)
                    .SendAsync(
                        "ReceiveMessage",
                        payload);
            }

            // Sender
            await Clients.Caller.SendAsync(
                "ReceiveMessage",
                payload);
        }

        public async Task MarkSeen(
            string senderId)
        {
            var myId = OnlineUsers
                .FirstOrDefault(x =>
                    x.Value == Context.ConnectionId)
                .Key;

            if (string.IsNullOrEmpty(myId))
                return;

            var update = Builders<Message>.Update
                .Set(x => x.IsSeen, true);

            await _context.Messages
                .UpdateManyAsync(
                    x =>
                        x.SenderId == senderId &&
                        x.ReceiverId == myId &&
                        x.IsSeen == false,
                    update);

            if (OnlineUsers.TryGetValue(
                senderId,
                out var connId))
            {
                await Clients.Client(connId)
                    .SendAsync(
                        "MessagesSeen",
                        myId);
            }
        }

        public async Task UserTyping(
            string receiverId,
            bool isTyping)
        {
            var senderId = OnlineUsers
                .FirstOrDefault(x =>
                    x.Value == Context.ConnectionId)
                .Key;

            if (string.IsNullOrEmpty(senderId))
                return;

            if (OnlineUsers.TryGetValue(
                receiverId,
                out var connId))
            {
                await Clients.Client(connId)
                    .SendAsync(
                        "UserTyping",
                        new
                        {
                            senderId,
                            isTyping
                        });
            }
        }

        public override async Task OnDisconnectedAsync(
            Exception? ex)
        {
            var user = OnlineUsers
                .FirstOrDefault(x =>
                    x.Value == Context.ConnectionId);

            if (!string.IsNullOrEmpty(user.Key))
            {
                OnlineUsers.TryRemove(
                    user.Key,
                    out _);

                var update = Builders<User>.Update
                    .Set(x => x.IsOnline, false)
                    .Set(x => x.LastSeen, DateTime.UtcNow);

                await _context.Users
                    .UpdateOneAsync(
                        x => x.Id == user.Key,
                        update);

                await Clients.All.SendAsync(
                    "UserOffline",
                    user.Key);
            }

            await base.OnDisconnectedAsync(ex);
        }

        public static bool IsUserOnline(
            string userId)
        {
            return OnlineUsers
                .ContainsKey(userId);
        }
    }
}