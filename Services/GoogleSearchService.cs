using System.Net;
using System.Text.Json;
using System.Web;
using AlphaBrowser.Models;
using HtmlAgilityPack;

namespace AlphaBrowser.Services;

public static class GoogleSearchService
{
    private static readonly HttpClient _http;

    static GoogleSearchService()
    {
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = true,
            UseCookies = true,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        };
        _http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(15) };
        _http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent",
            "Mozilla/5.0 (Linux; Android 14; Pixel 7) AppleWebKit/537.36 AlphaBrowser");
        _http.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "en-US,en;q=0.9");
        _http.DefaultRequestHeaders.TryAddWithoutValidation("Accept",
            "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
    }

    public static async Task<List<SearchResult>> SearchAsync(string query, int page = 0, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return new List<SearchResult>();

        var engines = new Func<string, int, CancellationToken, Task<List<SearchResult>>>[]
        {
            SearchDuckDuckGoLiteAsync,
            SearchDuckDuckGoHtmlAsync,
            SearchBingAsync,
            SearchYahooAsync
        };

        Exception? lastException = null;
        foreach (var engine in engines)
        {
            try
            {
                var results = await engine(query, page, ct);
                if (results.Count > 0) return results;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { lastException = ex; }
        }

        throw new InvalidOperationException(
            $"Search failed on all engines. {(lastException?.Message ?? "No results returned.")}", lastException);
    }

    private static async Task<List<SearchResult>> SearchDuckDuckGoLiteAsync(string query, int page, CancellationToken ct)
    {
        var encoded = HttpUtility.UrlEncode(query);
        var url = page == 0
            ? $"https://lite.duckduckgo.com/lite/?q={encoded}"
            : $"https://lite.duckduckgo.com/lite/?q={encoded}&s={page * 10}";
        var html = await _http.GetStringAsync(url, ct);
        return ParseDuckDuckGoLite(html);
    }

    private static List<SearchResult> ParseDuckDuckGoLite(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        var results = new List<SearchResult>();
        var nodes = doc.DocumentNode.SelectNodes("//a[contains(@class,'result-link')]") ??
                    doc.DocumentNode.SelectNodes("//a[@class='result-link']");
        if (nodes == null) return results;
        foreach (var node in nodes)
        {
            var title = HtmlEntity.DeEntitize(node.InnerText.Trim());
            if (string.IsNullOrWhiteSpace(title)) continue;
            var href = DecodeRedirectUrl(node.GetAttributeValue("href", ""));
            if (string.IsNullOrWhiteSpace(href) || !href.StartsWith("http")) continue;
            results.Add(new SearchResult { Title = title, Url = href, Display = ShortUrl(href), Snippet = "" });
        }
        return results;
    }

    private static async Task<List<SearchResult>> SearchDuckDuckGoHtmlAsync(string query, int page, CancellationToken ct)
    {
        var encoded = HttpUtility.UrlEncode(query);
        var url = page == 0
            ? $"https://html.duckduckgo.com/html/?q={encoded}&kl=us-en"
            : $"https://html.duckduckgo.com/html/?q={encoded}&kl=us-en&s={page * 10}&dc={page * 10 + 1}";
        var html = await _http.GetStringAsync(url, ct);
        return ParseDuckDuckGoHtml(html);
    }

    private static List<SearchResult> ParseDuckDuckGoHtml(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        var results = new List<SearchResult>();
        var nodes = doc.DocumentNode.SelectNodes(
            "//div[contains(@class,'result') and contains(@class,'results_links')]") ??
                    doc.DocumentNode.SelectNodes("//div[@class='result']") ??
                    doc.DocumentNode.SelectNodes("//div[contains(@class,'result__body')]");
        if (nodes == null) return results;
        foreach (var node in nodes)
        {
            var titleAnchor = node.SelectSingleNode(
                ".//a[contains(@class,'result__a')] | .//h2/a | .//a[@class='result__a']");
            if (titleAnchor == null) continue;
            var title = HtmlEntity.DeEntitize(titleAnchor.InnerText.Trim());
            if (string.IsNullOrWhiteSpace(title)) continue;
            var href = DecodeRedirectUrl(titleAnchor.GetAttributeValue("href", ""));
            if (string.IsNullOrWhiteSpace(href) || !href.StartsWith("http")) continue;
            var snippetNode = node.SelectSingleNode(".//*[contains(@class,'result__snippet')]");
            var snippet = snippetNode != null ? HtmlEntity.DeEntitize(snippetNode.InnerText.Trim()) : "";
            var displayNode = node.SelectSingleNode(".//*[contains(@class,'result__url')]");
            var display = displayNode != null ? HtmlEntity.DeEntitize(displayNode.InnerText.Trim()) : ShortUrl(href);
            results.Add(new SearchResult { Title = title, Url = href, Display = display, Snippet = snippet });
        }
        return results;
    }

    private static async Task<List<SearchResult>> SearchBingAsync(string query, int page, CancellationToken ct)
    {
        var encoded = HttpUtility.UrlEncode(query);
        var first = page * 10 + 1;
        var url = $"https://www.bing.com/search?q={encoded}&first={first}&count=10&setlang=en&adlt=strict";
        var html = await _http.GetStringAsync(url, ct);
        return ParseBing(html);
    }

    private static List<SearchResult> ParseBing(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        var results = new List<SearchResult>();
        var items = doc.DocumentNode.SelectNodes("//li[contains(@class,'b_algo')]") ??
                    doc.DocumentNode.SelectNodes("//div[contains(@class,'b_algo')]");
        if (items == null) return results;
        foreach (var item in items)
        {
            var anchor = item.SelectSingleNode(".//h2/a") ?? item.SelectSingleNode(".//a");
            if (anchor == null) continue;
            var title = HtmlEntity.DeEntitize(anchor.InnerText.Trim());
            var href = anchor.GetAttributeValue("href", "");
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(href) || !href.StartsWith("http")) continue;
            var snippetNode = item.SelectSingleNode(".//*[contains(@class,'b_caption')]//p") ?? item.SelectSingleNode(".//p");
            var snippet = snippetNode != null ? HtmlEntity.DeEntitize(snippetNode.InnerText.Trim()) : "";
            var displayNode = item.SelectSingleNode(".//*[contains(@class,'tptt') or contains(@class,'b_attribution')]");
            var display = displayNode != null ? HtmlEntity.DeEntitize(displayNode.InnerText.Trim()) : ShortUrl(href);
            results.Add(new SearchResult { Title = title, Url = href, Display = display, Snippet = snippet });
        }
        return results;
    }

    private static async Task<List<SearchResult>> SearchYahooAsync(string query, int page, CancellationToken ct)
    {
        var encoded = HttpUtility.UrlEncode(query);
        var start = page * 10 + 1;
        var url = $"https://search.yahoo.com/search?p={encoded}&ei=UTF-8&b={start}";
        var html = await _http.GetStringAsync(url, ct);
        return ParseYahoo(html);
    }

    private static List<SearchResult> ParseYahoo(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        var results = new List<SearchResult>();
        var nodes = doc.DocumentNode.SelectNodes("//div[contains(@class,'algo')]") ??
                    doc.DocumentNode.SelectNodes("//div[@class='algo']");
        if (nodes == null) return results;
        foreach (var node in nodes)
        {
            var anchor = node.SelectSingleNode(".//a");
            if (anchor == null) continue;
            var title = HtmlEntity.DeEntitize(anchor.InnerText.Trim());
            var href = DecodeRedirectUrl(anchor.GetAttributeValue("href", ""));
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(href) || !href.StartsWith("http")) continue;
            var snippetNode = node.SelectSingleNode(".//span[contains(@class,'content')]//p") ??
                              node.SelectSingleNode(".//p") ??
                              node.SelectSingleNode(".//span[contains(@class,'abstract')]");
            var snippet = snippetNode != null ? HtmlEntity.DeEntitize(snippetNode.InnerText.Trim()) : "";
            results.Add(new SearchResult { Title = title, Url = href, Display = ShortUrl(href), Snippet = snippet });
        }
        return results;
    }

    private static string DecodeRedirectUrl(string href)
    {
        if (string.IsNullOrWhiteSpace(href)) return href;
        if (href.Contains("duckduckgo.com/l/?") || href.StartsWith("/l/?"))
        {
            var qs = href.Contains("?") ? href.Substring(href.IndexOf('?') + 1) : "";
            foreach (var pair in qs.Split('&'))
                if (pair.StartsWith("uddg=")) return Uri.UnescapeDataString(pair.Substring(5));
        }
        if (href.Contains("search.yahoo.com") && href.Contains("/RU="))
        {
            var ruIdx = href.IndexOf("/RU=");
            if (ruIdx >= 0)
            {
                var start = ruIdx + 4;
                var end = href.IndexOf("/RK=", start);
                if (end < 0) end = href.IndexOf("/RS=", start);
                if (end < 0) end = href.Length;
                try { return Uri.UnescapeDataString(href.Substring(start, end - start)); } catch { }
            }
        }
        return href;
    }

    private static string ShortUrl(string url)
    {
        try
        {
            var u = new Uri(url);
            var path = u.AbsolutePath.TrimEnd('/');
            return u.Host.TrimStart('w', '.') + (path.Length > 0 ? path : "");
        }
        catch { return url; }
    }
}
