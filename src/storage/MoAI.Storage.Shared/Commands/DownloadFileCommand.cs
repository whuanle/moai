using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Storage.Commands;

/// <summary>
/// 下载文件到指定位置.
/// </summary>
public class DownloadFileCommand : IRequest<EmptyCommandResponse>
{
    /// <summary>
    /// key.
    /// </summary>
    public string ObjectKey { get; init; } = default!;

    /// <summary>
    /// 本地路径.
    /// </summary>
    public string LocalFilePath { get; init; } = default!;
}
