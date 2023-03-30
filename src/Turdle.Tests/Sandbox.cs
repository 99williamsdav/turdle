using Microsoft.Extensions.Logging;
using Moq;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Turdle.Bots;
using Turdle.ChatGpt;
using Turdle.Models;
using Xunit;

namespace Turdle.Tests
{
    public class Sandbox
    {
        private readonly ChatGptClient _chatGptClient;
        private readonly ChatGptService _chatGptService;
        private readonly WordService _wordService;
        public Sandbox()
        {
            _chatGptClient = new ChatGptClient(Mock.Of<ILogger<ChatGptClient>>());
            _chatGptService = new ChatGptService(_chatGptClient, Mock.Of<ILogger<ChatGptService>>());
            _wordService = new WordService();
        }

        [Fact(Skip = "ad-hoc sandbox")]
        public async Task Go()
        {
            var answerListType = AnswerListType.FiveLetterEasy;
            var personality = "homer simpson";
            var bot = new ChatGptPersonalityBot(personality, _chatGptService, _wordService);
            //var smackTalk = await bot.GetSmackTalk();

            var correctAnswer = _wordService.GetRandomWord(answerListType);
            var wordLength = correctAnswer.Length;

            var board = new Board();

            var firstGuess = await bot.SelectOpeningWord(wordLength);
            board.AddRow(firstGuess, correctAnswer, 1, 0, 1, null);

            while (!board.IsFinished)
            {
                var (nextGuess, speed) = await bot.SelectWord(wordLength, board, correctAnswer);
                board.AddRow(nextGuess, correctAnswer, 1, 0, 1, null);
            }
        }
    }
}
