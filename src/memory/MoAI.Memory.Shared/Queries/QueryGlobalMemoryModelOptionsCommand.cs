using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Infra.Services;
using MoAI.Memory.Queries.Responses;
using System.Text.Json.Serialization;

namespace MoAI.Memory.Queries;

/// <summary>
/// 查询全局记忆可选向量化模型（公开启用中的 embedding 模型；仅超级管理员，供系统设置选择）.
/// </summary>
public class QueryGlobalMemoryModelOptionsCommand : IRequest<QueryGlobalMemoryModelOptionsResponse>, IModelValidator<QueryGlobalMemoryModelOptionsCommand>, IUserIdContext
{
    /// <inheritdoc/>
    [JsonIgnore]
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryGlobalMemoryModelOptionsCommand> validate)
    {
    }
}
