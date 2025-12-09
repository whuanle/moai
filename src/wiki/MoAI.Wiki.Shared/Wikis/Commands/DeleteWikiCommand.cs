using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Wiki.Wikis.Commands;

/// <summary>
/// 删除知识库.
/// </summary>
public class DeleteWikiCommand : IRequest<EmptyCommandResponse>, IModelValidator<DeleteWikiCommand>
{
    /// <summary>
    /// 知识库id.
    /// </summary>
    public int WikiId { get; init; }

    /// <inheritdoc/>
    public void Validate(AbstractValidator<DeleteWikiCommand> validate)
    {
        validate.RuleFor(x => x.WikiId)
            .NotEmpty().WithMessage("知识库id不正确")
            .GreaterThan(0).WithMessage("知识库id不正确");
    }
}
