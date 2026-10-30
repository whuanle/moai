using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Wiki.Commands;

/// <summary>
/// 下载知识库文档内嵌图片附件.
/// </summary>
public class DownloadWikiDocumentImageCommand : IRequest<SimpleString>, IModelValidator<DownloadWikiDocumentImageCommand>
{
    /// <summary>
    /// 知识库 id.
    /// </summary>
    public long WikiId { get; init; }

    /// <summary>
    /// 文档 id.
    /// </summary>
    public long DocumentId { get; init; }

    /// <summary>
    /// 图片附件 id.
    /// </summary>
    public long ImageId { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<DownloadWikiDocumentImageCommand> validate)
    {
        // WikiId/DocumentId/ImageId 来自路由参数由 Controller 回填，自动验证发生在回填前，此处不做校验（否则恒 400）
    }
}
