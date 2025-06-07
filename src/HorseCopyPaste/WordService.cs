using System.Reflection;
using Turdle.Utils;

namespace HorseCopyPaste
{
    public class WordService
    {
        private string[] _words;

        public WordService()
        {
            var assembly = Assembly.GetExecutingAssembly();

            var filename = "HorseCopyPaste.Resources.CodenamesWords.txt";
            using (Stream stream = assembly.GetManifestResourceStream(filename))
            using (StreamReader reader = new StreamReader(stream))
            {
                var words = new List<string>();
                while (reader.Peek() != -1)
                    words.Add(reader.ReadLine() ?? "");
                _words = words.ToArray();
            }
        }

        public string GetWord() => _words.PickRandom();

        public string[] GetWords(int count)
        {
            var words = new List<string>();

            while (words.Count < count)
            {
                var word = GetWord();
                if (!words.Contains(word))
                    words.Add(word);
            }

            return words.ToArray();
        }
    }
}
