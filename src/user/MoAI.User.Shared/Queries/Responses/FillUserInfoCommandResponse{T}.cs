using MediatR;
using MoAI.Infra.Models;

namespace MoAI.User.Queries.Responses;
public class FillUserInfoCommandResponse
{
    public IReadOnlyCollection<AuditsInfo> Items { get; init; }
}