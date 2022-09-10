using Newtonsoft.Json;

namespace Turdle;

public class WordAnalysisService : IWordAnalysisService
{
    private const string Url = "https://api.datamuse.com/words?ml={0}&sp=?????";
    
    private readonly HttpClient _client = new HttpClient();
    
    private record WordScore(string Word, int Score);
    
    public async Task<(string, int)[]> GetSimilarWords(string word)
    {
        var url = string.Format(Url, word);
        var response = await _client.GetAsync(url);
        var json = await response.Content.ReadAsStringAsync();
        var words = JsonConvert.DeserializeObject<WordScore[]>(json);
        return words.Select(w => (w.Word, w.Score)).ToArray();
    }
}

public interface IWordAnalysisService
{
    Task<(string, int)[]> GetSimilarWords(string word);
}