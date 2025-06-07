using ChatGpt;
using HorseCopyPaste.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace HorseCopyPaste
{
    public class SpymasterService
    {
        private readonly ChatGptClient _chatGptClient;
        private readonly ILogger<SpymasterService> _logger;

        public SpymasterService(ChatGptClient chatGptClient, ILogger<SpymasterService> logger)
        {
            _chatGptClient = chatGptClient;
            _logger = logger;
        }

        public async Task<(string Clue, int Count)> GetClue(Game state, int team)
        {
            var targetCards = state.GetTargetCards(team);
            var cardsToAvoid = state.GetCardsToAvoid(team);
            var neutralCards = state.GetInPlayNeutralCards();

            var targets = string.Join(",", targetCards.Select(x => x.Word));
            var toAvoid = string.Join(",", cardsToAvoid.Concat(neutralCards).Select(x => x.Word));
            var prompt = $"Think of a single-word clue that links as many of the following comma-separated words as possible," +
                $"you only need to link a portion of them:\n{targets}\n\n" +
                $"The clue must avoid relating to any of the following comma-separated words:\n{toAvoid}\n\n" +
                $"Answer only with a JSON object with a string property called 'clue' and an array property called 'targets' " +
                $"where each object has properties 'word', 'link_description' and a 'link_strength' number between 1 and 10. " +
                $"Additionally, include an array property called 'to_avoid' listing all the words to avoid, with a description of how they relate to the clue. " +
                $"This should also be an array of objects with the properties 'word', 'link_description' and a 'link_strength' number between 1 and 10.";
                //$"Only include targets that can be linked to the clue in a non-tenuous way. The clue cannot be one of the words.";
            var response = await _chatGptClient.GetChatCompletion(prompt);
            var clue = JsonConvert.DeserializeObject<GetClueResponse>(response);

            // TODO retry if the clue is "connection"

            return (clue.clue, clue.targets.Length);
        }

        private record GetClueResponse (string clue, ClueTarget[] targets, ClueTarget[] to_avoid);
        private record ClueTarget(string word, string link_description, int link_strength);
    }
}