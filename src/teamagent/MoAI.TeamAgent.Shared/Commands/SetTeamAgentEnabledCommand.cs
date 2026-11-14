using System;
using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.TeamAgent.Commands;

/// <summary>
/// 启用/停用团队智能体，需要团队 Admin 及以上角色（内置智能体同样可启停）.
/// </summary>
public class SetTeamAgentEnabledCommand : IRequest<EmptyCommandResponse>, IModelValidator<SetTeamAgentEnabledCommand>, IUserIdContext
{
    /// <summary>
    /// 智能体 id，由 Controller 从路由参数回填.
    /// </summary>
    public Guid AgentId { get; init; }

    /// <summary>
    /// 是否启用.
    /// </summary>
    public bool Enabled { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<SetTeamAgentEnabledCommand> validate)
    {
        // AgentId 由 Controller 从路由参数回填，自动验证发生在回填之前，因此此处只校验请求体字段.
    }
}
