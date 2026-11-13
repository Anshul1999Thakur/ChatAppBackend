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
        private readonly IUserRepository _userRepo;
        private readonly EncryptionHelper _crypto;

        public MessageService(
            IMessageRepository messageRepo,
            IUserRepository userRepo,
            EncryptionHelper crypto)
        {
            _messageRepo = messageRepo;
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

            var message = new Message
            {
                SenderId = senderId,
                ReceiverId = receiverId,
                EncryptedText = _crypto.Encrypt(text.Trim()),
                CreatedAt = DateTime.UtcNow,
                IsSeen = false,
                IsDelivered = false
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
                CreatedAt = message.CreatedAt,
                IsSeen = message.IsSeen,
                IsDelivered = message.IsDelivered
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

            var message = new Message
            {
                SenderId = senderId,
                ReceiverId = receiverId,
                StickerUrl = stickerUrl,
                IsSticker = true,
                EncryptedText = _crypto.Encrypt("Sticker"),
                CreatedAt = DateTime.UtcNow,
                IsSeen = false,
                IsDelivered = false
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
                CreatedAt = message.CreatedAt,
                IsSeen = message.IsSeen,
                IsDelivered = message.IsDelivered
            };
        }

        public async Task<List<MessageResponseDto>> GetConversationHistoryAsync(string currentUserId, string otherUserId, int page, int pageSize)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 30;
            if (pageSize > 100) pageSize = 100;

            var messages = await _messageRepo.GetConversationHistoryAsync(currentUserId, otherUserId, page, pageSize);

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
                    CreatedAt = msg.CreatedAt,
                    IsSeen = msg.IsSeen,
                    IsDelivered = msg.IsDelivered
                });
            }

            return result;
        }

        public async Task MarkSeenAsync(string currentUserId, string senderId)
        {
            await _messageRepo.MarkMessagesAsSeenAsync(senderId, currentUserId);
        }
    }
}

