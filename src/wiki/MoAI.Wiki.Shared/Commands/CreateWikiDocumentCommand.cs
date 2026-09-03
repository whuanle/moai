using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Wiki.Commands;

/// <summary>
/// 创建知识库文档，全体团队成员可协作.
/// </summary>
public class CreateWikiDocumentCommand : IRequest<SimpleLong>, IModelValidator<CreateWikiDocumentCommand>
{
    /// <summary>
    /// 所属知识库 id，由 Controller 从路由参数回填.
    /// </summary>
    public long WikiId { get; init; }

    /// <summary>
    /// 文档标题.
    /// </summary>
    public string Title { get; init; } = default!;

    /// <summary>
    /// 文档内容（Markdown）.
    /// </summary>
    public string? Content { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<CreateWikiDocumentCommand> validate)
    {
        // WikiId 由 Controller 从路由参数回填，自动验证发生在回填之前，因此此处只校验请求体字段.
        validate.RuleFor(x => x.Title).NotEmpty().WithMessage("文档标题不能为空.").MaximumLength(100).WithMessage("文档标题最长 100 个字符.");
    }
}
