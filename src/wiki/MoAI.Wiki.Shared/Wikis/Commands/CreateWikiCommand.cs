using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Wiki.Wikis.Commands;

/// <summary>
/// 创建知识库.
/// </summary>
public class CreateWikiCommand : IRequest<SimpleInt>, IModelValidator<CreateWikiCommand>
{
    /// <summary>
    /// 团队名称.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// 团队描述.
    /// </summary>
    public string Description { get; init; } = default!;

    /// <summary>
    /// 是否是系统知识库，创建后不允许修改此属性.
    /// </summary>
    public bool IsPublic { get; init; }

    /// <inheritdoc/>
    public void Validate(AbstractValidator<CreateWikiCommand> validate)
    {
        validate.RuleFor(x => x.Name)
            .NotEmpty().WithMessage("知识库名称长度在 2-20 之间.")
            .Length(2, 20).WithMessage("知识库名称长度在 2-20 之间.");

        validate.RuleFor(x => x.Description)
            .NotEmpty().WithMessage("知识库描述长度在 2-255 之间.")
            .Length(2, 255).WithMessage("知识库描述长度在 2-255 之间.");
    }
}
