using System.Reflection;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Turdle.Models;
using Turdle.Utils;

namespace Turdle;

public class WordService
{
    private readonly IDictionary<int, string[]> _possibleAnswers = new Dictionary<int, string[]>();
    private readonly IDictionary<int, string[]> _naughtyWords = new Dictionary<int, string[]>();
    private readonly IDictionary<int, string[]> _botWords = new Dictionary<int, string[]>();
    private readonly IDictionary<int, HashSet<string>> _acceptedWordsByLength = new Dictionary<int, HashSet<string>>();
    private HashSet<string> _acceptedWords;
    private string[] _possibleWordleAnswers = new string[0];
    private string[] _xmasWords;
    private IDictionary<string, string> _wordEmojis;

    public WordService()
    {
        PopulateWords(4);
        PopulateWords(5);
        PopulateWords(6);
        PopulateWordleAnswers();
        PopulateXmasAnswers();

        _acceptedWords = _acceptedWordsByLength[4]
            .Concat(_acceptedWordsByLength[5])
            .Concat(_acceptedWordsByLength[6])
            .Concat(_xmasWords)
            .ToHashSet();

        PopulateEmojiWords();
    }

    public string[] GetDictionary(int wordLength)
    {
        return _acceptedWordsByLength[wordLength].ToArray();
    }

    public string GetRandomWord(AnswerListType answerListType)
    {
        var answerList = answerListType switch
        {
            AnswerListType.FourLetter => _possibleAnswers[4],
            AnswerListType.FiveLetterEasy => _possibleAnswers[5],
            AnswerListType.FiveLetterWordle => _possibleWordleAnswers,
            AnswerListType.SixLetter => _possibleAnswers[6],
            AnswerListType.Random => 
                _possibleAnswers[4].Concat(_possibleAnswers[5]).Concat(_possibleAnswers[6]).ToArray(),
            AnswerListType.RandomNaughty =>
                _naughtyWords[4].Concat(_naughtyWords[5]).Concat(_naughtyWords[6]).ToArray(),
            AnswerListType.Xmas => _xmasWords,
        };

        return answerList.PickRandom();
    }

    public bool IsWordAccepted(string word)
    {
        return _acceptedWords.Contains(word);
    }

    public string[] GetPossibleValidGuesses(Board board, int length) => GetPossibleValidGuesses(board.CorrectLetters,
        board.PresentLetters, board.AbsentLetters, board.PresentLetterCounts, length);

    public string[] GetPossibleValidGuesses(HashSet<Board.LetterPosition> correctLetters,
        HashSet<Board.LetterPosition> presentLetters, HashSet<char> absentLetters, Dictionary<char, int> presentLetterCounts, int length)
    {
        // remove unknown present letters from absent letters to handle case where only the 2nd instance was absent
        var presentUnknownLetters = presentLetterCounts
            .Where(x => x.Value > correctLetters.Count(l => l.Letter == x.Key))
            .Select(x => x.Key)
            .ToArray();
        absentLetters = absentLetters.Except(presentUnknownLetters).ToHashSet();
        
        var regex = "";
        for (var i = 0; i < length; i++)
        {
            if (correctLetters.Any(x => x.Position == i))
            {
                regex += correctLetters.Single(x => x.Position == i).Letter;
                continue;;
            }

            var lettersNotInThisPosition = presentLetters.Where(x => x.Position == i).Select(x => x.Letter).ToArray();
            char[] possibleLetters = Const.Alphabet
                .Except(absentLetters)
                .Except(lettersNotInThisPosition)
                .ToArray();
            regex += $"[{new string(possibleLetters)}]";
        }

        bool HasAllPresentLetters(string word)
        {
            foreach (var letterGrp in presentLetterCounts)
            {
                var presentCount = word.Count(x => x == letterGrp.Key);
                if (presentCount < letterGrp.Value)
                    return false;
                if (absentLetters.Contains(letterGrp.Key) && presentCount > letterGrp.Value)
                    return false;
            }

            return true;
        }

        var wordList = _botWords[length]; //_acceptedWordsByLength[length]

        var validWords = wordList
            .Where(word => Regex.IsMatch(word, regex))
            .Where(HasAllPresentLetters)
            .Distinct().ToArray();
        
        return validWords;
    }

    public string? GetWordEmoji(string word)
    {
        return _wordEmojis.TryGetValue(word, out var ret) ? ret : null;
    }

    private void PopulateWords(int length)
    {
        var assembly = Assembly.GetExecutingAssembly();
        
        var answerListFilename = $"Turdle.Resources._{length}_PossibleAnswers.json";
        using (Stream stream = assembly.GetManifestResourceStream(answerListFilename))
        using (StreamReader reader = new StreamReader(stream))
        {
            string jsonFile = reader.ReadToEnd();
            _possibleAnswers[length] = JsonConvert.DeserializeObject<string[]>(jsonFile).Distinct().ToArray();
            var letterCounts = 
                jsonFile.GroupBy(x => x)
                    .OrderBy(x => x.Key)
                    .ToDictionary(x => x.Key, x => x.Count());
        }
        
        var naughtyListFilename = $"Turdle.Resources._{length}_NaughtyAnswers.json";
        using (Stream stream = assembly.GetManifestResourceStream(naughtyListFilename))
        using (StreamReader reader = new StreamReader(stream))
        {
            string jsonFile = reader.ReadToEnd();
            _naughtyWords[length] = JsonConvert.DeserializeObject<string[]>(jsonFile).Distinct().ToArray();
        }

        _possibleAnswers[length] = _possibleAnswers[length].Concat(_naughtyWords[length]).Concat(_naughtyWords[length]).ToArray();
        
        var dictionaryFilename = $"Turdle.Resources._{length}_Dictionary.json";
        string[] dictionary;
        using (Stream stream = assembly.GetManifestResourceStream(dictionaryFilename))
        using (StreamReader reader = new StreamReader(stream))
        {
            string jsonFile = reader.ReadToEnd();
            dictionary = JsonConvert.DeserializeObject<string[]>(jsonFile);
        }
        
        var removedAnswersFilename = $"Turdle.Resources._{length}_RemovedAnswers.json";
        string[] removedAnswers = Array.Empty<string>();
        using (Stream stream = assembly.GetManifestResourceStream(removedAnswersFilename))
        {
            if (stream != null)
            {
                using (StreamReader reader = new StreamReader(stream))
                {
                    string jsonFile = reader.ReadToEnd();
                    removedAnswers = JsonConvert.DeserializeObject<string[]>(jsonFile);
                }
            }
        }

        _acceptedWordsByLength[length] = new HashSet<string>(dictionary.Concat(_possibleAnswers[length]).Concat(removedAnswers).Concat(_naughtyWords[length]));

        var reasonableListFilename = $"Turdle.Resources._{length}_ReasonableWords.json";
        using (Stream stream = assembly.GetManifestResourceStream(reasonableListFilename))
        using (StreamReader reader = new StreamReader(stream))
        {
            string jsonFile = reader.ReadToEnd();
            _botWords[length] = JsonConvert.DeserializeObject<string[]>(jsonFile).Distinct().ToArray();
        }
    }

    private void PopulateWordleAnswers()
    {
        var assembly = Assembly.GetExecutingAssembly();

        var answerListFilename = $"Turdle.Resources._5_PossibleWordleAnswers.json";
        using (Stream stream = assembly.GetManifestResourceStream(answerListFilename))
        using (StreamReader reader = new StreamReader(stream))
        {
            string jsonFile = reader.ReadToEnd();
            _possibleWordleAnswers = JsonConvert.DeserializeObject<string[]>(jsonFile).Distinct().ToArray();
        }
    }

    private void PopulateXmasAnswers()
    {
        var assembly = Assembly.GetExecutingAssembly();

        var answerListFilename = $"Turdle.Resources.XmasAnswers.json";
        using (Stream stream = assembly.GetManifestResourceStream(answerListFilename))
        using (StreamReader reader = new StreamReader(stream))
        {
            string jsonFile = reader.ReadToEnd();
            _xmasWords = JsonConvert.DeserializeObject<string[]>(jsonFile).Distinct().ToArray();
        }
    }

    private void PopulateEmojiWords()
    {
        var assembly = Assembly.GetExecutingAssembly();

        var filename = $"Turdle.Resources.WordEmojis.json";
        using (Stream stream = assembly.GetManifestResourceStream(filename))
        using (StreamReader reader = new StreamReader(stream))
        {
            string jsonFile = reader.ReadToEnd();
            _wordEmojis = JsonConvert.DeserializeObject<IDictionary<string, string>>(jsonFile);
        }
    }
}

public enum AnswerListType
{
    FourLetter,
    FiveLetterEasy,
    FiveLetterWordle,
    SixLetter,
    Random,
    RandomNaughty,
    Xmas
}
