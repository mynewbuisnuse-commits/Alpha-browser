using System.Text.Json;

namespace AlphaBrowser.Services;

public static class SuggestionsService
{
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(3) };

    static SuggestionsService()
    {
        _http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent",
            "Mozilla/5.0 (Linux; Android 14) AlphaBrowser");
    }

    public static async Task<List<string>> GetSuggestionsAsync(string query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return new List<string>();

        try
        {
            var url = $"https://suggestqueries.google.com/complete/search?client=chrome&q={Uri.EscapeDataString(query)}";
            var response = await _http.GetStringAsync(url, ct);
            using var doc = JsonDocument.Parse(response);
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() >= 2)
            {
                var arr = root[1];
                var results = new List<string>();
                foreach (var el in arr.EnumerateArray())
                {
                    var s = el.GetString();
                    if (!string.IsNullOrEmpty(s) && results.Count < 8)
                        results.Add(s!);
                }
                return results;
            }
        }
        catch { }

        return new List<string>();
    }
}
