using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Org.Queries.Responses;

namespace MoAI.Org.Queries;

/// <summary>
/// 查询资源有效授权角色绑定的用户 id 并集.
/// </summary>
public class QueryOrgGrantUsersCommand : IRequest<QueryOrgGrantUsersCommandResponse>, IModelValidator<QueryOrgGrantUsersCommand>
{
    /// <summary>
    /// 资源类型字符串.
    /// </summary>
    public string ResourceType { get; init; } = string.Empty;

    /// <summary>
    /// 资源主键字符串.
    /// </summary>
    public string ResourceId { get; init; } = string.Empty;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryOrgGrantUsersCommand> validate)
    {
        validate.RuleFor(x => x.ResourceId).NotEmpty();
    }
}
