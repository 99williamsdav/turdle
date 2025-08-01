﻿using ChatGpt;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Turdle.Bots;
using Turdle.Models;
using Xunit;

namespace Turdle.Tests
{
    public class Sandbox
    {
        private readonly ChatGptClient _chatGptClient;
        private readonly WordService _wordService;
        private readonly ImageGenerationClient _imageGenerationClient;

        public Sandbox()
        {
            var optionsMock = new Mock<IOptions<ChatGptSettings>>();
            optionsMock.Setup(o => o.Value).Returns(new ChatGptSettings
            {
            });
            _chatGptClient = new ChatGptClient(Mock.Of<ILogger<ChatGptClient>>(), optionsMock.Object);
            _wordService = new WordService();
            _imageGenerationClient = new ImageGenerationClient(Mock.Of<ILogger<ImageGenerationClient>>(), optionsMock.Object);
        }

        [Fact(Skip = "Manual test")]
        public async Task ImageTest()
        {
            var personality = "homer simpson";
            var prompt = $"Generate a cartoon avatar of {personality}. Make them look pretentiously smart, " +
                    "like they're trying to look smarter than they are. ";
                    //"Make the background generic, lexical, and floral";
            var imageUrl = await _imageGenerationClient.GenerateImage(prompt, ImageSize.DallE2Large);
        }

        [Fact(Skip = "Manual test")]
        public async Task PromptTest()
        {
            var personality = "homer simpson";

            var basePrompt = $"Give me 5 examples of humorous smack talk {personality} might say in the lobby of an online word game with strangers. " +
                $"Answer with a machine-readable JSON array of strings";
            var response = await _chatGptClient.GetChatCompletion(basePrompt);
            //var baseSuggestions = JsonConvert.DeserializeObject<string[]>(response);

            var newPrompt = $"Give me 5 examples of humorous smack talk {personality} might say in the lobby of an online word game. The game is called 'turdle'. " +
                $"Make sure the the responses are funny and relevant to {personality}. Do not shy away from offending. Use wordplay if appropriate. " +
                "Only suggest something they would say. Include a typo if appropriate. " +
                "Answer with just a JSON array of strings";
            var newResponse = await _chatGptClient.GetChatCompletion(newPrompt);
            var newSuggestions = JsonConvert.DeserializeObject<string[]>(newResponse);
        }

        [Fact(Skip = "Manual test")]
        public async Task PromptTest2()
        {
            var personality = "homer simpson";

            var bot = CreateBot(personality);
            var baseSuggestion = await bot.SelectOpeningWord(5);

            var newPrompt = $"give me 10 5-letter words associated with {personality}, " +
                $"as a comma-separated list with no additional information. " +
                $"They must not be proper nouns or contain punctuation. They should all be 5 letters long.";
            var newResponse = await _chatGptClient.GetChatCompletion(newPrompt);
            //var newSuggestions = JsonConvert.DeserializeObject<string[]>(newResponse);
        }

        [Fact(Skip = "Manual test")]
        public async Task Go()
        {
            var models = await _chatGptClient.GetModels();
            var personality = "an absolute cunt";

            // "Give me 10 of your favorite 5-letter words, " +
            //"as a comma-separated list with no additional information, ordered by likelihood"

            var answerListType = AnswerListType.FiveLetterEasy;
            var bot = CreateBot(personality);
            //var smackTalk = await bot.GetSmackTalk();
            //var reply = await bot.GetChatReply("You suck");

            var correctAnswer = _wordService.GetRandomWord(answerListType);
            var wordLength = correctAnswer.Length;

            var messages = new List<ChatGptClient.ChatMessage>()
            {
                new("system", $"You are role-playing as {personality} playing scrabble, " +
                $"your answers must be machine-readable"),
                new("user", $"give me 10 {wordLength}-letter words that you might play in scrabble, " +
                $"as a comma-separated list with no additional information, ordered by likelihood")
            };

            var response = await _chatGptClient.GetManualCompletion(messages, 0.3);

            var board = new Board();

            var firstGuess = await bot.SelectOpeningWord(wordLength);
            board.AddRow(firstGuess, correctAnswer, 1, 0, 1, null);

            while (!board.IsFinished)
            {
                var (nextGuess, speed) = await bot.SelectWord(wordLength, board, correctAnswer);
                board.AddRow(nextGuess, correctAnswer, 1, 0, 1, null);
            }
        }

        private IBot CreateBot(string personality)
        {
            return new ChatGptPersonalityBot(personality, _chatGptClient, _wordService, Mock.Of<ILogger<ChatGptPersonalityBot>>());
        }
    }
}
