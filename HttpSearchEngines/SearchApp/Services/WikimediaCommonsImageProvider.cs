using System.Text.Json.Serialization;
using SearchApp.Models;

namespace SearchApp.Services;

internal sealed class WikimediaCommonsImageProvider : ISearchProvider<ImageSearchResult>
{
    private const int ResultLimit = 8;
    private readonly HttpJsonClient _httpClient;

    public WikimediaCommonsImageProvider(HttpJsonClient httpClient)
    {
        _httpClient = httpClient;
    }

    public string Name => "Wikimedia Commons";

    public async Task<IReadOnlyList<ImageSearchResult>> SearchAsync(
        string query,
        CancellationToken cancellationToken)
    {
        string requestUrl =
            "https://commons.wikimedia.org/w/api.php?action=query&generator=search&format=json&formatversion=2" +
            "&gsrnamespace=6&prop=imageinfo%7Cinfo&iiprop=url&iiurlwidth=480&inprop=url" +
            $"&gsrlimit={ResultLimit}&gsrsearch={Uri.EscapeDataString(query)}";

        CommonsResponse response = await _httpClient.GetAsync<CommonsResponse>(requestUrl, cancellationToken);
        return response.Query?.Pages?
            .Select(CreateResult)
            .Where(result => result is not null)
            .Cast<ImageSearchResult>()
            .ToArray() ?? [];
    }

    private ImageSearchResult? CreateResult(CommonsPage page)
    {
        CommonsImageInfo? image = page.ImageInfo?.FirstOrDefault();
        if (image?.ThumbnailUrl is null || image.DescriptionUrl is null)
        {
            return null;
        }

        string title = page.Title.StartsWith("File:", StringComparison.OrdinalIgnoreCase)
            ? page.Title[5..]
            : page.Title;

        return new ImageSearchResult(
            Name,
            title,
            "Wikimedia Commons",
            image.ThumbnailUrl,
            image.DescriptionUrl);
    }

    private sealed class CommonsResponse
    {
        public CommonsQuery? Query { get; init; }
    }

    private sealed class CommonsQuery
    {
        public List<CommonsPage>? Pages { get; init; }
    }

    private sealed class CommonsPage
    {
        public string Title { get; init; } = string.Empty;

        [JsonPropertyName("imageinfo")]
        public List<CommonsImageInfo>? ImageInfo { get; init; }
    }

    private sealed class CommonsImageInfo
    {
        [JsonPropertyName("thumburl")]
        public string? ThumbnailUrl { get; init; }

        [JsonPropertyName("descriptionurl")]
        public string? DescriptionUrl { get; init; }
    }
}
