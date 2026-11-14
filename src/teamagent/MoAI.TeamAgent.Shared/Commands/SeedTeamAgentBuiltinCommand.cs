using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.TeamAgent.Commands;

/// <summary>
/// 为全部存量团队补种内置智能体（规划师/构建师，按名称幂等），仅平台管理员可调用.
/// </summary>
public class SeedTeamAgentBuiltinCommand : IRequest<SimpleInt>, IModelValidator<SeedTeamAgentBuiltinCommand>, IUserIdContext
{
    /// <inheritdoc/>
    [JsonIgnore]
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<SeedTeamAgentBuiltinCommand> validate)
    {
    }
}
