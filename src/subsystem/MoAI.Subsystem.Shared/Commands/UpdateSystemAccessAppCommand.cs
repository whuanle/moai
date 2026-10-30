using FluentValidation;
using MediatR;
using MoAI.Database.Enums;
using MoAI.Infra.Models;

namespace MoAI.Subsystem.Commands;

/// <summary>
/// 更新系统应用接入（root）：名称/描述/功能范围/状态；key 不可变更（删除后重建）.
/// </summary>
public class UpdateSystemAccessAppCommand : IRequest<EmptyCommandResponse>, IModelValidator<UpdateSystemAccessAppCommand>
{
    /// <summary>
    /// 接入 id（由路由填充）.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 接入名称.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// 描述，可选.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// 功能范围代码列表，至少一个.
    /// </summary>
    public IReadOnlyList<string> Scopes { get; init; } = [];

    /// <summary>
    /// 状态：enabled/disabled.
    /// </summary>
    public AccessAppStatus Status { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<UpdateSystemAccessAppCommand> validate)
    {
        validate.RuleFor(x => x.Name).NotEmpty().WithMessage("接入名称不能为空.").MaximumLength(40).WithMessage("接入名称最长 40 字符.");
        validate.RuleFor(x => x.Description).MaximumLength(255).WithMessage("描述最长 255 字符.");
    }
}
