using Newtonsoft.Json;
using System.Reflection;
using System.Resources;

namespace Turdle.Tools
{
    internal static class GetReasonableWords
    {
        public static void ProduceReasonableWordList(int wordLength)
        {
            var words = LoadWords();
            var batch = words.Where(x => x.Length == wordLength).Select(x => x.ToUpper()).ToArray();
            var json = JsonConvert.SerializeObject(batch).Replace(" ", "");
        }

        private static List<string> LoadWords()
        {
            var ret = new List<string>();

            var assembly = Assembly.GetExecutingAssembly();

            var answerListFilename = $"Turdle.Tools.Resources.Top20kWords.txt";
            using (Stream stream = assembly.GetManifestResourceStream(answerListFilename))
            using (StreamReader reader = new StreamReader(stream))
            {
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    ret.Add(line);
                }
            }

            return ret;
        }
    }
}
