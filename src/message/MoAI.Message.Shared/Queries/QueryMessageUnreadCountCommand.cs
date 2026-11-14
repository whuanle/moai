using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Message.Queries;

/// <summary>
/// 查询我的未读站内信数量.
/// </summary>
public class QueryMessageUnreadCountCommand : IRequest<SimpleLong>, IModelValidator<QueryMessageUnreadCountCommand>, IUserIdContext
{
    /// <inheritdoc/>
    [JsonIgnore]
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryMessageUnreadCountCommand> validate)
    {
    }
}
