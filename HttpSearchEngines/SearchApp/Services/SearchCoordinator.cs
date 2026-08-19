using System.Net.Http;
using System.Text.Json;
using SearchApp.Models;

namespace SearchApp.Services;

public sealed class SearchCoordinator
{
    public const int MaximumQueryLength = 200;
    private readonly IReadOnlyList<ISearchProvider<TextSearchResult>> _textProviders;
    private readonly IReadOnlyList<ISearchProvider<ImageSearchResult>> _imageProviders;

    internal SearchCoordinator(
        IReadOnlyList<ISearchProvider<TextSearchResult>> textProviders,
        IReadOnlyList<ISearchProvider<ImageSearchResult>> imageProviders)
    {
        _textProviders = textProviders;
        _imageProviders = imageProviders;
    }

    public IReadOnlyList<string> TextProviderNames => _textProviders.Select(provider => provider.Name).ToArray();
    public IReadOnlyList<string> ImageProviderNames => _imageProviders.Select(provider => provider.Name).ToArray();

    public Task<SearchBatch<TextSearchResult>> SearchTextAsync(
        string query,
        IReadOnlyCollection<string> selectedProviders,
        CancellationToken cancellationToken)
    {
        return SearchAsync(query, selectedProviders, _textProviders, cancellationToken);
    }

    public Task<SearchBatch<ImageSearchResult>> SearchImagesAsync(
        string query,
        IReadOnlyCollection<string> selectedProviders,
        CancellationToken cancellationToken)
    {
        return SearchAsync(query, selectedProviders, _imageProviders, cancellationToken);
    }

    private static async Task<SearchBatch<T>> SearchAsync<T>(
        string query,
        IReadOnlyCollection<string> selectedProviderNames,
        IReadOnlyList<ISearchProvider<T>> providers,
        CancellationToken cancellationToken)
    {
        string normalizedQuery = query.Trim();
        if (normalizedQuery.Length is 0 or > MaximumQueryLength)
        {
            throw new ArgumentException($"Search text must contain from 1 to {MaximumQueryLength} characters", nameof(query));
        }

        HashSet<string> selected = new(selectedProviderNames, StringComparer.Ordinal);
        ISearchProvider<T>[] activeProviders = providers
            .Where(provider => selected.Contains(provider.Name))
            .ToArray();

        if (activeProviders.Length == 0)
        {
            throw new ArgumentException("Select at least one search engine", nameof(selectedProviderNames));
        }

        Task<ProviderOutcome<T>>[] searches = activeProviders
            .Select(provider => SearchProviderAsync(provider, normalizedQuery, cancellationToken))
            .ToArray();

        ProviderOutcome<T>[] outcomes = await Task.WhenAll(searches);
        return new SearchBatch<T>(
            outcomes.SelectMany(outcome => outcome.Results).ToArray(),
            outcomes.Where(outcome => outcome.Failure is not null)
                .Select(outcome => outcome.Failure!)
                .ToArray());
    }

    private static async Task<ProviderOutcome<T>> SearchProviderAsync<T>(
        ISearchProvider<T> provider,
        string query,
        CancellationToken cancellationToken)
    {
        try
        {
            IReadOnlyList<T> results = await provider.SearchAsync(query, cancellationToken);
            return new ProviderOutcome<T>(results, null);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Failed<T>(provider.Name, "Request timed out");
        }
        catch (HttpRequestException error)
        {
            return Failed<T>(provider.Name, error.StatusCode is null
                ? "Network request failed"
                : $"HTTP {(int)error.StatusCode} {error.StatusCode}");
        }
        catch (JsonException)
        {
            return Failed<T>(provider.Name, "Invalid JSON response");
        }
    }

    private static ProviderOutcome<T> Failed<T>(string provider, string message)
    {
        return new ProviderOutcome<T>([], new ProviderFailure(provider, message));
    }

    private sealed record ProviderOutcome<T>(
        IReadOnlyList<T> Results,
        ProviderFailure? Failure);
}
