using ChatApp.Common;
using ChatApp.Models;
using ChatApp.Repositories;
using ChatApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

namespace ChatApp.Hubs
{
    [Authorize]
    public class ChatHub : Hub
    {
        private readonly IMessageService _messageService;
        private readonly IGroupMessageService _groupMessageService;
        private readonly IGroupMemberRepository _groupMemberRepo;
        private readonly IUserRepository _userRepo;
        private readonly IMessageRepository _messageRepo;
        private readonly ILogger<ChatHub> _logger;

        // Thread-safe multi-connection mapping: userId -> Set of connectionIds
        public static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> OnlineUsers = new();

        public ChatHub(
            IMessageService messageService,
            IGroupMessageService groupMessageService,
            IGroupMemberRepository groupMemberRepo,
            IUserRepository userRepo,
            IMessageRepository messageRepo,
            ILogger<ChatHub> logger)
        {
            _messageService = messageService;
            _groupMessageService = groupMessageService;
            _groupMemberRepo = groupMemberRepo;
            _userRepo = userRepo;
            _messageRepo = messageRepo;
            _logger = logger;
        }

        public override async Task OnConnectedAsync()
        {
            var userId = Context.User.TryGetUserId();
            if (!string.IsNullOrEmpty(userId))
            {
                await TrackUserConnectionAsync(userId, Context.ConnectionId);

                // Auto-join all group channels for real-time group broadcasts
                try
                {
                    var memberships = await _groupMemberRepo.GetMembershipsByUserIdAsync(userId);
                    foreach (var m in memberships)
                    {
                        await Groups.AddToGroupAsync(Context.ConnectionId, $"group_{m.GroupId}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to auto-join user groups on connect for {UserId}", userId);
                }
            }

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = Context.User.TryGetUserId();
            if (!string.IsNullOrEmpty(userId))
            {
                await RemoveUserConnectionAsync(userId, Context.ConnectionId);
            }
            else
            {
                // Fallback scan if claim was missing
                foreach (var pair in OnlineUsers)
                {
                    if (pair.Value.ContainsKey(Context.ConnectionId))
                    {
                        await RemoveUserConnectionAsync(pair.Key, Context.ConnectionId);
                        break;
                    }
                }
            }

            await base.OnDisconnectedAsync(exception);
        }

        // Backward compatibility method for existing frontend
        public async Task RegisterUser(string? clientSuppliedUserId)
        {
            var authenticatedUserId = Context.User.TryGetUserId() ?? clientSuppliedUserId;
            if (!string.IsNullOrEmpty(authenticatedUserId))
            {
                await TrackUserConnectionAsync(authenticatedUserId, Context.ConnectionId);

                // Auto-join all group channels for real-time group broadcasts
                try
                {
                    var memberships = await _groupMemberRepo.GetMembershipsByUserIdAsync(authenticatedUserId);
                    foreach (var m in memberships)
                    {
                        await Groups.AddToGroupAsync(Context.ConnectionId, $"group_{m.GroupId}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to auto-join user groups in RegisterUser for {UserId}", authenticatedUserId);
                }
            }
        }

        // ==========================================
        // 1-ON-1 MESSAGING
        // ==========================================

        public async Task SendMessage(string receiverId, string text)
        {
            var senderId = Context.User.GetUserId();
            if (string.IsNullOrEmpty(senderId)) return;

            var msg = await _messageService.SendMessageAsync(senderId, receiverId, text);

            var receiverConnections = GetUserConnections(receiverId);
            if (receiverConnections.Count > 0)
            {
                msg.IsDelivered = true;
                // Emit both standard events for frontend compatibility
                await Clients.Clients(receiverConnections).SendAsync("ReceiveMessage", msg);
                await Clients.Clients(receiverConnections).SendAsync("MessageReceived", msg);
            }

            // Also emit to all sender's connections (other open tabs/devices)
            var senderConnections = GetUserConnections(senderId);
            await Clients.Clients(senderConnections).SendAsync("ReceiveMessage", msg);
            await Clients.Clients(senderConnections).SendAsync("MessageReceived", msg);
        }

        public async Task SendSticker(string receiverId, string stickerUrl)
        {
            var senderId = Context.User.GetUserId();
            if (string.IsNullOrEmpty(senderId)) return;

            var msg = await _messageService.SendStickerAsync(senderId, receiverId, stickerUrl);

            var receiverConnections = GetUserConnections(receiverId);
            if (receiverConnections.Count > 0)
            {
                msg.IsDelivered = true;
                await Clients.Clients(receiverConnections).SendAsync("ReceiveMessage", msg);
                await Clients.Clients(receiverConnections).SendAsync("MessageReceived", msg);
            }

            var senderConnections = GetUserConnections(senderId);
            await Clients.Clients(senderConnections).SendAsync("ReceiveMessage", msg);
            await Clients.Clients(senderConnections).SendAsync("MessageReceived", msg);
        }

        public async Task EditMessage(string messageId, string newText)
        {
            var senderId = Context.User.GetUserId();
            if (string.IsNullOrEmpty(senderId)) return;

            var updatedMsg = await _messageService.EditMessageAsync(messageId, senderId, newText);

            var receiverConnections = GetUserConnections(updatedMsg.ReceiverId);
            if (receiverConnections.Count > 0)
            {
                await Clients.Clients(receiverConnections).SendAsync("MessageUpdated", updatedMsg);
                await Clients.Clients(receiverConnections).SendAsync("MessageEdited", updatedMsg);
            }

            var senderConnections = GetUserConnections(senderId);
            await Clients.Clients(senderConnections).SendAsync("MessageUpdated", updatedMsg);
            await Clients.Clients(senderConnections).SendAsync("MessageEdited", updatedMsg);
        }

        public async Task MarkSeen(string senderId)
        {
            var currentUserId = Context.User.GetUserId();
            if (string.IsNullOrEmpty(currentUserId)) return;

            await _messageService.MarkSeenAsync(currentUserId, senderId);

            var senderConnections = GetUserConnections(senderId);
            if (senderConnections.Count > 0)
            {
                await Clients.Clients(senderConnections).SendAsync("MessagesSeen", currentUserId);
            }
        }

        public async Task UserTyping(string receiverId, bool isTyping = true)
        {
            var senderId = Context.User.GetUserId();
            if (string.IsNullOrEmpty(senderId)) return;

            var receiverConnections = GetUserConnections(receiverId);
            if (receiverConnections.Count > 0)
            {
                await Clients.Clients(receiverConnections).SendAsync("UserTyping", new { senderId, isTyping });
                if (!isTyping)
                {
                    await Clients.Clients(receiverConnections).SendAsync("UserStoppedTyping", senderId);
                }
            }
        }

        public async Task UserStoppedTyping(string receiverId)
        {
            await UserTyping(receiverId, false);
        }

        // ==========================================
        // GROUP CHAT REAL-TIME
        // ==========================================

        public async Task JoinGroup(string groupId)
        {
            var userId = Context.User.GetUserId();
            var membership = await _groupMemberRepo.GetMembershipAsync(groupId, userId);
            if (membership == null)
            {
                throw new HubException("Unauthorized to join this group room");
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, $"group_{groupId}");
        }

        public async Task LeaveGroup(string groupId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"group_{groupId}");
        }

        public async Task SendGroupMessage(string groupId, string text)
        {
            var senderId = Context.User.GetUserId();
            var msg = await _groupMessageService.SendGroupMessageAsync(groupId, senderId, text);

            await Clients.Group($"group_{groupId}").SendAsync("ReceiveGroupMessage", msg);
            await Clients.Group($"group_{groupId}").SendAsync("GroupMessageReceived", msg);
        }

        public async Task SendGroupSticker(string groupId, string stickerUrl)
        {
            var senderId = Context.User.GetUserId();
            var msg = await _groupMessageService.SendGroupStickerAsync(groupId, senderId, stickerUrl);

            await Clients.Group($"group_{groupId}").SendAsync("ReceiveGroupMessage", msg);
            await Clients.Group($"group_{groupId}").SendAsync("GroupMessageReceived", msg);
        }

        public async Task EditGroupMessage(string groupId, string messageId, string newText)
        {
            var senderId = Context.User.GetUserId();
            var msg = await _groupMessageService.EditGroupMessageAsync(groupId, messageId, senderId, newText);

            await Clients.Group($"group_{groupId}").SendAsync("GroupMessageUpdated", msg);
            await Clients.Group($"group_{groupId}").SendAsync("GroupMessageEdited", msg);
        }

        public async Task GroupTyping(string groupId, bool isTyping)
        {
            var senderId = Context.User.GetUserId();
            var senderName = Context.User.GetFullName();

            await Clients.OthersInGroup($"group_{groupId}").SendAsync("GroupUserTyping", new
            {
                groupId,
                userId = senderId,
                userName = senderName,
                isTyping
            });
        }

        // ==========================================
        // CONNECTION TRACKING HELPERS
        // ==========================================

        private async Task TrackUserConnectionAsync(string userId, string connectionId)
        {
            var set = OnlineUsers.GetOrAdd(userId, _ => new ConcurrentDictionary<string, byte>());
            bool isFirstConnection = set.IsEmpty;
            set[connectionId] = 0;

            if (isFirstConnection)
            {
                await _userRepo.UpdateOnlineStatusAsync(userId, true);
                await Clients.All.SendAsync("UserOnline", userId);
            }

            // Deliver pending messages to this user
            var pending = await _messageRepo.GetPendingDeliveredMessagesAsync(userId);
            if (pending.Count > 0)
            {
                await _messageRepo.MarkMessagesAsDeliveredAsync(userId);
                foreach (var msg in pending)
                {
                    var senderConnections = GetUserConnections(msg.SenderId);
                    if (senderConnections.Count > 0)
                    {
                        await Clients.Clients(senderConnections).SendAsync("MessageDelivered", msg.Id);
                    }
                }
            }
        }

        private async Task RemoveUserConnectionAsync(string userId, string connectionId)
        {
            if (OnlineUsers.TryGetValue(userId, out var set))
            {
                set.TryRemove(connectionId, out _);
                if (set.IsEmpty)
                {
                    OnlineUsers.TryRemove(userId, out _);
                    await _userRepo.UpdateOnlineStatusAsync(userId, false);
                    await Clients.All.SendAsync("UserOffline", userId);
                }
            }
        }

        public static IReadOnlyList<string> GetUserConnections(string userId)
        {
            if (OnlineUsers.TryGetValue(userId, out var set))
            {
                return set.Keys.ToList();
            }
            return Array.Empty<string>();
        }

        public static bool IsUserOnline(string userId)
        {
            return OnlineUsers.TryGetValue(userId, out var set) && !set.IsEmpty;
        }
    }
}
