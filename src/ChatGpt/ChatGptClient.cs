using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System.Web;

namespace ChatGpt
{
    public class ChatGptClient
    {
        private const string CompletionApiUri = "https://api.openai.com/v1/chat/completions";
        private const string ModelApiUri = "https://api.openai.com/v1/models";

        private const decimal DollarsPerToken = 0.8m / 1_000_000;

        private const string Model = "gpt-4.1-nano";// "gpt-3.5-turbo";

        private readonly HttpClient _httpClient;
        private readonly ILogger<ChatGptClient> _logger;
        private readonly ChatGptSettings _settings;

        public ChatGptClient(ILogger<ChatGptClient> logger, IOptions<ChatGptSettings> settings)
        {
            _settings = settings.Value;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_settings.ApiKey}");
            _logger = logger;
        }

        public ChatGptClient(ILogger<ChatGptClient> logger, IOptions<ChatGptSettings> settings, string apiKey)
        {
            _settings = settings.Value;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
            _logger = logger;
        }

        public async Task<string> GetManualCompletion(IList<ChatMessage> messages, double temperature = 0.5, string model = Model)
        {
            var request = new ChatCompletionRequest(model, messages, temperature);
            _logger.LogInformation($"Calling ChatGPT.");
            var startTime = DateTime.UtcNow;

            var responseJson = await SendRequest(CompletionApiUri, request);
            var response = JsonConvert.DeserializeObject<ChatCompletionResponse>(responseJson);

            var duration = DateTime.UtcNow - startTime;
            var price = DollarsPerToken * response.usage.total_tokens;
            _logger.LogInformation($"ChatGPT tokens {response.usage.total_tokens} (${price:N4}), duration: {duration.TotalMilliseconds:N0}ms");

            var completion = response.choices.Single().message.content;

            return completion;
        }

        public async Task<string> GetChatCompletion(string message)
        {
            var request = new ChatCompletionRequest(Model, new[] { new ChatMessage("user", message) }, 0.5);

            try
            {
                _logger.LogInformation($"Calling ChatGPT with prompt: {message.Substring(0, 20)}...");
                var startTime = DateTime.UtcNow;

                var responseJson = await SendRequest(CompletionApiUri, request);
                var response = JsonConvert.DeserializeObject<ChatCompletionResponse>(responseJson);

                var duration = DateTime.UtcNow - startTime;
                var price = DollarsPerToken * response.usage.total_tokens;
                _logger.LogInformation($"ChatGPT tokens {response.usage.total_tokens} (${price:N4}), duration: {duration.TotalMilliseconds:N0}ms");

                var completion = response.choices.Single().message.content;

                return completion;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                throw;
            }
        }

        public async Task<string> GetModels()
        {
            var response = await _httpClient.GetAsync(ModelApiUri);
            var responseContent = await response.Content.ReadAsStringAsync();

            return responseContent;
        }

        private async Task<string> SendRequest<TRequest>(string url, TRequest request)
        {
            var json = JsonConvert.SerializeObject(request);

            // HTML-encode the JSON
            var encodedJson = HttpUtility.HtmlEncode(json);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(url, content);
            var responseContent = await response.Content.ReadAsStringAsync();

            // Get the response content
            return responseContent;
        }

        public record ChatCompletionRequest(string model, IList<ChatMessage> messages, double temperature = 1);

        public record ChatMessage(string role, string content);

        private record ChatCompletionResponse(string id, string Object, Choice[] choices, int created, string model, Usage usage, string finish_reason, int index);

        private record Usage(int prompt_tokens, int completion_tokens, int total_tokens);

        private record Choice(ChatMessage message);
    }

    public class ChatGptSettings
    {
        public string ApiKey { get; set; }
    }
}
