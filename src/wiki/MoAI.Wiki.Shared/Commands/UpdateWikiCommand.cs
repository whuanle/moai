using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Wiki.Commands;

/// <summary>
/// 更新知识库，需要团队 Admin 及以上角色.
/// </summary>
public class UpdateWikiCommand : IRequest<EmptyCommandResponse>, IModelValidator<UpdateWikiCommand>
{
    /// <summary>
    /// 知识库 id，由 Controller 从路由参数回填.
    /// </summary>
    public long WikiId { get; init; }

    /// <summary>
    /// 知识库名称.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// 知识库简介.
    /// </summary>
    public string? Description { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<UpdateWikiCommand> validate)
    {
        // WikiId 由 Controller 从路由参数回填，自动验证发生在回填之前，因此此处只校验请求体字段.
        validate.RuleFor(x => x.Name).NotEmpty().WithMessage("知识库名称不能为空.").MaximumLength(50).WithMessage("知识库名称最长 50 个字符.");
        validate.RuleFor(x => x.Description).MaximumLength(255).WithMessage("知识库简介最长 255 个字符.");
    }
}
