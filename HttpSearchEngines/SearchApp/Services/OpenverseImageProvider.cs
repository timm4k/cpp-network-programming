using System.Text.Json.Serialization;
using SearchApp.Models;

namespace SearchApp.Services;

internal sealed class OpenverseImageProvider : ISearchProvider<ImageSearchResult>
{
    private const int ResultLimit = 8;
    private readonly HttpJsonClient _httpClient;

    public OpenverseImageProvider(HttpJsonClient httpClient)
    {
        _httpClient = httpClient;
    }

    public string Name => "Openverse";

    public async Task<IReadOnlyList<ImageSearchResult>> SearchAsync(
        string query,
        CancellationToken cancellationToken)
    {
        string requestUrl =
            $"https://api.openverse.org/v1/images/?page_size={ResultLimit}&q={Uri.EscapeDataString(query)}";

        OpenverseResponse response = await _httpClient.GetAsync<OpenverseResponse>(requestUrl, cancellationToken);
        return response.Results?
            .Where(item => !string.IsNullOrWhiteSpace(item.Thumbnail) &&
                !string.IsNullOrWhiteSpace(item.ForeignLandingUrl))
            .Select(item => new ImageSearchResult(
                Name,
                string.IsNullOrWhiteSpace(item.Title) ? "Untitled image" : item.Title,
                string.IsNullOrWhiteSpace(item.Attribution)
                    ? $"Creator: {item.Creator ?? "Unknown"}"
                    : item.Attribution,
                item.Thumbnail,
                item.ForeignLandingUrl))
            .ToArray() ?? [];
    }

    private sealed class OpenverseResponse
    {
        public List<OpenverseItem>? Results { get; init; }
    }

    private sealed class OpenverseItem
    {
        public string? Title { get; init; }
        public string? Creator { get; init; }
        public string? Attribution { get; init; }
        public string Thumbnail { get; init; } = string.Empty;

        [JsonPropertyName("foreign_landing_url")]
        public string ForeignLandingUrl { get; init; } = string.Empty;
    }
}
