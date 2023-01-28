using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Reflection;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Turdle.Tools
{
    internal static class ParseEmojiSynonyms
    {
        public static void OutputWordEmojis()
        {
            var emojiSynonyms = GetEmojiSynonyms();

            var wordEmojis = new Dictionary<string, string>();

            var rejected = new List<(string, string)>();

            foreach (var emoji in emojiSynonyms)
            {
                foreach (var word in emoji.Synonyms)
                {
                    if (word.Length < 4 || word.Length > 6)
                        continue;

                    if (!wordEmojis.TryAdd(word, emoji.Emoji))
                    {
                        var existing = wordEmojis[word];
                        rejected.Add((word, $"{existing}>{emoji.Emoji}"));
                    }
                }
            }

            var serialised = JsonConvert.SerializeObject(wordEmojis);
        }

        private static List<EmojiSynonyms> GetEmojiSynonyms()
        {
            var ret = new List<EmojiSynonyms>();

            var assembly = Assembly.GetExecutingAssembly();

            var answerListFilename = $"Turdle.Tools.Resources.emoji-synonyms.csv";
            using (Stream stream = assembly.GetManifestResourceStream(answerListFilename))
            using (StreamReader reader = new StreamReader(stream))
            {
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    var values = line!.Split(',');
                    var emoji = values[0].Split(' ')[0];
                    var synonyms = values.Skip(1).Select(x => x.Trim().ToUpper()).ToArray();
                    ret.Add(new EmojiSynonyms(emoji, synonyms));
                }
            }

            return ret;
        }
    }

    public record EmojiSynonyms(string Emoji, string[] Synonyms);
}
