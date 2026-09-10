using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Wiki.Commands;

/// <summary>
/// 下载知识库文档.
/// </summary>
public class DownloadWikiDocumentCommand : IRequest<SimpleString>, IModelValidator<DownloadWikiDocumentCommand>
{
    /// <summary>
    /// 知识库 id.
    /// </summary>
    public long WikiId { get; init; }

    /// <summary>
    /// 文档 id.
    /// </summary>
    public long DocumentId { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<DownloadWikiDocumentCommand> validate)
    {
        // WikiId/DocumentId 来自路由参数由 Controller 回填，自动验证发生在回填前，此处不做校验（否则恒 400）
    }
}
