using System.Collections.Concurrent;

namespace AlphaBrowser.Services;

public static class FaviconService
{
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(5) };
    private static readonly ConcurrentDictionary<string, string?> _cache = new();

    public static async Task<string?> GetAsync(string domain)
    {
        if (_cache.TryGetValue(domain, out var cached))
            return cached;

        try
        {
            var url = $"https://www.google.com/s2/favicons?domain={domain}&sz=32";
            var bytes = await _http.GetByteArrayAsync(url);
            var base64 = Convert.ToBase64String(bytes);
            var result = $"data:image/png;base64,{base64}";
            _cache[domain] = result;
            return result;
        }
        catch
        {
            _cache[domain] = null;
            return null;
        }
    }
}
