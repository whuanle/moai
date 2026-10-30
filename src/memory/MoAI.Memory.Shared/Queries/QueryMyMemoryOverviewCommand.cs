using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Infra.Services;
using MoAI.Memory.Queries.Responses;
using System.Text.Json.Serialization;

namespace MoAI.Memory.Queries;

/// <summary>
/// 查询本人的记忆总览：有记忆的应用列表与全局记忆概况（记忆管理页入口数据）.
/// </summary>
public class QueryMyMemoryOverviewCommand : IRequest<QueryMyMemoryOverviewResponse>, IModelValidator<QueryMyMemoryOverviewCommand>, IUserIdContext
{
    /// <inheritdoc/>
    [JsonIgnore]
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryMyMemoryOverviewCommand> validate)
    {
    }
}
