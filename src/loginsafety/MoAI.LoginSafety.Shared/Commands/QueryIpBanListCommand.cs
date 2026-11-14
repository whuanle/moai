using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.LoginSafety.Queries.Responses;

namespace MoAI.LoginSafety.Commands;

/// <summary>
/// 分页查询 IP 封禁记录，仅管理员.
/// </summary>
public class QueryIpBanListCommand : PagedParamter, IRequest<QueryIpBanListCommandResponse>, IModelValidator<QueryIpBanListCommand>
{
    /// <summary>
    /// IP（精确匹配），为空则不过滤.
    /// </summary>
    public string? Ip { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryIpBanListCommand> validate)
    {
    }
}
