using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Variable.Commands;

/// <summary>
/// 更新团队变量，需要团队 Admin 及以上角色；类型不可修改；私密变量的值不回显，留空表示保持不变.
/// </summary>
public class UpdateVariableCommand : IRequest<EmptyCommandResponse>, IModelValidator<UpdateVariableCommand>
{
    /// <summary>
    /// 变量 id，由 Controller 从路由参数回填.
    /// </summary>
    public long VariableId { get; init; }

    /// <summary>
    /// 变量名 key，团队内唯一；字母开头，仅字母/数字/下划线；null 表示不修改.
    /// </summary>
    public string? Key { get; init; }

    /// <summary>
    /// 变量名称，null 表示不修改.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// 变量值，null 表示保持不变（私密变量推荐留空以避免回传明文）.
    /// </summary>
    public string? Value { get; init; }

    /// <summary>
    /// 变量描述，null 表示不修改.
    /// </summary>
    public string? Description { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<UpdateVariableCommand> validate)
    {
        // VariableId 由 Controller 从路由参数回填，自动验证发生在回填之前，因此此处只校验请求体字段.
        validate.RuleFor(x => x.Key).Matches(@"^[A-Za-z][A-Za-z0-9_]{0,99}$").WithMessage("变量名仅允许字母开头，包含字母/数字/下划线，最长 100.").When(x => x.Key != null);
        validate.RuleFor(x => x.Name).MaximumLength(50).WithMessage("变量名称最长 50 个字符.").When(x => x.Name != null);
        validate.RuleFor(x => x.Description).MaximumLength(255).WithMessage("变量描述最长 255 个字符.").When(x => x.Description != null);
    }
}
