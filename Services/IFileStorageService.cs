namespace ChatApp.Services
{
    public interface IFileStorageService
    {
        Task<string> SaveImageAsync(IFormFile file, string subFolder, long maxSizeBytes = 5 * 1024 * 1024);
        void DeleteFile(string? relativeUrl);
    }
}

