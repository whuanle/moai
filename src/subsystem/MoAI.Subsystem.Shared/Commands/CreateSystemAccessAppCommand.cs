using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Subsystem.Commands.Responses;

namespace MoAI.Subsystem.Commands;

/// <summary>
/// 创建系统应用接入（root）：key 原文仅在创建结果中返回一次.
/// </summary>
public class CreateSystemAccessAppCommand : IRequest<CreateSystemAccessAppCommandResponse>, IModelValidator<CreateSystemAccessAppCommand>
{
    /// <summary>
    /// 接入名称（如「ERP 组织同步」）.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// 描述，可选.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// 功能范围代码列表（org_read/org_write/user_read/user_write），至少一个.
    /// </summary>
    public IReadOnlyList<string> Scopes { get; init; } = [];

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<CreateSystemAccessAppCommand> validate)
    {
        validate.RuleFor(x => x.Name).NotEmpty().WithMessage("接入名称不能为空.").MaximumLength(40).WithMessage("接入名称最长 40 字符.");
        validate.RuleFor(x => x.Description).MaximumLength(255).WithMessage("描述最长 255 字符.");
    }
}
