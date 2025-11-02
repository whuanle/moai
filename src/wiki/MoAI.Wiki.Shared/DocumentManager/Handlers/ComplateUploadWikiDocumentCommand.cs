using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Wiki.DocumentManager.Handlers;

/// <summary>
/// 结束上传文件.
/// </summary>
public class ComplateUploadWikiDocumentCommand : IRequest<EmptyCommandResponse>
{
    /// <summary>
    /// 知识库ID.
    /// </summary>
    public int WikiId { get; init; } = default!;

    /// <summary>
    /// 上传成功或失败.
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// 文件ID.
    /// </summary>
    public int FileId { get; set; }

    /// <summary>
    /// 文件名称.
    /// </summary>
    public string FileName { get; set; } = default!;
}