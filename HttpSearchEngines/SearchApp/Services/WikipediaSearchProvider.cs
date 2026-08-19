using System.Net;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using SearchApp.Models;

namespace SearchApp.Services;

internal sealed partial class WikipediaSearchProvider : ISearchProvider<TextSearchResult>
{
    private const int ResultLimit = 10;
    private readonly HttpJsonClient _httpClient;

    public WikipediaSearchProvider(HttpJsonClient httpClient)
    {
        _httpClient = httpClient;
    }

    public string Name => "Wikipedia";

    public async Task<IReadOnlyList<TextSearchResult>> SearchAsync(
        string query,
        CancellationToken cancellationToken)
    {
        string requestUrl =
            "https://en.wikipedia.org/w/api.php?action=query&list=search&format=json&formatversion=2" +
            $"&srlimit={ResultLimit}&srsearch={Uri.EscapeDataString(query)}";

        WikipediaResponse response = await _httpClient.GetAsync<WikipediaResponse>(requestUrl, cancellationToken);
        return response.Query?.Search?
            .Select(item => new TextSearchResult(
                Name,
                WebUtility.HtmlDecode(item.Title),
                CleanSnippet(item.Snippet),
                $"https://en.wikipedia.org/?curid={item.PageId}"))
            .ToArray() ?? [];
    }

    private static string CleanSnippet(string snippet)
    {
        return WebUtility.HtmlDecode(HtmlTagPattern().Replace(snippet, string.Empty)).Trim();
    }

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex HtmlTagPattern();

    private sealed class WikipediaResponse
    {
        public WikipediaQuery? Query { get; init; }
    }

    private sealed class WikipediaQuery
    {
        public List<WikipediaItem>? Search { get; init; }
    }

    private sealed class WikipediaItem
    {
        public int PageId { get; init; }
        public string Title { get; init; } = string.Empty;
        public string Snippet { get; init; } = string.Empty;
    }
}
