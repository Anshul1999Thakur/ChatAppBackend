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
                CreatedAt = DateTime.UtcNow
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
                CreatedAt = message.CreatedAt
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
                CreatedAt = DateTime.UtcNow
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
                CreatedAt = message.CreatedAt
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
                    CreatedAt = msg.CreatedAt
                });
            }

            return result;
        }
    }
}

