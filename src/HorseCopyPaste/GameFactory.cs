using HorseCopyPaste.Models;
using Microsoft.Extensions.Logging;

namespace HorseCopyPaste
{
    public class GameFactory
    {
        private const int DefaultCardCount = 25;
        private const int DefaultNeutralCount = 7;
        private const int DefaultInstadeathCount = 1;

        private readonly WordService _wordService;

        private readonly ILogger<Game> _logger;

        public GameFactory(WordService wordService, ILogger<Game> logger)
        {
            _wordService = wordService;
            _logger = logger;
        }

        public Game Create(int teamCount, int cardCount = DefaultCardCount, int neutralCount = DefaultNeutralCount, int instaDeathCount = DefaultInstadeathCount)
        {
            var teamCardCount = cardCount - neutralCount - instaDeathCount;

            if (teamCardCount < teamCount * 2)
                throw new InvalidOperationException("Not enough cards to cover all players");
            
            var words = new Queue<string>(_wordService.GetWords(cardCount));

            var cards = new List<Card>();

            for (var i = 0; i < instaDeathCount; i++)
                cards.Add(new Card(words.Dequeue(), CardType.Instadeath));

            for (var i = 0; i < neutralCount; i++)
                cards.Add(new Card(words.Dequeue(), CardType.Neutral));

            while (words.Count > 0)
            {
                for (var i = 1; i <= teamCount; i++)
                {
                    if (!words.TryDequeue(out var word))
                        break;
                    cards.Add(new Card(word, i));
                }
            }

            var gameState = new Game(cards.ToArray(), _logger);

            return gameState;
        }
    }
}
