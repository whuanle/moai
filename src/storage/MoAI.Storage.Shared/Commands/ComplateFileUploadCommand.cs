using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Storage.Commands;

/// <summary>
/// 结束上传文件.
/// </summary>
public class ComplateFileUploadCommand : IRequest<EmptyCommandResponse>
{
    /// <summary>
    /// 上传成功或失败.
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// 文件ID.
    /// </summary>
    public int FileId { get; set; }
}
