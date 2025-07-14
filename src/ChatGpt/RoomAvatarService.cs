using Microsoft.Extensions.Logging;

namespace ChatGpt
{
    public class RoomAvatarService
    {
        private readonly ImageGenerationClient _imageClient;
        private readonly ILogger<RoomAvatarService> _logger;
        private readonly string _cacheDir;
        private readonly HttpClient _httpClient = new();

        public RoomAvatarService(ImageGenerationClient imageClient, ILogger<RoomAvatarService> logger, string? cacheDirectory = null)
        {
            _imageClient = imageClient;
            _logger = logger;
            _cacheDir = cacheDirectory ?? Path.Combine(AppContext.BaseDirectory, "wwwroot", "room-images");
            Directory.CreateDirectory(_cacheDir);
        }

        public async Task<string?> GetOrGenerateImage(string description)
        {
            if (string.IsNullOrWhiteSpace(description))
                throw new ArgumentException("Description must be provided", nameof(description));

            var fileName = SanitizeFileName(description) + ".png";
            var filePath = Path.Combine(_cacheDir, fileName);
            var relativePath = Path.Combine("room-images", fileName);
            if (File.Exists(filePath))
            {
                _logger.LogInformation("Returning cached room image for {description}", description);
                return relativePath;
            }

            var prompt = $"Cartoon of a {description}";
            var imageUrl = await _imageClient.GenerateImage(prompt, ImageSize.DallE2Small);

            if (string.IsNullOrEmpty(imageUrl))
            {
                _logger.LogWarning("Image generation returned no URL for {description}", description);
                return null;
            }

            try
            {
                var bytes = await _httpClient.GetByteArrayAsync(imageUrl);
                await File.WriteAllBytesAsync(filePath, bytes);
                _logger.LogInformation("Cached room image for {description} at {path}", description, filePath);
                return relativePath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cache room image for {description}", description);
                return null;
            }
        }

        private static string SanitizeFileName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var valid = new string(name.ToLower().Select(c => invalid.Contains(c) ? '_' : c).ToArray());
            return valid.Replace(' ', '_');
        }
    }
}
