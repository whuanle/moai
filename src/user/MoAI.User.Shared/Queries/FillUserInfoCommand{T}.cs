using MediatR;
using MoAI.Infra.Models;
using MoAI.User.Queries.Responses;

namespace MoAI.User.Queries;

/// <summary>
/// 用户信息查询填充.
/// </summary>
/// <typeparam name="T">带有审计属性的.</typeparam>
public class FillUserInfoCommand : IRequest<FillUserInfoCommandResponse>
{
    public IReadOnlyCollection<AuditsInfo> Items { get; init; }
}
