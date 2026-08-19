using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;

namespace SearchApp.Services;

internal sealed class HttpJsonClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;

    public HttpJsonClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<T> GetAsync<T>(string requestUrl, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _httpClient.GetAsync(
            requestUrl,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        response.EnsureSuccessStatusCode();
        T? result = await response.Content.ReadFromJsonAsync<T>(SerializerOptions, cancellationToken);
        return result ?? throw new JsonException("The service returned an empty JSON response");
    }
}
