using System.Net.Http.Headers;

namespace Zyker.Core.ApiClient;
/// <summary>
///     Struct object to represent downloadable file content.
/// </summary>
public struct DownloadFileInfo
{
    /// <summary>
    ///     File content in raw bytes.
    /// </summary>
    public byte[] Content { get; }

    /// <summary>
    ///     Represents the value of the Content-Disposition header.
    /// </summary>
    public ContentDispositionHeaderValue? ContentDisposition { get; }

    /// <summary>
    ///     Represents a media type used in a Content-Type header as defined in the RFC 2616.
    /// </summary>
    public MediaTypeHeaderValue? ContentType { get; }

    public DownloadFileInfo(byte[] bytes, ContentDispositionHeaderValue? contentDisposition, MediaTypeHeaderValue? contentType)
    {
        Content = bytes;
        ContentDisposition = contentDisposition;
        ContentType = contentType;
    }
}
