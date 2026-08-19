using SearchApp.Models;

namespace SearchApp.Services;

internal sealed class WikidataSearchProvider : ISearchProvider<TextSearchResult>
{
    private const int ResultLimit = 10;
    private readonly HttpJsonClient _httpClient;

    public WikidataSearchProvider(HttpJsonClient httpClient)
    {
        _httpClient = httpClient;
    }

    public string Name => "Wikidata";

    public async Task<IReadOnlyList<TextSearchResult>> SearchAsync(
        string query,
        CancellationToken cancellationToken)
    {
        string requestUrl =
            "https://www.wikidata.org/w/api.php?action=wbsearchentities&language=en&uselang=en&format=json" +
            $"&limit={ResultLimit}&search={Uri.EscapeDataString(query)}";

        WikidataResponse response = await _httpClient.GetAsync<WikidataResponse>(requestUrl, cancellationToken);
        return response.Search?
            .Where(item => !string.IsNullOrWhiteSpace(item.Id) && !string.IsNullOrWhiteSpace(item.Label))
            .Select(item => new TextSearchResult(
                Name,
                item.Label,
                string.IsNullOrWhiteSpace(item.Description) ? "No description available" : item.Description,
                $"https://www.wikidata.org/wiki/{Uri.EscapeDataString(item.Id)}"))
            .ToArray() ?? [];
    }

    private sealed class WikidataResponse
    {
        public List<WikidataItem>? Search { get; init; }
    }

    private sealed class WikidataItem
    {
        public string Id { get; init; } = string.Empty;
        public string Label { get; init; } = string.Empty;
        public string? Description { get; init; }
    }
}
