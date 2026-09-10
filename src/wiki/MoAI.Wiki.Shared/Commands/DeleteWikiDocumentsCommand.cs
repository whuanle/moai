using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Wiki.Commands;

/// <summary>
/// 删除知识库文档.
/// </summary>
public class DeleteWikiDocumentsCommand : IRequest<EmptyCommandResponse>, IModelValidator<DeleteWikiDocumentsCommand>
{
    /// <summary>
    /// 知识库 id.
    /// </summary>
    public long WikiId { get; init; }

    /// <summary>
    /// 文档 id 集合.
    /// </summary>
    public IReadOnlyCollection<long> DocumentIds { get; init; } = Array.Empty<long>();

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<DeleteWikiDocumentsCommand> validate)
    {
        // WikiId 来自路由参数由 Controller 回填，自动验证发生在回填前，此处不做校验（否则恒 400）
        validate.RuleFor(x => x.DocumentIds).NotEmpty().WithMessage("文档 id 不正确.").Must(x => x.All(id => id > 0)).WithMessage("文档 id 不正确.");
    }
}
