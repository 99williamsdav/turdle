using Microsoft.Extensions.Logging;

namespace ChatGpt
{
    public class PersonalityAvatarService
    {
        private readonly ImageGenerationClient _imageClient;
        private readonly ILogger<PersonalityAvatarService> _logger;
        private readonly string _cacheDir;
        private readonly HttpClient _httpClient = new();

        public PersonalityAvatarService(ImageGenerationClient imageClient, ILogger<PersonalityAvatarService> logger, string? cacheDirectory = null)
        {
            _imageClient = imageClient;
            _logger = logger;
            _cacheDir = cacheDirectory ?? Path.Combine(AppContext.BaseDirectory, "wwwroot", "avatar-cache");
            Directory.CreateDirectory(_cacheDir);
        }

        public async Task<string?> GetOrGenerateAvatar(string personality)
        {
            if (string.IsNullOrWhiteSpace(personality))
                throw new ArgumentException("Personality must be provided", nameof(personality));

            var fileName = SanitizeFileName(personality) + ".png";
            var filePath = Path.Combine(_cacheDir, fileName);
            var relativePath = Path.Combine("avatar-cache", fileName);
            if (File.Exists(filePath))
            {
                _logger.LogInformation("Returning cached avatar for {personality}", personality);
                return relativePath;
            }

            var prompt = $"Generate a cartoon avatar of {personality}. Make them look pretentiously smart, like they're trying to look smarter than they are";
            var imageUrl = await _imageClient.GenerateImage(prompt, ImageSize.DallE2Large);

            if (string.IsNullOrEmpty(imageUrl))
            {
                _logger.LogWarning("Image generation returned no URL for {personality}", personality);
                return null;
            }

            try
            {
                var bytes = await _httpClient.GetByteArrayAsync(imageUrl);
                await File.WriteAllBytesAsync(filePath, bytes);
                _logger.LogInformation("Cached avatar for {personality} at {path}", personality, filePath);
                return relativePath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cache avatar for {personality}", personality);
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
