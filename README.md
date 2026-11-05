# Generic HTTP client

[![.NET](https://github.com/nashokin/Zyker.Core.ApiClient/actions/workflows/dotnet.yml/badge.svg?branch=main)](https://github.com/nashokin/Zyker.Core.ApiClient/actions/workflows/dotnet.yml)
![NuGet Version](https://img.shields.io/nuget/v/Zyker.Core.ApiClient)

---

### Usage

 - Install **NuGet** package into your project -> `Install-Package Zyker.Core.ApiClient -version [PKG_VERSION]`
 - Inherit from -> `ApiClient` **base** class
```csharp
public class FinnhubClient
    : ApiClient, IFinnhubClient
{
    [CODE], etc. ...
}
```
 - Instantiate the HTTP client with one of the constructors, passing HTTP client factory and/or JSON parsing options
```csharp
public FinnhubClient(
    IHttpClientFactory httpClientFactory,
    ILogger<FinnhubClient> logger,
    IConfiguration configuration)
    : base(httpClientFactory, logger)
{
    _logger = logger;

    IConfigurationSection finnhubConfiguration = configuration.GetRequiredSection(CONFIG_SECTION);
    _baseUri = finnhubConfiguration.GetSection(CONFIG_BASE_URI).Value;
    _authorisation = finnhubConfiguration.GetSection(CONFIG_AUTH).Value;
}
```
 - Compose HTTP message with the inherited compose massage, additional headers, query string and body object parameters can be supplied however optional
```csharp
...
string requestUri = $"{_baseUri}/[ENDPOINT_ROUTE]";
Dictionary<string, string> headers = new() { ["authorization"] = _authorisation };
Dictionary<string, string> queryString = new()
{
    ["mic"] = MIC,
    ["localcode"] = LOCAL_CODE,
    ["isin"] = ISIN,
    ["format"] = FORMAT
};
HttpRequestMessage httpRequestMessage = ApiClient
    .CreateHttpRequestMessage(HttpMethod.Get, requestUri, headers, queryString);
...
```
 - Send the request to the specified Uri
```csharp
public async Task<FinnhubLatestSecurityPriceDto> GetEndOfDaySecurityPricesAsync(string MIC,
    string localCode, string ISIN, CancellationToken cancellationToken)
{
    _logger.LogInformation("{LOGGER_PREFIX} Request to get the EOD security prices for : "
        + "MIC - {MIC}, LocalCode - {LocalCode}, ISIN - {ISIN}",
        LOGGER_PREFIX, MIC, localCode, ISIN);

    // Construct request message with access token header
    string requestUri = $"{_baseUri}/[ENDPOINT_ROUTE]";
    Dictionary<string, string> headers = new() { ["authorization"] = _authorisation };
    Dictionary<string, string> queryString = new()
    {
        ["mic"] = MIC,
        ["localcode"] = LOCAL_CODE,
        ["isin"] = ISIN,
        ["format"] = FORMAT
    };
    HttpRequestMessage httpRequestMessage = ApiClient
        .CreateHttpRequestMessage(HttpMethod.Get, requestUri, headers, queryString);
    byte[] bytes = await base.SendRequestAsync(httpRequestMessage, cancellationToken);

    // Parse JSON metadata and get attached files
    Utf8JsonReader jsonReader = new(new ReadOnlySpan<byte>(bytes));
    JsonDocument jsonResponse = null;
    if (!JsonDocument.TryParseValue(ref jsonReader, out jsonResponse))
        throw new HttpRequestException("Bad request, invalid JSON.", null,
            HttpStatusCode.BadRequest);

    JsonElement data = jsonResponse.RootElement.GetProperty("jsondata");

    FinnhubLatestSecurityPriceDto response = JsonSerializer
        .Deserialize<FinnhubLatestSecurityPriceDto>(data[0].GetRawText(),
            base.JsonSerializerOptions);

    _logger.LogInformation("{LOGGER_PREFIX} Retrieved prices : JSON response - {ResponseJson}",
        LOGGER_PREFIX, JsonSerializer.Serialize(response, base.JsonSerializerOptions));

    return response;
}
```
