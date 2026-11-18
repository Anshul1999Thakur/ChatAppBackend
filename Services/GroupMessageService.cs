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

            DateTime? expiresAt = null;
            if (group.AutoDeleteDurationHours > 0)
            {
                expiresAt = DateTime.UtcNow.AddHours(group.AutoDeleteDurationHours);
            }

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
                EditedAt = null,
                ExpiresAt = expiresAt
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
                EditedAt = null,
                ExpiresAt = message.ExpiresAt
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

            DateTime? expiresAt = null;
            if (group.AutoDeleteDurationHours > 0)
            {
                expiresAt = DateTime.UtcNow.AddHours(group.AutoDeleteDurationHours);
            }

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
                EditedAt = null,
                ExpiresAt = expiresAt
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
                EditedAt = null,
                ExpiresAt = message.ExpiresAt
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
                EditedAt = message.EditedAt,
                ExpiresAt = message.ExpiresAt
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

            var messages = await _messageRepo.GetGroupHistoryAsync(groupId, page, pageSize, membership.ClearedAt);

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
                    EditedAt = msg.EditedAt,
                    ExpiresAt = msg.ExpiresAt
                });
            }

            return result;
        }

        public async Task ClearGroupChatAsync(string groupId, string currentUserId, bool clearForEveryone)
        {
            var membership = await _memberRepo.GetMembershipAsync(groupId, currentUserId);
            if (membership == null)
                throw new ForbiddenException("You are not a member of this group");

            if (clearForEveryone)
            {
                if (membership.Role != GroupRole.Owner && membership.Role != GroupRole.Admin)
                    throw new ForbiddenException("Only group admins or owners can clear chat for everyone");

                await _messageRepo.DeleteAllByGroupIdAsync(groupId);
            }
            else
            {
                await _memberRepo.SetClearedAtAsync(groupId, currentUserId, DateTime.UtcNow);
            }
        }

        public async Task<ConversationSettingDto> SetGroupDisappearingMessagesAsync(string groupId, string currentUserId, int hours)
        {
            var membership = await _memberRepo.GetMembershipAsync(groupId, currentUserId);
            if (membership == null)
                throw new ForbiddenException("You are not a member of this group");

            if (membership.Role != GroupRole.Owner && membership.Role != GroupRole.Admin)
                throw new ForbiddenException("Only group admins or owners can change disappearing messages settings");

            if (hours < 0)
                throw new BadRequestException("Duration hours cannot be negative");

            var group = await _groupRepo.GetByIdAsync(groupId);
            if (group == null)
                throw new NotFoundException("Group not found or has been deleted");

            group.AutoDeleteDurationHours = hours;
            group.AutoDeleteSetBy = currentUserId;
            group.AutoDeleteSetAt = DateTime.UtcNow;

            await _groupRepo.UpdateAsync(group);

            // WhatsApp announcement system message
            var caller = await _userRepo.GetByIdAsync(currentUserId);
            string callerName = caller?.FullName ?? "An admin";
            string durationText = hours switch
            {
                0 => "off",
                24 => "24 hours",
                168 => "7 days",
                2160 => "90 days",
                _ => $"{hours} hours"
            };

            string notice = hours == 0
                ? $"{callerName} turned off disappearing messages."
                : $"{callerName} set messages to disappear after {durationText}.";

            var sysMsg = new GroupMessage
            {
                GroupId = groupId,
                SenderId = currentUserId,
                SenderName = callerName,
                EncryptedText = _crypto.Encrypt(notice),
                IsSystemMessage = true,
                CreatedAt = DateTime.UtcNow
            };
            await _messageRepo.CreateAsync(sysMsg);

            return new ConversationSettingDto
            {
                AutoDeleteDurationHours = hours,
                AutoDeleteSetBy = currentUserId,
                AutoDeleteSetAt = DateTime.UtcNow
            };
        }

        public async Task<ConversationSettingDto> GetGroupDisappearingMessagesAsync(string groupId, string currentUserId)
        {
            var membership = await _memberRepo.GetMembershipAsync(groupId, currentUserId);
            if (membership == null)
                throw new ForbiddenException("You are not a member of this group");

            var group = await _groupRepo.GetByIdAsync(groupId);
            if (group == null)
                throw new NotFoundException("Group not found or has been deleted");

            return new ConversationSettingDto
            {
                AutoDeleteDurationHours = group.AutoDeleteDurationHours,
                AutoDeleteSetBy = group.AutoDeleteSetBy,
                AutoDeleteSetAt = group.AutoDeleteSetAt
            };
        }
    }
}
