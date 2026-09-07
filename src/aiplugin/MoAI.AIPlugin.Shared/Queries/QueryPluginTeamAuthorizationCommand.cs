using System;
using FluentValidation;
using MediatR;
using MoAI.AIPlugin.Queries.Responses;
using MoAI.Infra.Models;

namespace MoAI.AIPlugin.Queries;

/// <summary>
/// 查询私有系统插件的团队授权列表；公开插件返回 isPublic=true 且 items 为空.
/// </summary>
public class QueryPluginTeamAuthorizationCommand : IRequest<QueryPluginTeamAuthorizationCommandResponse>, IModelValidator<QueryPluginTeamAuthorizationCommand>
{
    /// <summary>
    /// 系统插件记录 id，由 Controller 从路由参数回填.
    /// </summary>
    public Guid PluginId { get; set; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryPluginTeamAuthorizationCommand> validate)
    {
        // PluginId 由 Controller 从路由参数回填，自动验证发生在回填之前，因此此处不校验.
    }
}
