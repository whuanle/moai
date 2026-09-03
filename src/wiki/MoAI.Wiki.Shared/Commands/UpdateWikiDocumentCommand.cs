using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Wiki.Commands;

/// <summary>
/// 更新知识库文档，全体团队成员可协作.
/// </summary>
public class UpdateWikiDocumentCommand : IRequest<EmptyCommandResponse>, IModelValidator<UpdateWikiDocumentCommand>
{
    /// <summary>
    /// 文档 id，由 Controller 从路由参数回填.
    /// </summary>
    public long DocumentId { get; init; }

    /// <summary>
    /// 文档标题.
    /// </summary>
    public string Title { get; init; } = default!;

    /// <summary>
    /// 文档内容（Markdown）.
    /// </summary>
    public string Content { get; init; } = default!;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<UpdateWikiDocumentCommand> validate)
    {
        // DocumentId 由 Controller 从路由参数回填，自动验证发生在回填之前，因此此处只校验请求体字段.
        validate.RuleFor(x => x.Title).NotEmpty().WithMessage("文档标题不能为空.").MaximumLength(100).WithMessage("文档标题最长 100 个字符.");
    }
}
