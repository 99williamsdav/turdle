using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace ChatGpt
{
    public class ImageGenerationClient
    {
        private const string ImageGenerationApiUri = "https://api.openai.com/v1/images/generations";
        private const string Model = "dall-e-2";

        private readonly HttpClient _httpClient;
        private readonly ILogger<ImageGenerationClient> _logger;
        private readonly ChatGptSettings _settings;

        public ImageGenerationClient(ILogger<ImageGenerationClient> logger, IOptions<ChatGptSettings> settings)
        {
            _settings = settings.Value;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_settings.ApiKey}");
            _logger = logger;
        }

        public ImageGenerationClient(ILogger<ImageGenerationClient> logger, IOptions<ChatGptSettings> settings, string apiKey)
        {
            _settings = settings.Value;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
            _logger = logger;
        }

        public async Task<string?> GenerateImage(
            string prompt,
            int n = 1,
            ImageSize size = ImageSize.Square,
            ImageQuality quality = ImageQuality.Auto,
            string model = Model,
            bool transparentBackground = true)
        {
            var request = new ImageGenerationRequest(
                model,
                prompt,
                n,
                size.ToApiString(),
                quality.ToApiString(),
                transparentBackground);
            _logger.LogInformation("Calling OpenAI Image Generation API.");

            var response = await SendRequest<ImageGenerationRequest, ImageGenerationResponse>(ImageGenerationApiUri, request);

            return response.data.FirstOrDefault()?.url;
        }

        private async Task<TResponse> SendRequest<TRequest, TResponse>(string url, TRequest request)
        {
            var json = JsonConvert.SerializeObject(request);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(url, content);
            var responseContent = await response.Content.ReadAsStringAsync();

            var result = JsonConvert.DeserializeObject<TResponse>(responseContent);
            return result!;
        }

        private record ImageGenerationRequest(
            string model,
            string prompt,
            int n,
            string size,
            string quality,
            bool transparent);
        private record ImageGenerationResponse(ImageData[] data);
        private record ImageData(string url);
    }

    public enum ImageSize
    {
        Square,
        Landscape,
        Portrait,
        Auto
    }

    public enum ImageQuality
    {
        Low,
        Medium,
        High,
        Auto
    }

    internal static class ImageEnumExtensions
    {
        public static string ToApiString(this ImageSize size) => size switch
        {
            ImageSize.Square => "1024x1024",
            ImageSize.Landscape => "1536x1024",
            ImageSize.Portrait => "1024x1536",
            _ => "auto"
        };

        public static string ToApiString(this ImageQuality quality) => quality switch
        {
            ImageQuality.Low => "low",
            ImageQuality.Medium => "medium",
            ImageQuality.High => "high",
            _ => "auto"
        };
    }
}
