using System;
using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.TeamAgent.Queries.Responses;

namespace MoAI.TeamAgent.Queries;

/// <summary>
/// 查询团队智能体详情（含子智能体定义），仅团队成员可访问.
/// </summary>
public class QueryTeamAgentDetailCommand : IRequest<QueryTeamAgentDetailCommandResponse>, IModelValidator<QueryTeamAgentDetailCommand>, IUserIdContext
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
    public static void Validate(AbstractValidator<QueryTeamAgentDetailCommand> validate)
    {
        // AgentId 由 Controller 从路由参数回填，自动验证发生在回填之前，因此此处只校验请求体字段.
    }
}
