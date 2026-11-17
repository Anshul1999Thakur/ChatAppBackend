using ChatApp.Common;
using ChatApp.DTOs;
using ChatApp.Helpers;
using ChatApp.Models;
using ChatApp.Repositories;

namespace ChatApp.Services
{
    public class GroupMessageService : IGroupMessageService
    {
        private readonly IGroupMessageRepository _messageRepo;
        private readonly IGroupMemberRepository _memberRepo;
        private readonly IGroupRepository _groupRepo;
        private readonly IUserRepository _userRepo;
        private readonly EncryptionHelper _crypto;

        public GroupMessageService(
            IGroupMessageRepository messageRepo,
            IGroupMemberRepository memberRepo,
            IGroupRepository groupRepo,
            IUserRepository userRepo,
            EncryptionHelper crypto)
        {
            _messageRepo = messageRepo;
            _memberRepo = memberRepo;
            _groupRepo = groupRepo;
            _userRepo = userRepo;
            _crypto = crypto;
        }

        public async Task<GroupMessageDto> SendGroupMessageAsync(string groupId, string senderId, string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                throw new BadRequestException("Message text cannot be empty or whitespace");

            var membership = await _memberRepo.GetMembershipAsync(groupId, senderId);
            if (membership == null)
                throw new ForbiddenException("You are not a member of this group");

            var group = await _groupRepo.GetByIdAsync(groupId);
            if (group == null)
                throw new NotFoundException("Group not found or has been deleted");

            var sender = await _userRepo.GetByIdAsync(senderId);

            var message = new GroupMessage
            {
                GroupId = groupId,
                SenderId = senderId,
                SenderName = sender?.FullName ?? "Unknown",
                EncryptedText = _crypto.Encrypt(text.Trim()),
                IsSticker = false,
                IsSystemMessage = false,
                CreatedAt = DateTime.UtcNow,
                IsEdited = false,
                EditedAt = null
            };

            await _messageRepo.CreateAsync(message);

            return new GroupMessageDto
            {
                Id = message.Id,
                GroupId = message.GroupId,
                SenderId = message.SenderId,
                SenderName = message.SenderName,
                Text = text.Trim(),
                StickerUrl = null,
                IsSticker = false,
                IsSystemMessage = false,
                CreatedAt = message.CreatedAt,
                IsEdited = false,
                EditedAt = null
            };
        }

        public async Task<GroupMessageDto> SendGroupStickerAsync(string groupId, string senderId, string stickerUrl)
        {
            if (string.IsNullOrWhiteSpace(stickerUrl))
                throw new BadRequestException("Sticker URL cannot be empty");

            var membership = await _memberRepo.GetMembershipAsync(groupId, senderId);
            if (membership == null)
                throw new ForbiddenException("You are not a member of this group");

            var group = await _groupRepo.GetByIdAsync(groupId);
            if (group == null)
                throw new NotFoundException("Group not found or has been deleted");

            var sender = await _userRepo.GetByIdAsync(senderId);

            var message = new GroupMessage
            {
                GroupId = groupId,
                SenderId = senderId,
                SenderName = sender?.FullName ?? "Unknown",
                StickerUrl = stickerUrl,
                IsSticker = true,
                EncryptedText = _crypto.Encrypt("Sticker"),
                IsSystemMessage = false,
                CreatedAt = DateTime.UtcNow,
                IsEdited = false,
                EditedAt = null
            };

            await _messageRepo.CreateAsync(message);

            return new GroupMessageDto
            {
                Id = message.Id,
                GroupId = message.GroupId,
                SenderId = message.SenderId,
                SenderName = message.SenderName,
                Text = "Sticker",
                StickerUrl = message.StickerUrl,
                IsSticker = true,
                IsSystemMessage = false,
                CreatedAt = message.CreatedAt,
                IsEdited = false,
                EditedAt = null
            };
        }

        public async Task<GroupMessageDto> EditGroupMessageAsync(string groupId, string messageId, string currentUserId, string newText)
        {
            if (string.IsNullOrWhiteSpace(newText))
                throw new BadRequestException("Message text cannot be empty or whitespace");

            var trimmedText = newText.Trim();
            if (trimmedText.Length > 2000)
                throw new BadRequestException("Message text cannot exceed 2000 characters");

            var membership = await _memberRepo.GetMembershipAsync(groupId, currentUserId);
            if (membership == null)
                throw new ForbiddenException("You are not a member of this group");

            var group = await _groupRepo.GetByIdAsync(groupId);
            if (group == null)
                throw new NotFoundException("Group not found or has been deleted");

            var message = await _messageRepo.GetByIdAsync(messageId);
            if (message == null || message.GroupId != groupId)
                throw new NotFoundException("Message not found in this group");

            // WhatsApp Rule 1: Only sender can edit their message
            if (message.SenderId != currentUserId)
                throw new ForbiddenException("You can only edit your own messages in the group");

            // WhatsApp Rule 2: Cannot edit system messages or stickers
            if (message.IsSystemMessage || message.IsSticker)
                throw new BadRequestException("System messages and stickers cannot be edited");

            // WhatsApp Rule 3: 15-minute time window
            if (DateTime.UtcNow - message.CreatedAt > TimeSpan.FromMinutes(15))
                throw new BadRequestException("Messages can only be edited within 15 minutes of sending");

            // Update content
            message.EncryptedText = _crypto.Encrypt(trimmedText);
            message.IsEdited = true;
            message.EditedAt = DateTime.UtcNow;

            await _messageRepo.UpdateAsync(message);

            return new GroupMessageDto
            {
                Id = message.Id,
                GroupId = message.GroupId,
                SenderId = message.SenderId,
                SenderName = message.SenderName,
                Text = trimmedText,
                StickerUrl = message.StickerUrl,
                IsSticker = message.IsSticker,
                IsSystemMessage = message.IsSystemMessage,
                CreatedAt = message.CreatedAt,
                IsEdited = message.IsEdited,
                EditedAt = message.EditedAt
            };
        }

        public async Task<List<GroupMessageDto>> GetGroupMessagesAsync(string groupId, string currentUserId, int page, int pageSize)
        {
            var membership = await _memberRepo.GetMembershipAsync(groupId, currentUserId);
            if (membership == null)
                throw new ForbiddenException("You are not a member of this group");

            var group = await _groupRepo.GetByIdAsync(groupId);
            if (group == null)
                throw new NotFoundException("Group not found or has been deleted");

            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 30;
            if (pageSize > 100) pageSize = 100;

            var messages = await _messageRepo.GetGroupHistoryAsync(groupId, page, pageSize);

            var result = new List<GroupMessageDto>(messages.Count);
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

                result.Add(new GroupMessageDto
                {
                    Id = msg.Id,
                    GroupId = msg.GroupId,
                    SenderId = msg.SenderId,
                    SenderName = msg.SenderName,
                    Text = text,
                    StickerUrl = msg.StickerUrl,
                    IsSticker = msg.IsSticker,
                    IsSystemMessage = msg.IsSystemMessage,
                    CreatedAt = msg.CreatedAt,
                    IsEdited = msg.IsEdited,
                    EditedAt = msg.EditedAt
                });
            }

            return result;
        }
    }
}
