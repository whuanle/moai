using System;
using System.Collections.Generic;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.AIPlugin.Commands;

/// <summary>
/// 更新私有系统插件的团队授权（全量替换），仅私有插件可设置；公开插件应使用公开状态接口.
/// </summary>
public class UpdatePluginTeamAuthorizationCommand : IRequest<EmptyCommandResponse>, IModelValidator<UpdatePluginTeamAuthorizationCommand>
{
    /// <summary>
    /// 系统插件记录 id，由 Controller 从路由参数回填.
    /// </summary>
    public Guid PluginId { get; set; }

    /// <summary>
    /// 授权团队 id 集合（全量替换），取消授权的团队将无法继续使用该私有插件.
    /// </summary>
    public List<int> TeamIds { get; set; } = new();

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<UpdatePluginTeamAuthorizationCommand> validate)
    {
        validate.RuleFor(x => x.TeamIds)
            .NotNull()
            .Must(ids => ids.All(id => id > 0)).WithMessage("团队 id 必须大于 0.")
            .Must(ids => ids.Distinct().Count() == ids.Count).WithMessage("团队 id 不能重复.");
    }
}
