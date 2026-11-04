using System;
using FluentValidation;
using MediatR;
using MoAI.AIPlugin.Queries.Responses;
using MoAI.Infra.Models;

namespace MoAI.TeamPlugin.Queries;

/// <summary>
/// 查询团队自有插件的语义配置（使用须知/语义条目/工具元数据/工具清单），仅团队成员可访问.
/// </summary>
public class QueryTeamPluginSemanticCommand : IRequest<QueryPluginSemanticCommandResponse>, IModelValidator<QueryTeamPluginSemanticCommand>, IUserIdContext
{
    /// <inheritdoc/>
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    public UserType ContextUserType { get; init; }

    /// <summary>
    /// 团队 id，由路由回填（自动验证先于回填，不可在此校验）.
    /// </summary>
    public long TeamId { get; init; }

    /// <summary>
    /// 插件记录 id，由路由回填.
    /// </summary>
    public Guid PluginId { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryTeamPluginSemanticCommand> validate)
    {
        // TeamId/PluginId 由路由回填，缺失时 Handler 统一 404
    }
}
