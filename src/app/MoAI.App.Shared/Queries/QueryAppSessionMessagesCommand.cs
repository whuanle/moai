using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.App.Queries.Responses;
using MoAI.Infra.Models;
using MoAI.Infra.Services;

namespace MoAI.App.Queries;

/// <summary>
/// 查询会话消息（按 seq 升序，落库为压缩后视图）；仅会话归属用户可访问.
/// </summary>
public class QueryAppSessionMessagesCommand : IRequest<QueryAppSessionMessagesCommandResponse>, IUserIdContext, IModelValidator<QueryAppSessionMessagesCommand>
{
    /// <summary>
    /// 会话 id.
    /// </summary>
    public Guid SessionId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryAppSessionMessagesCommand> validate)
    {
        validate.RuleFor(x => x.SessionId).NotEmpty().WithMessage("会话 id 不正确.");
    }
}
