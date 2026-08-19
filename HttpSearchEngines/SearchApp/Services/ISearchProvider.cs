namespace SearchApp.Services;

internal interface ISearchProvider<T>
{
    string Name { get; }
    Task<IReadOnlyList<T>> SearchAsync(string query, CancellationToken cancellationToken);
}
