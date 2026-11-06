using System;
using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.TeamAgent.Commands;

/// <summary>
/// 删除团队智能体（软删除），需要团队 Admin 及以上角色.
/// </summary>
public class DeleteTeamAgentCommand : IRequest<EmptyCommandResponse>, IModelValidator<DeleteTeamAgentCommand>, IUserIdContext
{
    /// <summary>
    /// 智能体 id，由 Controller 从路由参数回填.
    /// </summary>
    public Guid AgentId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<DeleteTeamAgentCommand> validate)
    {
        // AgentId 由 Controller 从路由参数回填，自动验证发生在回填之前，因此此处只校验请求体字段.
    }
}
