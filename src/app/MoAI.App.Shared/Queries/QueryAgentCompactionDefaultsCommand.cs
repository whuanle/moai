using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Infra.Services;
using MoAI.App.Queries.Responses;
using System.Text.Json.Serialization;

namespace MoAI.App.Queries;

/// <summary>
/// 查询 Agent 上下文压缩的生效默认参数（系统设置 AGENT_COMPACTION_DEFAULTS，供应用配置页输入框预填）.
/// </summary>
public class QueryAgentCompactionDefaultsCommand : IRequest<QueryAgentCompactionDefaultsResponse>, IModelValidator<QueryAgentCompactionDefaultsCommand>, IUserIdContext
{
    /// <inheritdoc/>
    [JsonIgnore]
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryAgentCompactionDefaultsCommand> validate)
    {
    }
}
