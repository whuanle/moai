using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.LoginSafety.Queries.Responses;

namespace MoAI.LoginSafety.Commands;

/// <summary>
/// 查询当前用户本人的登录记录（仅成功登录），需登录，用户 id 由上下文自动注入.
/// </summary>
public class QueryMyLoginLogCommand : PagedParamter, IRequest<QueryMyLoginLogCommandResponse>, IModelValidator<QueryMyLoginLogCommand>, IUserIdContext
{
    /// <inheritdoc/>
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryMyLoginLogCommand> validate)
    {
    }
}
