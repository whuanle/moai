using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Variable.Commands;

/// <summary>
/// 创建团队变量，需要团队 Admin 及以上角色.
/// </summary>
public class CreateVariableCommand : IRequest<SimpleLong>, IModelValidator<CreateVariableCommand>
{
    /// <summary>
    /// 所属团队 id.
    /// </summary>
    public long TeamId { get; init; }

    /// <summary>
    /// 变量名，团队内唯一，字母开头，仅字母/数字/下划线，插件配置中以 <c>${key}</c> 引用.
    /// </summary>
    public string Key { get; init; } = default!;

    /// <summary>
    /// 变量名称，仅组织用途，可为空.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// 是否私密变量：私密变量的值仅管理员可见，落库 AES 加密.
    /// </summary>
    public bool IsSecret { get; init; }

    /// <summary>
    /// 变量值.
    /// </summary>
    public string Value { get; init; } = default!;

    /// <summary>
    /// 变量描述.
    /// </summary>
    public string? Description { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<CreateVariableCommand> validate)
    {
        validate.RuleFor(x => x.TeamId).GreaterThan(0).WithMessage("团队 id 不正确.");
        validate.RuleFor(x => x.Key).NotEmpty().WithMessage("变量名不能为空.").Matches(@"^[A-Za-z][A-Za-z0-9_]{0,99}$").WithMessage("变量名仅允许字母开头，包含字母/数字/下划线，最长 100.");
        validate.RuleFor(x => x.Value).NotEmpty().WithMessage("变量值不能为空.");
        validate.RuleFor(x => x.Name).MaximumLength(50).WithMessage("变量名称最长 50 个字符.");
        validate.RuleFor(x => x.Description).MaximumLength(255).WithMessage("变量描述最长 255 个字符.");
    }
}
