﻿using Newtonsoft.Json;
using System.Web;

namespace Turdle.ChatGpt
{
    public class ChatGptClient
    {
        private const string ApiKey = "";
        private const string CompletionApiUri = "https://api.openai.com/v1/chat/completions";
        private const string ModelApiUri = "https://api.openai.com/v1/models";

        private const string Model = "gpt-3.5-turbo";

        private readonly HttpClient _httpClient;
        private readonly ILogger<ChatGptClient> _logger;

        public ChatGptClient(ILogger<ChatGptClient> logger)
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {ApiKey}");
            _logger = logger;
        }

        public async Task<string> GetChatCompletion(string message)
        {
            var request = new ChatCompletionRequest(Model, new[] { new ChatMessage("user", message) });

            try
            {
                var startTime = DateTime.UtcNow;

                var responseJson = await SendRequest(CompletionApiUri, request);
                var response = JsonConvert.DeserializeObject<ChatCompletionResponse>(responseJson);

                var duration = DateTime.UtcNow - startTime;
                _logger.LogInformation($"ChatGPT tokens {response.usage.total_tokens}, duration: {duration.TotalMilliseconds:N0}ms");

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

        private record ChatCompletionRequest(string model, ChatMessage[] messages);

        private record ChatMessage(string role, string content);

        private record ChatCompletionResponse(string id, string Object, Choice[] choices, int created, string model, Usage usage, string finish_reason, int index);

        private record Usage(int prompt_tokens, int completion_tokens, int total_tokens);

        private record Choice(ChatMessage message);
    }
}
