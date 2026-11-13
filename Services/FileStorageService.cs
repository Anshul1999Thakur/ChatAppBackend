using ChatApp.Common;

namespace ChatApp.Services
{
    public class FileStorageService : IFileStorageService
    {
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<FileStorageService> _logger;

        private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".webp"
        };

        private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "image/jpeg", "image/png", "image/webp"
        };

        public FileStorageService(IWebHostEnvironment env, ILogger<FileStorageService> logger)
        {
            _env = env;
            _logger = logger;
        }

        public async Task<string> SaveImageAsync(IFormFile? file, string subFolder, long maxSizeBytes = 5 * 1024 * 1024)
        {
            if (file == null || file.Length == 0)
                throw new BadRequestException("No file was uploaded");

            if (file.Length > maxSizeBytes)
                throw new BadRequestException($"File size exceeds the allowed maximum limit of {maxSizeBytes / (1024 * 1024)} MB");

            var ext = Path.GetExtension(file.FileName);
            if (string.IsNullOrEmpty(ext) || !AllowedExtensions.Contains(ext))
                throw new BadRequestException("Invalid file type. Only JPEG, PNG, and WebP images are allowed");

            if (!AllowedMimeTypes.Contains(file.ContentType))
                throw new BadRequestException("Invalid MIME type for image upload");

            // Magic bytes verification
            using (var stream = file.OpenReadStream())
            {
                var headerBytes = new byte[8];
                int read = await stream.ReadAsync(headerBytes, 0, headerBytes.Length);
                if (read < 4 || !IsValidImageHeader(headerBytes, ext))
                {
                    throw new BadRequestException("File header is corrupted or does not match a valid image format");
                }
            }

            var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var uploadDir = Path.Combine(webRoot, "uploads", subFolder);

            if (!Directory.Exists(uploadDir))
            {
                Directory.CreateDirectory(uploadDir);
            }

            var uniqueFileName = $"{Guid.NewGuid():N}{ext.ToLowerInvariant()}";
            var fullPath = Path.Combine(uploadDir, uniqueFileName);

            using (var fileStream = new FileStream(fullPath, FileMode.Create))
            {
                await file.CopyToAsync(fileStream);
            }

            return $"/uploads/{subFolder}/{uniqueFileName}";
        }

        public void DeleteFile(string? relativeUrl)
        {
            if (string.IsNullOrWhiteSpace(relativeUrl)) return;

            try
            {
                var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                var cleanPath = relativeUrl.TrimStart('/', '\\').Replace('/', Path.DirectorySeparatorChar);
                var fullPath = Path.Combine(webRoot, cleanPath);

                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete file at {Path}", relativeUrl);
            }
        }

        private static bool IsValidImageHeader(byte[] bytes, string extension)
        {
            var ext = extension.ToLowerInvariant();

            // JPEG: FF D8 FF
            if (ext is ".jpg" or ".jpeg")
            {
                return bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF;
            }

            // PNG: 89 50 4E 47
            if (ext == ".png")
            {
                return bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47;
            }

            // WebP: RIFF ... WEBP
            if (ext == ".webp")
            {
                return bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46;
            }

            return false;
        }
    }
}

