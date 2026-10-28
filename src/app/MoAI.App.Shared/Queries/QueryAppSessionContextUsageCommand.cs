using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.App.Queries.Responses;
using MoAI.Infra.Models;
using MoAI.Infra.Services;

namespace MoAI.App.Queries;

/// <summary>
/// 查询会话上下文用量：会话累计 token 与生效对话模型的上下文窗口上限；仅会话归属用户可访问.
/// </summary>
public class QueryAppSessionContextUsageCommand : IRequest<QueryAppSessionContextUsageCommandResponse>, IUserIdContext, IModelValidator<QueryAppSessionContextUsageCommand>
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
    public static void Validate(AbstractValidator<QueryAppSessionContextUsageCommand> validate)
    {
        validate.RuleFor(x => x.SessionId).NotEmpty().WithMessage("会话 id 不正确.");
    }
}
