using FluentValidation;
using Mediator;
using MoAI.Infra.Models;
using MoAI.Subsystem.Queries.Responses;

namespace MoAI.Subsystem.Queries;

/// <summary>
/// 查询当前用户可进入的子系统列表（用户面门户）：启用中、已开放委托登录（配置回调地址），
/// 且用户经「团队范围 ∪ 组织授权」命中（与 SSO 门禁同口径）.
/// </summary>
public class QueryMySubsystemsCommand : IUserIdContext, IRequest<QueryMySubsystemsCommandResponse>, IModelValidator<QueryMySubsystemsCommand>
{
    /// <inheritdoc/>
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryMySubsystemsCommand> validate)
    {
    }
}
