using MediatR;
using MoAI.Common.Queries.Response;
using MoAI.Infra.Models;

namespace MoAI.User.Queries;

/// <summary>
/// 用户信息查询填充.
/// </summary>
public class FillUserInfoCommand : IRequest<FillUserInfoCommandResponse>
{
    /// <summary>
    /// 集合.
    /// </summary>
    public required IReadOnlyCollection<AuditsInfo> Items { get; init; }
}
