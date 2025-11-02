using MediatR;
using MoAI.Infra.Models;
using MoAI.Storage.Queries.Response;

namespace MoAI.Store.Queries;

/// <summary>
/// 获取文件的下载地址.
/// </summary>
public class QueryFileDownloadUrlCommand : IRequest<QueryFileDownloadUrlCommandResponse>
{
    /// <summary>
    /// 过期时间，该地址过期后不能使用.
    /// </summary>
    public TimeSpan ExpiryDuration { get; init; }

    /// <summary>
    /// 对象列表.
    /// </summary>
    public IReadOnlyCollection<KeyValueString> ObjectKeys { get; init; } = new List<KeyValueString>();
}
