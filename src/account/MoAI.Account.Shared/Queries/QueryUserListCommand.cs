using FluentValidation;
using MediatR;
using MoAI.Account.Queries.Responses;
using MoAI.Infra.Models;

namespace MoAI.Account.Queries;

/// <summary>
/// 分页查询用户列表.
/// </summary>
public class QueryUserListCommand : PagedParamter, IRequest<QueryUserListCommandResponse>, IModelValidator<QueryUserListCommand>
{
    /// <summary>
    /// 搜索关键字，模糊匹配用户名/昵称/邮箱，可为空.
    /// </summary>
    public string? SearchText { get; set; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryUserListCommand> validate)
    {
        validate.RuleFor(x => x.SearchText).MaximumLength(50).WithMessage("搜索关键字最长 50 个字符.");
    }
}
