using FluentValidation;
using MediatR;
using MoAI.Account.Queries.Responses;
using MoAI.Infra.Models;

namespace MoAI.Account.Queries;

/// <summary>
/// 查询指定用户的信息（管理员视角）.
/// </summary>
public class QueryUserInfoCommand : IRequest<UserStateInfo>, IModelValidator<QueryUserInfoCommand>
{
    /// <summary>
    /// 用户 id.
    /// </summary>
    public long UserId { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryUserInfoCommand> validate)
    {
        validate.RuleFor(x => x.UserId).GreaterThan(0).WithMessage("用户 id 不正确.");
    }
}
