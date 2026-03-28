using System.Buffers.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace ChatGpt
{
    public class ImageGenerationClient
    {
        private const string ImageGenerationApiUri = "https://api.openai.com/v1/images/generations";
        private const string Model = "gpt-image-1-mini"; //"dall-e-2";

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

        /// <summary>
        /// Generates an image for the given prompt.
        /// </summary>
        /// <returns>The png data</returns>
        public async Task<byte[]?> GenerateImage(
            string prompt,
            ImageSize size = ImageSize.Gpt1MiniSquare,
            ImageQuality quality = ImageQuality.Auto,
            string model = Model,
            bool transparentBackground = true)
        {
            var request = new ImageGenerationRequest(
                model,
                prompt,
                1,
                size.ToApiString(),
                quality.ToApiString(),
                //transparentBackground,
                "low"
                );
            _logger.LogInformation("Calling OpenAI Image Generation API.");

            var response = await SendRequest<ImageGenerationRequest, ImageGenerationResponse>(ImageGenerationApiUri, request);

            var imageData = response.data?.FirstOrDefault();
            if (imageData?.B64Json != null)
            {
                byte[] data = Convert.FromBase64String(imageData.B64Json);
                return data;
            }
            else if (imageData?.Url != null)
            {
                var data = await _httpClient.GetByteArrayAsync(imageData?.Url);
                return data;
            }
            
            throw new InvalidOperationException("No image data available");
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
            //bool transparent,
            string moderation
            );
        private record ImageGenerationResponse(ImageData[] data);
        private class ImageData
        {
            [JsonProperty("url")]
            public string? Url { get; init; }
            [JsonProperty("revised_prompt")]
            public string? RevisedPrompt { get; init; }
            [JsonProperty("b64_json")]
            public string? B64Json { get; init; }
        }
    }

    public enum ImageSize
    {
        DallE2Small,
        DallE2Medium,
        DallE2Large,
        DallE3Square,
        DallE3Landscape,
        DallE3Portrait,
        Gpt1MiniSquare,
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
            ImageSize.DallE2Small => "256x256",
            ImageSize.DallE2Medium => "512x512",
            ImageSize.DallE2Large => "1024x1024",
            ImageSize.DallE3Square => "1024x1024",
            ImageSize.DallE3Landscape => "1792x1024",
            ImageSize.DallE3Portrait => "1024x1792",
            ImageSize.Gpt1MiniSquare => "1024x1024",
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
