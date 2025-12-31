using MediatR;
using MoAI.Storage.Commands.Models;

namespace MoAI.Storage.Commands;

/// <summary>
/// 下载文件.
/// </summary>
public class DownloadFileCommand : IRequest<DownloadFileCommandResponse>
{
    /// <summary>
    /// key.
    /// </summary>
    public int FileId { get; init; } = default!;
}
