using System;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Infra.Services;
using MoAI.Memory.Queries.Responses;
using System.Text.Json.Serialization;

namespace MoAI.Memory.Queries;

/// <summary>
/// 查询应用记忆统计（全部用户的记忆总条数与用户数，团队管理员视角）.
/// </summary>
public class QueryAppMemoryStatsCommand : IRequest<QueryAppMemoryStatsResponse>, IModelValidator<QueryAppMemoryStatsCommand>, IUserIdContext
{
    /// <summary>
    /// 应用 id.
    /// </summary>
    public Guid AppId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryAppMemoryStatsCommand> validate)
    {
    }
}
