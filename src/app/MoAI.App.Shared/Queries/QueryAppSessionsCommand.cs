using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.App.Queries.Responses;
using MoAI.Infra.Models;
using MoAI.Infra.Services;

namespace MoAI.App.Queries;

/// <summary>
/// 查询当前用户在某个应用下的会话列表（倒序）；仅团队成员可访问.
/// </summary>
public class QueryAppSessionsCommand : IRequest<QueryAppSessionsCommandResponse>, IUserIdContext, IModelValidator<QueryAppSessionsCommand>
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
    public static void Validate(AbstractValidator<QueryAppSessionsCommand> validate)
    {
        validate.RuleFor(x => x.AppId).NotEmpty().WithMessage("应用 id 不正确.");
    }
}
