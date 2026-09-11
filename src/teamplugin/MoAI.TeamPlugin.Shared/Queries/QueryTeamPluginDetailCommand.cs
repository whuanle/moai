using System;
using FluentValidation;
using MediatR;
using MoAI.AIPlugin.Queries.Responses;
using MoAI.Infra.Models;

namespace MoAI.TeamPlugin.Queries;

/// <summary>
/// 查询团队自定义插件（MCP/OpenAPI）详细信息，仅团队成员可访问.
/// </summary>
public class QueryTeamPluginDetailCommand : IUserIdContext, IRequest<QueryCustomPluginDetailCommandResponse>, IModelValidator<QueryTeamPluginDetailCommand>
{
    /// <inheritdoc/>
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    public UserType ContextUserType { get; init; }

    /// <summary>
    /// 团队 id.
    /// </summary>
    public long TeamId { get; init; }

    /// <summary>
    /// 插件记录 id.
    /// </summary>
    public Guid PluginId { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryTeamPluginDetailCommand> validate)
    {
        validate.RuleFor(x => x.TeamId).GreaterThan(0).WithMessage("团队 id 不正确.");
        validate.RuleFor(x => x.PluginId)
            .NotEmpty().WithMessage("插件 id 错误.")
            .NotEqual(Guid.Empty).WithMessage("插件 id 错误.");
    }
}
