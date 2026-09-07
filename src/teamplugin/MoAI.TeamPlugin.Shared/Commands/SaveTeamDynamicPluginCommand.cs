using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.TeamPlugin.Commands;

/// <summary>
/// 保存团队动态插件实例。创建时填实例 key + 模板 key + 配置；更新时实例 key 不可变.
/// </summary>
public class SaveTeamDynamicPluginCommand : IUserIdContext, IRequest<EmptyCommandResponse>, IModelValidator<SaveTeamDynamicPluginCommand>
{
    /// <inheritdoc/>
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    public UserType ContextUserType { get; init; }

    /// <summary>
    /// 团队 id.
    /// </summary>
    public long TeamId { get; init; }

    /// <summary>
    /// 实例 key（用户填，小写+下划线，团队内唯一）；更新时不可变.
    /// </summary>
    public string InstanceKey { get; init; } = string.Empty;

    /// <summary>
    /// 模板 key（后端代码模型的 key，如 dynamic_greet）.
    /// </summary>
    public string TempleteKey { get; init; } = string.Empty;

    /// <summary>
    /// 实例标题（展示名称）.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// 描述.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// 实例配置 JSON.
    /// </summary>
    public string Config { get; init; } = string.Empty;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<SaveTeamDynamicPluginCommand> validate)
    {
        validate.RuleFor(x => x.TeamId).GreaterThan(0).WithMessage("团队 id 不正确.");
        validate.RuleFor(x => x.InstanceKey)
            .NotEmpty().WithMessage("实例 Key 不能为空")
            .Matches("^[a-z_][a-z0-9_]*$").WithMessage("实例 Key 只能是小写字母、数字和下划线，且不能以数字开头")
            .MaximumLength(30).WithMessage("实例 Key 长度不能超过 30");
        validate.RuleFor(x => x.TempleteKey)
            .NotEmpty().WithMessage("模板 Key 不能为空");
        validate.RuleFor(x => x.Title)
            .NotEmpty().WithMessage("实例标题不能为空")
            .MaximumLength(30).WithMessage("实例标题长度不能超过 30");
        validate.RuleFor(x => x.Description)
            .MaximumLength(255).WithMessage("描述长度不能超过 255");
    }
}
