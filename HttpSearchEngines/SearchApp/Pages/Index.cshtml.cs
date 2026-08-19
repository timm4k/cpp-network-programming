using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SearchApp.Models;
using SearchApp.Services;

namespace SearchApp.Pages;

public sealed class IndexModel : PageModel
{
    private readonly SearchCoordinator _searchCoordinator;

    public IndexModel(SearchCoordinator searchCoordinator)
    {
        _searchCoordinator = searchCoordinator;
    }

    [BindProperty]
    public string? TextQuery { get; set; }

    [BindProperty]
    public List<string> SelectedTextProviders { get; set; } = [];

    [BindProperty]
    public string? ImageQuery { get; set; }

    [BindProperty]
    public List<string> SelectedImageProviders { get; set; } = [];

    public IReadOnlyList<string> TextProviderNames => _searchCoordinator.TextProviderNames;
    public IReadOnlyList<string> ImageProviderNames => _searchCoordinator.ImageProviderNames;
    public IReadOnlyList<TextSearchResult> TextResults { get; private set; } = [];
    public IReadOnlyList<ImageSearchResult> ImageResults { get; private set; } = [];
    public string TextStatus { get; private set; } = string.Empty;
    public string ImageStatus { get; private set; } = string.Empty;
    public string ActiveTab { get; private set; } = "text";

    public void OnGet()
    {
        SelectedTextProviders = [.. TextProviderNames];
        SelectedImageProviders = [.. ImageProviderNames];
    }

    public async Task<IActionResult> OnPostTextAsync(CancellationToken cancellationToken)
    {
        ActiveTab = "text";
        SelectedImageProviders = [.. ImageProviderNames];

        if (!TryValidateSearch(
                TextQuery,
                SelectedTextProviders,
                TextProviderNames,
                out string validationMessage))
        {
            TextStatus = validationMessage;
            return Page();
        }

        SearchBatch<TextSearchResult> batch = await _searchCoordinator.SearchTextAsync(
            TextQuery!,
            SelectedTextProviders,
            cancellationToken);
        TextResults = batch.Results;
        TextStatus = BuildStatus(TextResults.Count, batch.Failures);
        return Page();
    }

    public async Task<IActionResult> OnPostImagesAsync(CancellationToken cancellationToken)
    {
        ActiveTab = "images";
        SelectedTextProviders = [.. TextProviderNames];

        if (!TryValidateSearch(
                ImageQuery,
                SelectedImageProviders,
                ImageProviderNames,
                out string validationMessage))
        {
            ImageStatus = validationMessage;
            return Page();
        }

        SearchBatch<ImageSearchResult> batch = await _searchCoordinator.SearchImagesAsync(
            ImageQuery!,
            SelectedImageProviders,
            cancellationToken);
        ImageResults = batch.Results;
        ImageStatus = BuildStatus(ImageResults.Count, batch.Failures);
        return Page();
    }

    private static bool TryValidateSearch(
        string? query,
        IReadOnlyCollection<string> selectedProviders,
        IReadOnlyCollection<string> availableProviders,
        out string message)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            message = "Enter search text";
            return false;
        }
        if (query.Trim().Length > SearchCoordinator.MaximumQueryLength)
        {
            message = $"Search text cannot exceed {SearchCoordinator.MaximumQueryLength} characters";
            return false;
        }
        if (selectedProviders.Count == 0)
        {
            message = "Select at least one search engine";
            return false;
        }
        if (selectedProviders.Any(provider => !availableProviders.Contains(provider, StringComparer.Ordinal)))
        {
            message = "The selected search engine is not available";
            return false;
        }

        message = string.Empty;
        return true;
    }

    private static string BuildStatus(int resultCount, IReadOnlyList<ProviderFailure> failures)
    {
        string status = $"{resultCount} result(s) found";
        if (failures.Count == 0)
        {
            return status;
        }

        string errors = string.Join(" · ", failures.Select(failure => $"{failure.Provider}: {failure.Message}"));
        return $"{status} · {errors}";
    }
}
