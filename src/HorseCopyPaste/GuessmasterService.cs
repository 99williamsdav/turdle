using ChatGpt;
using HorseCopyPaste.Models;
using Microsoft.Extensions.Logging;

namespace HorseCopyPaste
{
    public class GuessmasterService
    {
        private readonly ChatGptClient _chatGptClient;
        private readonly ILogger<GuessmasterService> _logger;

        public GuessmasterService(ChatGptClient chatGptClient, ILogger<GuessmasterService> logger)
        {
            _chatGptClient = chatGptClient;
            _logger = logger;
        }

        public async Task<string[]> GetGuesses(Game state, string clue, int guessCount)
        {
            var optionCards = state.GetRemainingCards();
            var options = optionCards.Select(x => x.Word);

            var optionList = string.Join(",", options);
            var prompt = $"Which of the following comma-separated words are most connected to the clue '{clue}':\n{optionList}\n\n" +
                $"Please answer with only a comma-separated list of up to a maximum of {guessCount} words." + //, you can pick fewer if some links are very weak. " +
                $"Order your answers by the strength of the connection to the clue";
            var response = await _chatGptClient.GetChatCompletion(prompt);

            var selections = response.Replace(" ", "").Split(",");

            return selections.Intersect(options).Take(guessCount).ToArray();
        }
    }
}