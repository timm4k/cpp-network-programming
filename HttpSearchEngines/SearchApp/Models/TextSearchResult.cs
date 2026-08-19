namespace SearchApp.Models;

public sealed record TextSearchResult(
    string Provider,
    string Title,
    string Description,
    string PageUrl);
