using System;
using System.Collections.Generic;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.AIChannel.Commands;

/// <summary>
/// 更新私有模型的团队授权（全量替换），仅私有模型可设置；公开模型应使用额度接口.
/// </summary>
public class UpdateAIModelAuthorizationCommand : IRequest<EmptyCommandResponse>, IModelValidator<UpdateAIModelAuthorizationCommand>
{
    /// <summary>
    /// 模型 id，由 Controller 从路由参数回填.
    /// </summary>
    public Guid ModelId { get; set; }

    /// <summary>
    /// 授权团队 id 集合（全量替换），取消授权的团队会同步移除其额度规则.
    /// </summary>
    public List<int> TeamIds { get; set; } = new();

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<UpdateAIModelAuthorizationCommand> validate)
    {
        validate.RuleFor(x => x.TeamIds)
            .NotNull()
            .Must(ids => ids.All(id => id > 0)).WithMessage("团队 id 必须大于 0.")
            .Must(ids => ids.Distinct().Count() == ids.Count).WithMessage("团队 id 不能重复.");
    }
}
