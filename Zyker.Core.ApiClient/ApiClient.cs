using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Zyker.Core.ApiClient;
/// <summary>
///     Genetic HTTP API client.
/// </summary>
public abstract class ApiClient
{
    private const string API_CLIENT_NAME = "Zyker.HttpClient";
    private const int TIMEOUT_MINUTES = 3;

    /// <summary>
    ///     HTTP client for making API platform requests.
    /// </summary>
    protected HttpClient Client { get; private set; }

    /// <summary>
    ///     JSON serialiser options for deserialisation.
    /// </summary>
    protected JsonSerializerOptions JsonSerializerOptions { get; private set; }

    /// <summary>
    ///     MEL(Microsoft Extension Logging) logger.
    /// </summary>
    protected ILogger Logger { get; private set; }

    protected ApiClient(IHttpClientFactory httpClientFactory, ILogger logger)
    {
        JsonSerializerOptions = new()
        {
            NumberHandling = JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString,
            PropertyNameCaseInsensitive = true
        };
        JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());

        Client = httpClientFactory.CreateClient(API_CLIENT_NAME);
        Client.Timeout = TimeSpan.FromMinutes(TIMEOUT_MINUTES);

        Logger = logger;
    }

    protected ApiClient(IHttpClientFactory httpClientFactory, ILogger logger, JsonSerializerOptions jsonSerializerOptions)
    {
        JsonSerializerOptions = jsonSerializerOptions;

        Client = httpClientFactory.CreateClient(API_CLIENT_NAME);
        Client.Timeout = TimeSpan.FromMinutes(TIMEOUT_MINUTES);

        Logger = logger;
    }

    protected ApiClient(IHttpClientFactory httpClientFactory, ILogger logger, TimeSpan timeout, JsonSerializerOptions jsonSerializerOptions)
    {
        JsonSerializerOptions = jsonSerializerOptions;

        Client = httpClientFactory.CreateClient(API_CLIENT_NAME);
        Client.Timeout = timeout;

        Logger = logger;
    }

    /// <summary>
    ///     Send HTTP request message to specified Uri.
    /// </summary>
    /// <typeparam name="TResponseDto">
    ///     Data transfer object for response to be deserialised into.
    /// </typeparam>
    /// <param name="httpRequestMessage">HTTP request message object.</param>
    /// <param name="cancellationToken">Cancellation token object.</param>
    /// <returns>Deserialised data transfer object</returns>
    protected async Task<TResponseDto> SendRequestAsync<TResponseDto>(HttpRequestMessage httpRequestMessage, CancellationToken cancellationToken)
    {
        try
        {
            Logger.LogInformation("Initiating request to ... -> {RequestUri}", httpRequestMessage.RequestUri);

            byte[] response = await GetResponseAsync(httpRequestMessage, cancellationToken);
            TResponseDto data = JsonSerializer.Deserialize<TResponseDto>(Encoding.UTF8.GetString(response), JsonSerializerOptions)!;

            return data;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error occurred during the execution of the request ... -> {RequestUri}", httpRequestMessage.RequestUri);

            throw new HttpRequestException(ex.Message, ex.InnerException);
        }
    }

    /// <summary>
    ///     Send HTTP request message to specified Uri.
    /// </summary>
    /// <param name="httpRequestMessage">HTTP request message object.</param>
    /// <param name="cancellationToken">Cancellation token object.</param>
    /// <returns>Array of bytes containing the response message.</returns>
    protected async Task<byte[]> SendRequestAsync(HttpRequestMessage httpRequestMessage, CancellationToken cancellationToken)
    {
        try
        {
            Logger.LogInformation("Initiating request to ... -> {RequestUri}", httpRequestMessage.RequestUri);

            byte[] response = await GetResponseAsync(httpRequestMessage, cancellationToken);

            return response;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error occurred during the execution of the request ... -> {RequestUri}", httpRequestMessage.RequestUri);

            throw new HttpRequestException(ex.Message, ex.InnerException);
        }
    }

    /// <summary>
    ///     Retrieve file from the provided url over the network.
    /// </summary>
    /// <para>
    ///     This method instantiates a new HTTP client object and sets 1 minute timeout parameter
    ///     from the default 30 seconds.
    /// </para>
    /// <param name="fileUri">Endpoint Uri for the HTTP client to call.</param>
    /// <param name="cancellationToken">Cancellation token object.</param>
    /// <returns><c>DownloadFileInfo</c> object containing file content and additional information.</returns>
    [ExcludeFromCodeCoverage(Justification = "Wrapper method to call the overloaded method with HttpClient parameter, which is tested.")]
    protected static async Task<DownloadFileInfo> GetFileAsync(string fileUri, CancellationToken cancellationToken)
    {
        using HttpClient httpClient = new();
        httpClient.Timeout = TimeSpan.FromMinutes(1);

        return await GetFileAsync(fileUri, httpClient, cancellationToken);
    }

    /// <summary>
    ///     Retrieve file from the provided url over the network.
    /// </summary>
    /// <param name="fileUri">Endpoint Uri for the HTTP client to call.</param>
    /// <param name="httpClient">HTTP client object with specific parameter setting.</param>
    /// <param name="cancellationToken">Cancellation token object.</param>
    /// <returns>
    ///     <c>DownloadFileInfo</c> object containing file content and additional information. <see cref="DownloadFileInfo" />
    /// </returns>
    protected static async Task<DownloadFileInfo> GetFileAsync(string fileUri, HttpClient httpClient, CancellationToken cancellationToken)
    {
        HttpRequestMessage httpRequestMessage = CreateHttpRequestMessage(HttpMethod.Get, fileUri, null, null, null);

        try
        {
            HttpResponseMessage httpResponseMessage = await httpClient
                .SendAsync(httpRequestMessage, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            httpResponseMessage.EnsureSuccessStatusCode();

            Stream response = await httpResponseMessage.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            byte[] bytes = ReadStream(response);

            return bytes.Length != 0
                ? new DownloadFileInfo(bytes, httpResponseMessage.Content.Headers?.ContentDisposition, httpResponseMessage.Content.Headers?.ContentType)
                : throw new HttpRequestException("Empty response ...", null, HttpStatusCode.BadRequest);
        }
        catch (Exception ex)
        {
            throw new HttpRequestException(ex.Message, ex.InnerException);
        }
    }

    /// <summary>
    ///     Create HTTP request message object.
    /// </summary>
    /// <param name="httpMethod">HTTP verbs object(e.g. GET, POST etc.).</param>
    /// <param name="requestUri">Endpoint Uri for the HTTP client to call.</param>
    /// <param name="headers">Additional header parameters object.</param>
    /// <param name="queryParameters">Additional query string parameters object.</param>
    /// <param name="body">Content body object, will be serialised to JSON.</param>
    /// <returns>Composed request message object.</returns>
    protected static HttpRequestMessage CreateHttpRequestMessage(HttpMethod httpMethod, string requestUri, Dictionary<string, string>? headers = null,
        Dictionary<string, string>? queryParameters = null, object? body = null)
    {
        // Add query string parameters to the request message
        if (queryParameters?.Count > 0)
        {
            QueryString query = QueryString.Create(queryParameters);
            requestUri += query.ToString();
        }

        HttpRequestMessage httpRequestMessage = new(httpMethod, requestUri);

        // Add headers to the request message
        if (headers?.Count > 0)
        {
            foreach (KeyValuePair<string, string> header in headers)
            {
                httpRequestMessage.Headers.Add(header.Key, header.Value);
            }
        }

        // Add content body the request message
        if (body is not null)
        {
            httpRequestMessage.Content = JsonContent.Create(body);
        }

        return httpRequestMessage;
    }

    protected static JsonDocument ParseJsonDocument(byte[] bytes)
    {
        // Parse JSON metadata and get attached files
        try
        {
            Utf8JsonReader jsonReader = new(new ReadOnlySpan<byte>(bytes));
            JsonDocument jsonDocument = JsonDocument.ParseValue(ref jsonReader);

            return jsonDocument; // implementation has [NotNullWhen(true)] attribute, will not be null here
        }
        catch (JsonException ex)
        {
            throw new HttpRequestException("Bad request, invalid JSON.", ex.InnerException, HttpStatusCode.BadRequest);
        }
    }

    private async Task<byte[]> GetResponseAsync(HttpRequestMessage httpRequestMessage, CancellationToken cancellationToken)
    {
        using HttpResponseMessage httpResponseMessage = await Client
            .SendAsync(httpRequestMessage, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        httpResponseMessage.EnsureSuccessStatusCode();

        Stream response = await httpResponseMessage.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        byte[] bytes = ReadStream(response);

        return bytes.Length != 0
            ? bytes
            : throw new HttpRequestException("Empty response ...", null, HttpStatusCode.BadRequest);
    }

    private static byte[] ReadStream(Stream stream)
    {
        byte[] buffer = new byte[12 * 1024];
        using MemoryStream memoryStream = new();
        int read;
        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            memoryStream.Write(buffer, 0, read);
        }

        return memoryStream.ToArray();
    }
}
