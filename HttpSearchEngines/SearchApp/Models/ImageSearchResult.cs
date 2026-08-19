namespace SearchApp.Models;

public sealed record ImageSearchResult(
    string Provider,
    string Title,
    string Attribution,
    string ThumbnailUrl,
    string PageUrl);
