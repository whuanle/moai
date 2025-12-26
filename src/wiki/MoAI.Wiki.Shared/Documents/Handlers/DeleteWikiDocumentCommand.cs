using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Wiki.Documents.Handlers;

/// <summary>
/// 删除知识库文档.
/// </summary>
public class DeleteWikiDocumentCommand : IRequest<EmptyCommandResponse>, IModelValidator<DeleteWikiDocumentCommand>
{
    /// <summary>
    /// 知识库 id.
    /// </summary>
    public int WikiId { get; init; }

    /// <summary>
    /// 文档 id.
    /// </summary>
    public IReadOnlyCollection<int> DocumentIds { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<DeleteWikiDocumentCommand> validate)
    {
        validate.RuleFor(x => x.WikiId)
            .NotEmpty().WithMessage("知识库id不正确")
            .GreaterThan(0).WithMessage("知识库id不正确");

        validate.RuleFor(x => x.DocumentIds)
            .NotEmpty().WithMessage("文档id不正确")
            .Must(x => x.All(id => id > 0)).WithMessage("文档id不正确");
    }
}