using ChatApp.DTOs;

namespace ChatApp.Services
{
    public interface IUserService
    {
        Task<UserProfileDto> GetProfileAsync(string userId);
        Task<UserProfileDto> UpdateProfileAsync(string userId, UpdateProfileDto dto);
        Task<UserProfileDto> UpdateProfileImageAsync(string userId, IFormFile file);
        Task<List<UserContactDto>> GetUserDirectoryAsync(string currentUserId);
    }
}

