using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Zyker.Core.ApiClient.Tests;
/// <summary>
///     Stub proxy HTTP client tests.
/// </summary>
public class ProxyStubTests
{
    private string _requestUri;
    private Dictionary<string, string> _headers;
    private Dictionary<string, string> _queryString;

    private readonly ProxyClient _proxyClient;

    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
    private readonly Mock<ILogger<ProxyClient>> _mockLogger;

    public ProxyStubTests()
    {
        _requestUri = "http://localhost";
        _headers = new() { ["authorization"] = Convert.ToBase64String(Encoding.UTF8.GetBytes("---=== authorization token ===---")) };
        _queryString = new()
        {
            ["mic"] = "XSTU",
            ["localcode"] = "NSN",
            ["isin"] = "DE0005021307",
            ["format"] = "json"
        };

        _mockHttpClientFactory = new Mock<IHttpClientFactory>();
        _mockLogger = new Mock<ILogger<ProxyClient>>();

        // Setup HTTP client to intercept Send to API requests
        HttpResponseMessage httpResponseMessageSend = new()
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(JsonSerializer.Serialize(new CastReturnModel("123", "Name)", "and description) ...")))
        };
        DelegatingHandlerStub clientHandlerStubSend = new((request, cancellationToken) => Task.FromResult(httpResponseMessageSend));
        HttpClient clientSend = new(clientHandlerStubSend);
        _mockHttpClientFactory.Setup(_ => _.CreateClient(It.IsAny<string>())).Returns(clientSend);
        _proxyClient = new ProxyClient(_mockHttpClientFactory.Object, _mockLogger.Object);
    }

    [Fact(DisplayName = "Proxy stub - SendRequestAsync -> returns DTO object")]
    public async Task ApiClient_ShouldReturnParsedDataTransferObject()
    {
        // Arrange
        HttpRequestMessage httpRequestMessage = ProxyClient.CallCreateHttpRequestMessage(HttpMethod.Get, _requestUri, _headers, _queryString);

        // Act
        CastReturnModel model = await _proxyClient.CallSendTestRequestAsync<CastReturnModel>(httpRequestMessage, It.IsAny<CancellationToken>());

        // Assert
        model.ShouldNotBeNull();
    }

    [Fact(DisplayName = "Proxy stub - SendRequestAsync -> returns bytes array")]
    public async Task ApiClient_ShouldReturnArrayOfBytes()
    {
        // Arrange
        HttpRequestMessage httpRequestMessage = ProxyClient.CallCreateHttpRequestMessage(HttpMethod.Get, _requestUri, _headers, _queryString);

        // Act
        byte[] bytes = await _proxyClient.CallSendTestRequestAsync(httpRequestMessage, It.IsAny<CancellationToken>());

        // Assert
        bytes.ShouldNotBeEmpty();
    }

    [Fact(DisplayName = "Proxy stub - GetFileAsync -> return DownloadFileInfo object")]
    public async Task ApiClient_ShouldReturnDownloadFileInfoObject()
    {
        // Arrange
        ByteArrayContent content = new(new byte[] { 1, 2, 3 });
        HttpResponseMessage httpResponseMessageDownload = new()
        {
            StatusCode = HttpStatusCode.OK,
            Content = content
        };
        DelegatingHandlerStub clientHandlerStub = new((request, cancellationToken) => Task.FromResult(httpResponseMessageDownload));
        HttpClient httpClient = new(clientHandlerStub);

        // Act
        DownloadFileInfo response = await ProxyClient.CallGetFileAsync(_requestUri, httpClient, It.IsAny<CancellationToken>());

        // Assert
        Assert.NotEmpty(response.Content);
        Assert.Equal(new byte[] { 1, 2, 3 }, response.Content);
        Assert.Equal(3, response.Content.Length);
        Assert.IsType<DownloadFileInfo>(response);
    }

    [Fact(DisplayName = "Proxy stub - GetFileAsync -> should throw on empty response")]
    public async Task ApiClient_ShouldThrowHttpRequestException_OnEmptyResponse()
    {
        // Arrange
        ByteArrayContent content = new(new byte[] { });
        HttpResponseMessage httpResponseMessageDownload = new()
        {
            StatusCode = HttpStatusCode.OK,
            Content = content
        };
        DelegatingHandlerStub clientHandlerStub = new((request, cancellationToken) => Task.FromResult(httpResponseMessageDownload));
        HttpClient httpClient = new(clientHandlerStub);

        // Act & Assert
        HttpRequestException ex = await Assert.ThrowsAsync<HttpRequestException>(async () =>
        {
            await ProxyClient.CallGetFileAsync(_requestUri, httpClient, It.IsAny<CancellationToken>());
        });

        Assert.Equal("Empty response ...", ex.Message);
        Assert.IsType<HttpRequestException>(ex);
    }

    [Fact(DisplayName = "Proxy stub - GetFileAsync -> should wrap and throw HttpRequestException")]
    public async Task ApiClient_GetFileAsync_WhenSendAsyncThrows_ShouldWrapInHttpRequestException()
    {
        // Arrange
        Mock<HttpMessageHandler> handlerMock = new();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Simulated throw ...", new Exception("Inner exception")));
        HttpClient httpClient = new(handlerMock.Object);

        // Act & Assert
        HttpRequestException ex = await Assert.ThrowsAsync<HttpRequestException>(async () =>
        {
            await ProxyClient.CallGetFileAsync(_requestUri, httpClient, It.IsAny<CancellationToken>());
        });

        Assert.Equal("Simulated throw ...", ex.Message);
        Assert.IsType<HttpRequestException>(ex);
    }
}

public class CastReturnModel
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }

    public CastReturnModel(string id, string name, string description)
    {
        Id = id;
        Name = name;
        Description = description;
    }
}

public class ProxyClient
    : ApiClient
{
    public ProxyClient(IHttpClientFactory httpClientFactory, ILogger<ProxyClient> logger)
        : base(httpClientFactory, logger)
    { }

    public static HttpRequestMessage CallCreateHttpRequestMessage(HttpMethod httpMethod, string requestUri, Dictionary<string, string>? headers = null,
        Dictionary<string, string>? queryParameters = null, object? body = null)
    {
        return CreateHttpRequestMessage(HttpMethod.Get, requestUri, headers, queryParameters);
    }

    public async Task<TResponseDto> CallSendTestRequestAsync<TResponseDto>(HttpRequestMessage httpRequestMessage, CancellationToken cancellationToken)
    {
        return await SendRequestAsync<TResponseDto>(httpRequestMessage, cancellationToken);
    }

    public async Task<byte[]> CallSendTestRequestAsync(HttpRequestMessage httpRequestMessage, CancellationToken cancellationToken)
    {
        return await SendRequestAsync(httpRequestMessage, cancellationToken);
    }

    public static async Task<DownloadFileInfo> CallGetFileAsync(string requestUri, HttpClient httpClient, CancellationToken cancellationToken)
    {
        return await GetFileAsync(requestUri, httpClient, cancellationToken);
    }
}

public class DelegatingHandlerStub
    : DelegatingHandler
{
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handlerFunc;

    public DelegatingHandlerStub(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handlerFunc)
    {
        _handlerFunc = handlerFunc;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return _handlerFunc(request, cancellationToken);
    }
}

