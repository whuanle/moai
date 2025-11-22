using MoAI.Infra.OAuth.Models;
using Refit;

namespace MoAI.Infra.Put;

/// <summary>
/// Put 文件请求.
/// </summary>
public interface IPutClient
{
    /// <summary>
    /// Client.
    /// </summary>
    public HttpClient Client { get; }

    /// <summary>
    /// 上传文件请求.
    /// </summary>
    /// <param name="path"></param>
    /// <param name="stream"></param>
    /// <returns></returns>
    [Put("/{**path}")]
    [QueryUriFormat(UriFormat.Unescaped)]
    Task PutAsync(string path, [Body(buffered: true)] StreamPart stream);

    /// <summary>
    /// 下载文件请求.
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    [Get("/{**path}")]
    [QueryUriFormat(UriFormat.Unescaped)]
    Task<Stream> DownloadAsync(string path);
}
