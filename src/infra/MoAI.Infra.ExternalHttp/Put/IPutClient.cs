using MoAI.Infra.OAuth.Models;
using Refit;

namespace MoAI.Infra.Put;

/// <summary>
/// Put 文件请求.
/// </summary>
public interface IPutClient
{
    public HttpClient Client { get; }

    /// <summary>
    /// 请求.
    /// </summary>
    /// <param name="path"></param>
    /// <param name="stream"></param>
    /// <returns></returns>
    [Put("/{**path}")]
    [QueryUriFormat(UriFormat.Unescaped)]
    Task<OpenIdConfiguration> Put(string path, [Body(buffered: true)] StreamPart stream);
}
