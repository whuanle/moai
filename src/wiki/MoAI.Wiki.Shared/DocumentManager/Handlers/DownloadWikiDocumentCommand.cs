using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Wiki.DocumentManager.Handlers;

/// <summary>
/// 下载知识库文档.
/// </summary>
public class DownloadWikiDocumentCommand : IRequest<SimpleString>
{
    /// <summary>
    /// 知识库 id.
    /// </summary>
    public int WikiId { get; init; }

    /// <summary>
    /// 文档 id.
    /// </summary>
    public int DocumentId { get; init; }
}
