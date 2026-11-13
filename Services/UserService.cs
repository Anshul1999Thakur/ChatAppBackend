using ChatApp.Common;
using ChatApp.DTOs;
using ChatApp.Helpers;
using ChatApp.Repositories;

namespace ChatApp.Services
{
    public class UserService : IUserService
    {
        private readonly IUserRepository _userRepo;
        private readonly IMessageRepository _messageRepo;
        private readonly IFileStorageService _fileService;
        private readonly EncryptionHelper _crypto;

        public UserService(
            IUserRepository userRepo,
            IMessageRepository messageRepo,
            IFileStorageService fileService,
            EncryptionHelper crypto)
        {
            _userRepo = userRepo;
            _messageRepo = messageRepo;
            _fileService = fileService;
            _crypto = crypto;
        }

        public async Task<UserProfileDto> GetProfileAsync(string userId)
        {
            var user = await _userRepo.GetByIdAsync(userId);
            if (user == null)
                throw new NotFoundException("User not found");

            return new UserProfileDto
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                ProfilePictureUrl = user.ProfilePictureUrl,
                Bio = user.Bio,
                IsOnline = user.IsOnline,
                LastSeen = user.LastSeen,
                CreatedAt = user.CreatedAt
            };
        }

        public async Task<UserProfileDto> UpdateProfileAsync(string userId, UpdateProfileDto dto)
        {
            var user = await _userRepo.GetByIdAsync(userId);
            if (user == null)
                throw new NotFoundException("User not found");

            if (string.IsNullOrWhiteSpace(dto.FullName))
                throw new BadRequestException("Full name cannot be empty");

            user.FullName = dto.FullName.Trim();
            user.Bio = dto.Bio?.Trim();
            user.UpdatedAt = DateTime.UtcNow;

            await _userRepo.UpdateAsync(user);

            return new UserProfileDto
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                ProfilePictureUrl = user.ProfilePictureUrl,
                Bio = user.Bio,
                IsOnline = user.IsOnline,
                LastSeen = user.LastSeen,
                CreatedAt = user.CreatedAt
            };
        }

        public async Task<UserProfileDto> UpdateProfileImageAsync(string userId, IFormFile file)
        {
            var user = await _userRepo.GetByIdAsync(userId);
            if (user == null)
                throw new NotFoundException("User not found");

            var oldImage = user.ProfilePictureUrl;
            var newImageUrl = await _fileService.SaveImageAsync(file, "profiles");

            user.ProfilePictureUrl = newImageUrl;
            user.UpdatedAt = DateTime.UtcNow;

            await _userRepo.UpdateAsync(user);

            if (!string.IsNullOrEmpty(oldImage) && !oldImage.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                _fileService.DeleteFile(oldImage);
            }

            return new UserProfileDto
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                ProfilePictureUrl = user.ProfilePictureUrl,
                Bio = user.Bio,
                IsOnline = user.IsOnline,
                LastSeen = user.LastSeen,
                CreatedAt = user.CreatedAt
            };
        }

        public async Task<UserProfileDto> RemoveProfileImageAsync(string userId)
        {
            var user = await _userRepo.GetByIdAsync(userId);
            if (user == null)
                throw new NotFoundException("User not found");

            var oldImage = user.ProfilePictureUrl;
            user.ProfilePictureUrl = null;
            user.UpdatedAt = DateTime.UtcNow;

            await _userRepo.UpdateAsync(user);

            if (!string.IsNullOrEmpty(oldImage) && !oldImage.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                _fileService.DeleteFile(oldImage);
            }

            return new UserProfileDto
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                ProfilePictureUrl = null,
                Bio = user.Bio,
                IsOnline = user.IsOnline,
                LastSeen = user.LastSeen,
                CreatedAt = user.CreatedAt
            };
        }

        public async Task<List<UserContactDto>> GetUserDirectoryAsync(string currentUserId)
        {
            var users = await _userRepo.GetAllExceptAsync(currentUserId);
            var result = new List<UserContactDto>(users.Count);

            foreach (var user in users)
            {
                var unreadCount = await _messageRepo.GetUnreadCountAsync(user.Id, currentUserId);
                var lastMsg = await _messageRepo.GetLastMessageBetweenUsersAsync(user.Id, currentUserId);

                string decryptedLastMessage = string.Empty;
                DateTime? lastMessageTime = null;

                if (lastMsg != null)
                {
                    lastMessageTime = lastMsg.CreatedAt;
                    if (lastMsg.IsSticker)
                    {
                        decryptedLastMessage = "🎭 Sticker";
                    }
                    else
                    {
                        try
                        {
                            decryptedLastMessage = _crypto.Decrypt(lastMsg.EncryptedText);
                        }
                        catch
                        {
                            decryptedLastMessage = "[Encrypted Message]";
                        }
                    }
                }

                result.Add(new UserContactDto
                {
                    Id = user.Id,
                    FullName = user.FullName,
                    Email = user.Email,
                    ProfilePictureUrl = user.ProfilePictureUrl,
                    IsOnline = user.IsOnline,
                    LastSeen = user.LastSeen,
                    UnreadCount = unreadCount,
                    LastMessage = decryptedLastMessage,
                    LastMessageTime = lastMessageTime
                });
            }

            return result;
        }
    }
}
