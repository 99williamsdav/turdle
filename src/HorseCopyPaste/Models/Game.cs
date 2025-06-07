using log4net.Core;
using Microsoft.Extensions.Logging;

namespace HorseCopyPaste.Models
{
    public class Game
    {
        public Card[] Cards { get; init; }

        public Card[] GetTargetCards(int team) => Cards.Where(x => !x.IsRevealed && x.Team == team).ToArray();
        public Card[] GetCardsToAvoid(int team) => 
            Cards
                .Where(x => !x.IsRevealed)
                .Where(x => x.Type == CardType.Team ? x.Team != team : x.Type == CardType.Instadeath)
                .ToArray();
        public Card[] GetInPlayNeutralCards() => Cards.Where(x => !x.IsRevealed && x.Type == CardType.Neutral).ToArray();
        public Card[] GetRemainingCards() => Cards.Where(x => !x.IsRevealed).ToArray();

        public int TeamCount { get; }
        public int TeamToStart { get; }
        public int TeamToGoNext { get; private set; }
        public int[] Scores { get; }
        public int GetRemainingCount(int team) => Cards.Count(x => !x.IsRevealed && x.Team == team);
        public GameStatus Status { get; private set; }
        public int? WinningTeam { get; private set; }

        public List<Turn> Turns { get; } = new List<Turn>();

        private readonly ILogger<Game> _logger;


        public Game(Card[] cards, ILogger<Game> logger)
        {
            Cards = cards;
            var teamCards = cards.Where(x => x.Type == CardType.Team).GroupBy(x => x.Team.Value);
            TeamCount = teamCards.Count();
            Scores = new int[TeamCount + 1];
            var maxCardAmount = teamCards.Max(x => x.Count());
            TeamToGoNext = teamCards.First(x => x.Count() == maxCardAmount).Key;
            Status = GameStatus.Playing;
            _logger = logger;
        }

        public Card TouchCard(int team, string word, string clue)
        {
            if (team != TeamToGoNext)
                throw new InvalidOperationException($"Not Team {team}'s go");

            var card = Cards.SingleOrDefault(x => x.Word == word);
            if (card == null)
                throw new InvalidOperationException($"'{word}' is not a card");

            card.Reveal();
            if (card.Type == CardType.Team)
            {
                Scores[card.Team!.Value] += 1;
                if (Cards.Where(x => x.Team == card!.Team).All(x => x.IsRevealed))
                {
                    Status = GameStatus.Finished;
                    WinningTeam = card!.Team;
                }
            }

            if (card.Type == CardType.Instadeath)
            {
                Status = GameStatus.Finished;
                // TODO work out what to do when there are >2 teams
                WinningTeam = team == TeamCount ? 1 : team + 1;
                // DEATH
            }

            if (card.Team != team)
                TeamToGoNext = team < TeamCount ? team + 1 : 1;

            var turn = new Turn(card, team, clue);
            Turns.Add(turn);

            _logger.LogInformation($"{turn}. Remaining: ");
            for (var i = 1; i <= TeamCount; i++)
            {
                var remaining = GetRemainingCount(i);
                _logger.LogInformation($"{(TeamColour)i} {remaining}");
            }

            return card;
        }

        public void FinishTurn(int team)
        {
            if (team != TeamToGoNext)
                throw new InvalidOperationException($"Not Team {team}'s go");

            TeamToGoNext = team < TeamCount ? team + 1 : 1;
        }
    }

    public enum GameStatus
    {
        None = 0,
        Playing = 1,
        Finished = 2
    }

    public class Card
    {
        public string Word { get; init; }
        public bool IsRevealed { get; private set; } = false;
        public CardType Type { get; init; }
        public int? Team { get; }
        public TeamColour? TeamColour => Team == null ? null : (TeamColour)Team;

        public Card(string word, CardType type)
        {
            Word = word;
            Type = type;
        }

        public Card(string word, int team)
        {
            Word = word;
            Type = CardType.Team;
            Team = team;
        }

        public void Reveal() => IsRevealed = true;

        public override string ToString() => $"{Word} {Type} {(Type == CardType.Team ? TeamColour : "")} {(IsRevealed ? "👁" : "")}";
    }

    public record Turn(Card Card, int Team, string Clue)
    {
        public override string ToString()
        {
            var cardDescription = Card.Type == CardType.Team ? ((TeamColour)Card.Team!).ToString() : Card.Type.ToString();
            return $"{(TeamColour)Team} tapped {Card.Word} from the clue '{Clue}', which was {cardDescription}";
        }
    }

    public enum CardType
    {
        None,
        Team,
        Neutral,
        Instadeath
    }

    public enum TeamColour
    {
        None = 0,
        Red = 1,
        Blue = 2,
        Yellow = 3,
        Green = 4,
        Purple = 5,
        Orange = 6
    }
}
