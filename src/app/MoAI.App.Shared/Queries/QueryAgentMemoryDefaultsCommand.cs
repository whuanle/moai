using FluentValidation;
using MediatR;
using MoAI.App.Queries.Responses;
using MoAI.Infra.Models;
using MoAI.Infra.Services;
using System.Text.Json.Serialization;

namespace MoAI.App.Queries;

/// <summary>
/// 查询长期记忆的默认提示词（应用未配置 prompt 时生效的平台默认值，供应用配置页展示与占位）.
/// </summary>
public class QueryAgentMemoryDefaultsCommand : IRequest<QueryAgentMemoryDefaultsResponse>, IModelValidator<QueryAgentMemoryDefaultsCommand>, IUserIdContext
{
    /// <inheritdoc/>
    [JsonIgnore]
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryAgentMemoryDefaultsCommand> validate)
    {
    }
}
