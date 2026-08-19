using System.Net;
using System.Net.Http.Headers;
using SearchApp.Services;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services
    .AddHttpClient("SearchApis", client =>
    {
        client.Timeout = TimeSpan.FromSeconds(20);
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("HttpSearchEngines", "1.0"));
    })
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        AutomaticDecompression = DecompressionMethods.All
    });

builder.Services.AddScoped(serviceProvider =>
{
    HttpClient httpClient = serviceProvider
        .GetRequiredService<IHttpClientFactory>()
        .CreateClient("SearchApis");
    HttpJsonClient jsonClient = new(httpClient);

    return new SearchCoordinator(
        [new WikipediaSearchProvider(jsonClient), new WikidataSearchProvider(jsonClient)],
        [new WikimediaCommonsImageProvider(jsonClient), new OpenverseImageProvider(jsonClient)]);
});

WebApplication app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();
app.UseRouting();
app.MapRazorPages();
app.Run();
