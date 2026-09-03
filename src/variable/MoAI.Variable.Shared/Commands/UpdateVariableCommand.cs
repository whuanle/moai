using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Variable.Commands;

/// <summary>
/// 更新团队变量，需要团队 Admin 及以上角色；变量名与类型不可修改，私密变量值留空表示保持不变.
/// </summary>
public class UpdateVariableCommand : IRequest<EmptyCommandResponse>, IModelValidator<UpdateVariableCommand>
{
    /// <summary>
    /// 变量 id，由 Controller 从路由参数回填.
    /// </summary>
    public long VariableId { get; init; }

    /// <summary>
    /// 分组名，null 表示不修改.
    /// </summary>
    public string? GroupName { get; init; }

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
        validate.RuleFor(x => x.GroupName).MaximumLength(50).WithMessage("分组名最长 50 个字符.").When(x => x.GroupName != null);
        validate.RuleFor(x => x.Description).MaximumLength(255).WithMessage("变量描述最长 255 个字符.").When(x => x.Description != null);
    }
}
