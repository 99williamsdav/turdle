﻿using System.Reflection;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Turdle.Models;
using Turdle.Utils;

namespace Turdle;

public class WordService
{
    private readonly IDictionary<int, string[]> _possibleAnswers = new Dictionary<int, string[]>();
    private readonly IDictionary<int, string[]> _naughtyWords = new Dictionary<int, string[]>();
    private readonly IDictionary<int, HashSet<string>> _acceptedWords = new Dictionary<int, HashSet<string>>();
    private string[] _possibleWordleAnswers = new string[0];

    public WordService()
    {
        PopulateWords(4);
        PopulateWords(5);
        PopulateWords(6);
        PopulateWordleAnswers();
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
                _possibleAnswers[4].Concat(_possibleAnswers[5]).Concat(_possibleAnswers[6]).Concat(_possibleWordleAnswers).ToArray(),
        };

        return answerList.PickRandom();
    }

    public bool IsWordAccepted(string word)
    {
        return _acceptedWords[word.Length].Contains(word);
    }

    public string[] GetPossibleValidGuesses(Board board, int length) => GetPossibleValidGuesses(board.CorrectLetters,
        board.PresentLetters, board.AbsentLetters, board.PresentLetterCounts, length);

    public string[] GetPossibleValidGuesses(HashSet<Board.LetterPosition> correctLetters,
        HashSet<Board.LetterPosition> presentLetters, HashSet<char> absentLetters, Dictionary<char, int> presentLetterCounts, int length)
    {
        // remove present letters from absent letters to handle case where only the 2nd instance was absent
        absentLetters = absentLetters.Except(presentLetterCounts.Select(x => x.Key)).ToHashSet();
        
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

        var validWords = _acceptedWords[length]
            .Where(word => Regex.IsMatch(word, regex))
            .Where(HasAllPresentLetters).ToArray();
        
        return validWords;
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

        _acceptedWords[length] = new HashSet<string>(dictionary.Concat(_possibleAnswers[length]).Concat(removedAnswers).Concat(_naughtyWords[length]));
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
}

public enum AnswerListType
{
    FourLetter,
    FiveLetterEasy,
    FiveLetterWordle,
    SixLetter,
    Random
}
