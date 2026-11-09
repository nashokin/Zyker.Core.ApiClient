# Generic HTTP client

[![Build](https://github.com/nashokin/Zyker.Core.ApiClient/actions/workflows/build.yaml/badge.svg)](https://github.com/nashokin/Zyker.Core.ApiClient/actions/workflows/build.yaml)
![NuGet Version](https://img.shields.io/nuget/v/Zyker.Core.ApiClient)
[![Quality gate status](https://sonarcloud.io/api/project_badges/measure?project=nashokin_Zyker.Core.ApiClient&metric=alert_status&token=6a806cb7475b78450092f05483cb4a5d91da2f76)](https://sonarcloud.io/summary/new_code?id=nashokin_Zyker.Core.ApiClient)
[![Coverage](https://sonarcloud.io/api/project_badges/measure?project=nashokin_Zyker.Core.ApiClient&metric=coverage&token=6a806cb7475b78450092f05483cb4a5d91da2f76)](https://sonarcloud.io/summary/new_code?id=nashokin_Zyker.Core.ApiClient)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

Reusable **.NET** base library for building **HTTP API** integrations. Features include typed request/response handling, **JSON** processing, file downloads, centralised error handling, configurable timeouts and `Microsoft.Extensions.Logging` support.

---

### Usage

 - Install **NuGet** package into your project -> `Install-Package Zyker.Core.ApiClient -version [PKG_VERSION]`
 - Inherit from -> `ApiClient` **base** class
```csharp
public class MyApiClient
    : ApiClient, IMyApiClient
{
    [CODE], etc. ...
}
```
 - Instantiate **HTTP** client with one of the constructors, passing HTTP client factory and/or JSON parsing options
```csharp
public MyApiClient(
    IHttpClientFactory httpClientFactory,
    ILogger<MyApiClient> logger,
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
public async Task<EndOfDayPricesDto> GetEndOfDayPricesAsync(string MIC,
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

    EndOfDayPricesDto response = JsonSerializer
        .Deserialize<EndOfDayPricesDto>(data[0].GetRawText(), base.JsonSerializerOptions);

    _logger.LogInformation("{LOGGER_PREFIX} Retrieved prices : JSON response - {ResponseJson}",
        LOGGER_PREFIX, JsonSerializer.Serialize(response, base.JsonSerializerOptions));

    return response;
}
```
