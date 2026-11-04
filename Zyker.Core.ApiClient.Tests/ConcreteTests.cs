using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Zyker.Core.ApiClient.Tests;
public class ConcreteTests
{
    private readonly string _requestUri;
    private readonly Dictionary<string, string> _headers;
    private readonly Dictionary<string, string> _queryString;
    private readonly Dictionary<string, string> _body;

    private readonly JsonSerializerOptions _jsonSerializerOptions;

    private readonly Mock<ILogger<ProxyClient>> _mockLogger;
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;

    public ConcreteTests()
    {
        _requestUri = "http://localhost";
        _queryString = new() { { "q", "search" } };
        _headers = new() { { "X-Test", "value" } };
        _body = new() { { "Id", "1" } };

        _jsonSerializerOptions = new()
        {
            NumberHandling = JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString,
            PropertyNameCaseInsensitive = true
        };
        _jsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());

        _mockLogger = new Mock<ILogger<ProxyClient>>();
        _mockHttpClientFactory = new Mock<IHttpClientFactory>();
        _mockHttpClientFactory.Setup(_ => _.CreateClient(It.IsAny<string>())).Returns(new HttpClient());
    }

    [Fact(DisplayName = "Concrete - constructor -> sets Timeout and Logger")]
    public void Constructor_SetsTimeoutAndLogger()
    {
        // Act
        TestApiClient apiClientCtorOne = new(_mockHttpClientFactory.Object, _mockLogger.Object);
        TestApiClient apiClientCtorTwo = new(_mockHttpClientFactory.Object, _mockLogger.Object, _jsonSerializerOptions);
        TestApiClient apiClientCtorThree = new(_mockHttpClientFactory.Object, _mockLogger.Object, TimeSpan.FromMinutes(3), _jsonSerializerOptions);

        // Assert
        Assert.NotNull(apiClientCtorOne.ExposedClient);
        Assert.NotNull(apiClientCtorTwo.ExposedClient);
        Assert.NotNull(apiClientCtorThree.ExposedClient);
        Assert.Equal(TimeSpan.FromMinutes(3), apiClientCtorOne.ExposedClient.Timeout);
        Assert.Equal(TimeSpan.FromMinutes(3), apiClientCtorTwo.ExposedClient.Timeout);
        Assert.Equal(TimeSpan.FromMinutes(3), apiClientCtorThree.ExposedClient.Timeout);
    }

    [Fact(DisplayName = "Concrete - SendRequestAsync<T> -> deserializes response")]
    public async Task SendRequestAsyncT_DeserializesResponse()
    {
        // Arrange
        MockHttpMessageHandler handler = new("{\"TestValue\":\"Property value\"}", HttpStatusCode.OK);
        HttpClient client = new(handler);

        _mockHttpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(client);

        TestApiClient apiClient = new(_mockHttpClientFactory.Object, _mockLogger.Object);
        HttpRequestMessage request = new(HttpMethod.Get, _requestUri);

        // Act
        TestDto result = await apiClient.CallSendRequestAsync<TestDto>(request, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.TestValue);
        Assert.Equal("Property value", result.TestValue);
    }

    [Fact(DisplayName = "Concrete - SendRequestAsync -> throws on empty response")]
    public async Task SendRequestAsync_ThrowsOnEmptyResponse()
    {
        // Arrange
        HttpRequestMessage request = new(HttpMethod.Get, _requestUri);

        // Act
        TestApiClient apiClient = new(_mockHttpClientFactory.Object, _mockLogger.Object);

        // Assert
        await Assert.ThrowsAsync<HttpRequestException>(async () => await apiClient.CallSendRequestAsync(request, CancellationToken.None));
        await Assert.ThrowsAsync<HttpRequestException>(async () => await apiClient.CallSendRequestAsync<TestDto>(request, CancellationToken.None));
    }

    [Fact(DisplayName = "Concrete - CreateHttpRequestMessage -> builds correct request")]
    public void CreateHttpRequestMessage_BuildsCorrectRequest()
    {
        // Act
        HttpRequestMessage request = TestApiClient.CallCreateHttpRequestMessage(HttpMethod.Post, _requestUri, _headers, _queryString, _body);

        // Assert
        Assert.NotNull(request.Content);
        Assert.Equal($"{_requestUri}/?q=search", request?.RequestUri?.ToString());
        Assert.Equal("value", request?.Headers.GetValues("X-Test").First());
        Assert.Equal(HttpMethod.Post, request?.Method);
    }

    [Fact(DisplayName = "Concrete - ParseJsonDocument -> parses valid JSON object")]
    public void ParseJsonDocument_ParsesValidJson()
    {
        // Arrange
        byte[] json = Encoding.UTF8.GetBytes("{\"key\":\"value\"}");

        // Act
        TestApiClient apiClient = new(_mockHttpClientFactory.Object, _mockLogger.Object);
        JsonDocument jsonDocument = apiClient.CallParseJsonDocument(json);

        // Assert
        Assert.NotNull(jsonDocument);
        Assert.Equal("value", jsonDocument.RootElement.GetProperty("key").GetString());
    }

    [Fact(DisplayName = "Concrete - ParseJsonDocument -> throws on invalid JSON string")]
    public void ParseJsonDocument_ThrowsOnInvalidJson()
    {
        // Arrange
        string invalidJson = "Not a valid JSON object ...";

        MockHttpMessageHandler handler = new(invalidJson, HttpStatusCode.OK);
        HttpClient client = new(handler);

        _mockHttpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(client);

        // Act
        TestApiClient apiClient = new(_mockHttpClientFactory.Object, _mockLogger.Object);

        // Act & Assert
        Assert.Throws<HttpRequestException>(() => apiClient.CallParseJsonDocument(Encoding.UTF8.GetBytes(invalidJson)));
    }

    private class TestDto
    {
        public string? TestValue { get; set; }
    }

    private class TestApiClient
        : ApiClient
    {
        public TestApiClient(IHttpClientFactory factory, ILogger logger)
            : base(factory, logger)
        { }

        public TestApiClient(IHttpClientFactory factory, ILogger logger, JsonSerializerOptions options)
            : base(factory, logger, options)
        { }

        public TestApiClient(IHttpClientFactory factory, ILogger logger, TimeSpan timeout, JsonSerializerOptions options)
            : base(factory, logger, timeout, options)
        { }

        public HttpClient ExposedClient => Client;

        public async Task<T> CallSendRequestAsync<T>(HttpRequestMessage request, CancellationToken token)
            => await SendRequestAsync<T>(request, token);

        public async Task<byte[]> CallSendRequestAsync(HttpRequestMessage request, CancellationToken token)
            => await SendRequestAsync(request, token);

        public static HttpRequestMessage CallCreateHttpRequestMessage(HttpMethod httpMethod, string requestUri, Dictionary<string, string>? headers = null,
            Dictionary<string, string>? queryParameters = null, object? body = null)
        {
            return ApiClient.CreateHttpRequestMessage(httpMethod, requestUri, headers, queryParameters, body);
        }

        public JsonDocument CallParseJsonDocument(byte[] json) => ApiClient.ParseJsonDocument(json);
    }

    private class MockHttpMessageHandler
        : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;

        public MockHttpMessageHandler(string content, HttpStatusCode statusCode)
        {
            _response = new HttpResponseMessage(statusCode)
            {
                Content = new ByteArrayContent(Encoding.UTF8.GetBytes(content))
            };
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_response);
    }
}
