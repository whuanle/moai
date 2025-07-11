using MediatR;
using MoAI.Store.Enums;

namespace MoAI.Storage.Queries;

public class QueryFileLocalPathCommand : IRequest<QueryFileLocalPathCommandResponse>
{
    /// <summary>
    /// 文件可见性
    /// </summary>
    public FileVisibility Visibility { get; init; }

    /// <summary>
    /// key.
    /// </summary>
    public string ObjectKey { get; init; } = default!;
}

public class QueryFileLocalPathCommandResponse
{
    public string FilePath { get; init; }
}
