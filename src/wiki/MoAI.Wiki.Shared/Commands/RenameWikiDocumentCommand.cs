using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Wiki.Commands;

/// <summary>
/// 重命名知识库文档.
/// </summary>
public class RenameWikiDocumentCommand : IRequest<EmptyCommandResponse>, IModelValidator<RenameWikiDocumentCommand>
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
    /// 新的文件名称.
    /// </summary>
    public string FileName { get; init; } = default!;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<RenameWikiDocumentCommand> validate)
    {
        // WikiId/DocumentId 来自路由参数由 Controller 回填，自动验证发生在回填前，此处不做校验（否则恒 400）
        validate.RuleFor(x => x.FileName).NotEmpty().WithMessage("文件名称不能为空.").MaximumLength(255).WithMessage("文件名称最长 255 个字符.");
    }
}
