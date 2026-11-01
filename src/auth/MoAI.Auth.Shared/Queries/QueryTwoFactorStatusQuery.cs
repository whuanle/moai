using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.Auth.Queries.Responses;
using MoAI.Infra.Models;
namespace MoAI.Auth.Queries;

/// <summary>
/// 查询当前用户两步验证（2FA）状态.
/// </summary>
public class QueryTwoFactorStatusQuery : IRequest<QueryTwoFactorStatusQueryResponse>, IUserIdContext, IModelValidator<QueryTwoFactorStatusQuery>
{
    /// <inheritdoc/>
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public UserType ContextUserType { get; init; }


    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryTwoFactorStatusQuery> validate)
    {
        validate.RuleFor(x => x.ContextUserId).GreaterThan(0).WithMessage("用户未登录.");
    }
}
