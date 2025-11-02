using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Storage.Commands;

/// <summary>
/// 删除文件.
/// </summary>
public class DeleteFileCommand : IRequest<EmptyCommandResponse>
{
    /// <summary>
    /// 文件id.
    /// </summary>
    public IReadOnlyCollection<int> FileIds { get; init; } = Array.Empty<int>();
}
