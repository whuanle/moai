using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.App.Queries.Responses;
using MoAI.Infra.Models;
using MoAI.Infra.Services;

namespace MoAI.App.Queries;

/// <summary>
/// 查询团队下的应用列表，仅团队成员可访问.
/// </summary>
public class QueryAppsCommand : IRequest<QueryAppsCommandResponse>, IUserIdContext, IModelValidator<QueryAppsCommand>
{
    /// <summary>
    /// 团队 id.
    /// </summary>
    public long TeamId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryAppsCommand> validate)
    {
        validate.RuleFor(x => x.TeamId).GreaterThan(0).WithMessage("团队 id 不正确.");
    }
}
