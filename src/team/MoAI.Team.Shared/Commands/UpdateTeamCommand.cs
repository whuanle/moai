using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Team.Commands;

/// <summary>
/// 更新团队信息，仅 Owner/Admin 可操作.
/// </summary>
public class UpdateTeamCommand : IRequest<EmptyCommandResponse>, IModelValidator<UpdateTeamCommand>
{
    /// <summary>
    /// 团队 id，由 Controller 从路由参数回填.
    /// </summary>
    public long TeamId { get; init; }

    /// <summary>
    /// 团队名称，为空时不修改.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// 团队简介，为空时不修改.
    /// </summary>
    public string? Description { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<UpdateTeamCommand> validate)
    {
        // TeamId 由 Controller 从路由参数回填，自动验证发生在回填之前，因此此处只校验请求体字段.
        validate.RuleFor(x => x.Name).NotEmpty().WithMessage("团队名称不能为空.").MaximumLength(50).WithMessage("团队名称最长 50 个字符.").When(x => x.Name != null);
        validate.RuleFor(x => x.Description).MaximumLength(255).WithMessage("团队简介最长 255 个字符.").When(x => x.Description != null);
    }
}
