using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Wiki.Commands;

/// <summary>
/// 创建知识库，需要团队 Admin 及以上角色.
/// </summary>
public class CreateWikiCommand : IRequest<SimpleLong>, IModelValidator<CreateWikiCommand>
{
    /// <summary>
    /// 所属团队 id.
    /// </summary>
    public long TeamId { get; init; }

    /// <summary>
    /// 知识库名称.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// 知识库简介.
    /// </summary>
    public string? Description { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<CreateWikiCommand> validate)
    {
        validate.RuleFor(x => x.TeamId).GreaterThan(0).WithMessage("团队 id 不正确.");
        validate.RuleFor(x => x.Name).NotEmpty().WithMessage("知识库名称不能为空.").MaximumLength(50).WithMessage("知识库名称最长 50 个字符.");
        validate.RuleFor(x => x.Description).MaximumLength(255).WithMessage("知识库简介最长 255 个字符.");
    }
}
