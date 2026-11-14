using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Infra.Services;
using MoAI.Memory.Queries.Responses;
using System.Text.Json.Serialization;

namespace MoAI.Memory.Queries;

/// <summary>
/// 查询长期记忆向量化模型迁移状态：当前生效模型/维度、平台记忆总数与最近一次迁移任务进度（仅超级管理员）.
/// </summary>
public class QueryMemoryModelMigrationStatusCommand : IRequest<QueryMemoryModelMigrationStatusResponse>, IModelValidator<QueryMemoryModelMigrationStatusCommand>, IUserIdContext
{
    /// <inheritdoc/>
    [JsonIgnore]
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryMemoryModelMigrationStatusCommand> validate)
    {
    }
}
