using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.App.Queries.Responses;
using MoAI.Infra.Models;
using MoAI.Infra.Services;

namespace MoAI.App.Queries;

/// <summary>
/// 查询指定会话的消息详情（压缩后视图）；需要团队 Admin 及以上角色.
/// </summary>
public class QueryAppLogMessagesCommand : IRequest<QueryAppLogMessagesCommandResponse>, IUserIdContext, IModelValidator<QueryAppLogMessagesCommand>
{
    /// <summary>
    /// 应用 id（来自路由）.
    /// </summary>
    public Guid AppId { get; init; }

    /// <summary>
    /// 会话 id（来自路由）.
    /// </summary>
    public Guid SessionId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryAppLogMessagesCommand> validate)
    {
        validate.RuleFor(x => x.SessionId).NotEmpty().WithMessage("会话 id 不正确.");
    }
}
