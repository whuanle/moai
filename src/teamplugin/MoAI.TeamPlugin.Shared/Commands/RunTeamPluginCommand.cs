using FluentValidation;
using MediatR;
using MoAI.AIPlugin.Models;
using MoAI.Infra.Models;

namespace MoAI.TeamPlugin.Commands;

/// <summary>
/// 执行团队可用插件（团队自有或已授权本团队的系统插件），仅团队成员可访问.
/// </summary>
public class RunTeamPluginCommand : IUserIdContext, IRequest<PluginRunResult>, IModelValidator<RunTeamPluginCommand>
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
    /// 插件 key.
    /// </summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>
    /// 请求参数 JSON 字符串.
    /// </summary>
    public string RequestJson { get; init; } = string.Empty;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<RunTeamPluginCommand> validate)
    {
        validate.RuleFor(x => x.TeamId).GreaterThan(0).WithMessage("团队 id 不正确.");
        validate.RuleFor(x => x.Key).NotEmpty().WithMessage("插件 Key 不能为空");
        validate.RuleFor(x => x.RequestJson).NotEmpty().WithMessage("请求参数不能为空");
    }
}
