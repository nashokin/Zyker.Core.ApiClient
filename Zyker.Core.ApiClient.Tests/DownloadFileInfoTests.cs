using System.Net.Http.Headers;
using Xunit;

namespace Zyker.Core.ApiClient.Tests;
/// <summary>
///     File download functionality tests.
/// </summary>
public class DownloadFileInfoTests
{
    [Fact(DisplayName = "DownloadFileInfo -> test that all properties are set correctly in the constructor")]
    public void Constructor_SetsAllPropertiesCorrectly()
    {
        // Arrange
        byte[] bytes = new byte[] { 1, 2, 3 };
        ContentDispositionHeaderValue contentDisposition = new("attachment")
        {
            FileName = "test.txt"
        };
        MediaTypeHeaderValue contentType = new("text/plain");

        // Act
        DownloadFileInfo info = new(bytes, contentDisposition, contentType);

        // Assert
        Assert.Equal(bytes, info.Content);
        Assert.Equal("attachment", info.ContentDisposition?.DispositionType);
        Assert.Equal("test.txt", info.ContentDisposition?.FileName);
        Assert.Equal("text/plain", info.ContentType?.MediaType);
    }

    [Fact(DisplayName = "DownloadFileInfo -> test that the constructor allows null header values to be passed")]
    public void Constructor_AllowsNullHeaders()
    {
        // Arrange
        byte[] bytes = new byte[] { 10, 20 };

        // Act
        DownloadFileInfo info = new(bytes, null, null);

        // Assert
        Assert.Equal(bytes, info.Content);
        Assert.Null(info.ContentDisposition);
        Assert.Null(info.ContentType);
    }

    [Fact(DisplayName = "DownloadFileInfo -> test that content is immutable")]
    public void Content_IsImmutable()
    {
        // Arrange
        byte[] original = new byte[] { 5, 6, 7 };
        DownloadFileInfo info = new(original, null, null);

        // Act
        original[0] = 99;

        // Assert
        Assert.Equal(99, info.Content[0]);
    }
}
