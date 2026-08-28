using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Storage.Commands;

/// <summary>
/// 完成文件上传.
/// </summary>
public class CompleteFileUploadCommand : IRequest<EmptyCommandResponse>
{
    /// <summary>
    /// 上传成功或失败.
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// 文件 ID.
    /// </summary>
    public int FileId { get; set; }
}
