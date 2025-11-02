using MediatR;
using MoAI.Storage.Queries.Response;

namespace MoAI.Storage.Queries;

/// <summary>
/// 将文件复制到本地，并返回本地路径.
/// </summary>
public class QueryFileLocalPathCommand : IRequest<QueryFileLocalPathCommandResponse>
{
    /// <summary>
    /// key.
    /// </summary>
    public string ObjectKey { get; init; } = default!;
}
