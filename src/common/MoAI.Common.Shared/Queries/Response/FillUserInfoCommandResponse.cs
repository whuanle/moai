using MoAI.Infra.Models;

namespace MoAI.Common.Queries.Response;

public class FillUserInfoCommandResponse
{
    public required IReadOnlyCollection<AuditsInfo> Items { get; init; }
}