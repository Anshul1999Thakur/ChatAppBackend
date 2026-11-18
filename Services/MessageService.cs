using ChatApp.Common;
using ChatApp.DTOs;
using ChatApp.Helpers;
using ChatApp.Models;
using ChatApp.Repositories;

namespace ChatApp.Services
{
    public class MessageService : IMessageService
    {
        private readonly IMessageRepository _messageRepo;
        private readonly IConversationSettingRepository _settingRepo;
        private readonly IUserRepository _userRepo;
        private readonly EncryptionHelper _crypto;

        public MessageService(
            IMessageRepository messageRepo,
            IConversationSettingRepository settingRepo,
            IUserRepository userRepo,
            EncryptionHelper crypto)
        {
            _messageRepo = messageRepo;
            _settingRepo = settingRepo;
            _userRepo = userRepo;
            _crypto = crypto;
        }

        public async Task<MessageResponseDto> SendMessageAsync(string senderId, string receiverId, string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                throw new BadRequestException("Message text cannot be empty or whitespace");

            if (senderId == receiverId)
                throw new BadRequestException("You cannot send a message to yourself");

            var receiver = await _userRepo.GetByIdAsync(receiverId);
            if (receiver == null)
                throw new NotFoundException("Receiver does not exist");

            // Check disappearing message settings
            var setting = await _settingRepo.GetSettingAsync(senderId, receiverId);
            DateTime? expiresAt = null;
            if (setting != null && setting.AutoDeleteDurationHours > 0)
            {
                expiresAt = DateTime.UtcNow.AddHours(setting.AutoDeleteDurationHours);
            }

            var message = new Message
            {
                SenderId = senderId,
                ReceiverId = receiverId,
                EncryptedText = _crypto.Encrypt(text.Trim()),
                CreatedAt = DateTime.UtcNow,
                IsSeen = false,
                IsDelivered = false,
                IsEdited = false,
                EditedAt = null,
                ExpiresAt = expiresAt,
                IsSystemMessage = false
            };

            await _messageRepo.CreateAsync(message);

            return new MessageResponseDto
            {
                Id = message.Id,
                SenderId = message.SenderId,
                ReceiverId = message.ReceiverId,
                Text = text.Trim(),
                StickerUrl = null,
                IsSticker = false,
                IsSystemMessage = false,
                CreatedAt = message.CreatedAt,
                IsSeen = message.IsSeen,
                IsDelivered = message.IsDelivered,
                IsEdited = false,
                EditedAt = null,
                ExpiresAt = message.ExpiresAt
            };
        }

        public async Task<MessageResponseDto> SendStickerAsync(string senderId, string receiverId, string stickerUrl)
        {
            if (string.IsNullOrWhiteSpace(stickerUrl))
                throw new BadRequestException("Sticker URL cannot be empty");

            if (senderId == receiverId)
                throw new BadRequestException("You cannot send a sticker to yourself");

            var receiver = await _userRepo.GetByIdAsync(receiverId);
            if (receiver == null)
                throw new NotFoundException("Receiver does not exist");

            var setting = await _settingRepo.GetSettingAsync(senderId, receiverId);
            DateTime? expiresAt = null;
            if (setting != null && setting.AutoDeleteDurationHours > 0)
            {
                expiresAt = DateTime.UtcNow.AddHours(setting.AutoDeleteDurationHours);
            }

            var message = new Message
            {
                SenderId = senderId,
                ReceiverId = receiverId,
                StickerUrl = stickerUrl,
                IsSticker = true,
                EncryptedText = _crypto.Encrypt("Sticker"),
                CreatedAt = DateTime.UtcNow,
                IsSeen = false,
                IsDelivered = false,
                IsEdited = false,
                EditedAt = null,
                ExpiresAt = expiresAt,
                IsSystemMessage = false
            };

            await _messageRepo.CreateAsync(message);

            return new MessageResponseDto
            {
                Id = message.Id,
                SenderId = message.SenderId,
                ReceiverId = message.ReceiverId,
                Text = "Sticker",
                StickerUrl = message.StickerUrl,
                IsSticker = true,
                IsSystemMessage = false,
                CreatedAt = message.CreatedAt,
                IsSeen = message.IsSeen,
                IsDelivered = message.IsDelivered,
                IsEdited = false,
                EditedAt = null,
                ExpiresAt = message.ExpiresAt
            };
        }

        public async Task<MessageResponseDto> EditMessageAsync(string messageId, string currentUserId, string newText)
        {
            if (string.IsNullOrWhiteSpace(newText))
                throw new BadRequestException("Message text cannot be empty or whitespace");

            var trimmedText = newText.Trim();
            if (trimmedText.Length > 2000)
                throw new BadRequestException("Message text cannot exceed 2000 characters");

            var message = await _messageRepo.GetByIdAsync(messageId);
            if (message == null)
                throw new NotFoundException("Message not found");

            // WhatsApp Rule 1: Only sender can edit message
            if (message.SenderId != currentUserId)
                throw new ForbiddenException("You can only edit messages sent by you");

            // WhatsApp Rule 2: Cannot edit stickers or system messages
            if (message.IsSticker || message.IsSystemMessage)
                throw new BadRequestException("Stickers and system notices cannot be edited");

            // WhatsApp Rule 3: 15-minute time window
            if (DateTime.UtcNow - message.CreatedAt > TimeSpan.FromMinutes(15))
                throw new BadRequestException("Messages can only be edited within 15 minutes of sending");

            // Update content
            message.EncryptedText = _crypto.Encrypt(trimmedText);
            message.IsEdited = true;
            message.EditedAt = DateTime.UtcNow;

            await _messageRepo.UpdateAsync(message);

            return new MessageResponseDto
            {
                Id = message.Id,
                SenderId = message.SenderId,
                ReceiverId = message.ReceiverId,
                Text = trimmedText,
                StickerUrl = message.StickerUrl,
                IsSticker = message.IsSticker,
                IsSystemMessage = message.IsSystemMessage,
                CreatedAt = message.CreatedAt,
                IsSeen = message.IsSeen,
                IsDelivered = message.IsDelivered,
                IsEdited = message.IsEdited,
                EditedAt = message.EditedAt,
                ExpiresAt = message.ExpiresAt
            };
        }

        public async Task<List<MessageResponseDto>> GetConversationHistoryAsync(string currentUserId, string otherUserId, int page, int pageSize)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 30;
            if (pageSize > 100) pageSize = 100;

            // Fetch clearedAt timestamp for current user if available
            var setting = await _settingRepo.GetSettingAsync(currentUserId, otherUserId);
            DateTime? clearedAfter = null;
            if (setting != null && setting.ClearedAt.TryGetValue(currentUserId, out var dt))
            {
                clearedAfter = dt;
            }

            var messages = await _messageRepo.GetConversationHistoryAsync(currentUserId, otherUserId, page, pageSize, clearedAfter);

            var result = new List<MessageResponseDto>(messages.Count);
            foreach (var msg in messages)
            {
                string text = string.Empty;
                try
                {
                    text = _crypto.Decrypt(msg.EncryptedText);
                }
                catch
                {
                    text = "[Encrypted Message]";
                }

                result.Add(new MessageResponseDto
                {
                    Id = msg.Id,
                    SenderId = msg.SenderId,
                    ReceiverId = msg.ReceiverId,
                    Text = text,
                    StickerUrl = msg.StickerUrl,
                    IsSticker = msg.IsSticker,
                    IsSystemMessage = msg.IsSystemMessage,
                    CreatedAt = msg.CreatedAt,
                    IsSeen = msg.IsSeen,
                    IsDelivered = msg.IsDelivered,
                    IsEdited = msg.IsEdited,
                    EditedAt = msg.EditedAt,
                    ExpiresAt = msg.ExpiresAt
                });
            }

            return result;
        }

        public async Task MarkSeenAsync(string currentUserId, string senderId)
        {
            await _messageRepo.MarkMessagesAsSeenAsync(senderId, currentUserId);
        }

        public async Task ClearChatAsync(string currentUserId, string otherUserId, bool clearForEveryone)
        {
            if (clearForEveryone)
            {
                // Delete messages completely from database
                await _messageRepo.ClearMessagesAsync(currentUserId, otherUserId);
            }
            else
            {
                // Set ClearedAt timestamp for current user (hides messages prior to this moment for caller)
                await _settingRepo.SetUserClearedAtAsync(currentUserId, otherUserId, currentUserId, DateTime.UtcNow);
            }
        }

        public async Task<ConversationSettingDto> GetChatSettingsAsync(string currentUserId, string otherUserId)
        {
            var setting = await _settingRepo.GetSettingAsync(currentUserId, otherUserId);
            return new ConversationSettingDto
            {
                AutoDeleteDurationHours = setting?.AutoDeleteDurationHours ?? 0,
                AutoDeleteSetBy = setting?.AutoDeleteSetBy,
                AutoDeleteSetAt = setting?.AutoDeleteSetAt
            };
        }

        public async Task<ConversationSettingDto> SetDisappearingMessagesAsync(string currentUserId, string otherUserId, int hours)
        {
            if (hours < 0)
                throw new BadRequestException("Duration hours cannot be negative");

            await _settingRepo.UpdateAutoDeleteDurationAsync(currentUserId, otherUserId, hours, currentUserId);

            // Create WhatsApp-style in-chat system message announcing the change
            string durationText = hours switch
            {
                0 => "off",
                24 => "24 hours",
                168 => "7 days",
                2160 => "90 days",
                _ => $"{hours} hours"
            };

            var caller = await _userRepo.GetByIdAsync(currentUserId);
            var callerName = caller?.FullName ?? "User";

            string notice = hours == 0
                ? $"{callerName} turned off disappearing messages."
                : $"{callerName} set messages to disappear after {durationText}.";

            var systemMsg = new Message
            {
                SenderId = currentUserId,
                ReceiverId = otherUserId,
                EncryptedText = _crypto.Encrypt(notice),
                IsSystemMessage = true,
                CreatedAt = DateTime.UtcNow,
                IsSeen = false,
                IsDelivered = false
            };
            await _messageRepo.CreateAsync(systemMsg);

            return new ConversationSettingDto
            {
                AutoDeleteDurationHours = hours,
                AutoDeleteSetBy = currentUserId,
                AutoDeleteSetAt = DateTime.UtcNow
            };
        }
    }
}
