namespace SearchApp.Models;

public sealed record ProviderFailure(string Provider, string Message);

public sealed record SearchBatch<T>(
    IReadOnlyList<T> Results,
    IReadOnlyList<ProviderFailure> Failures);
