using System;
using System.Collections.Generic;
using System.Linq;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.AIChannel.Commands;

/// <summary>
/// 渠道级批量授权（增量语义，幂等）：对渠道下所有私有模型补齐授权与取消授权，公开模型不受影响.
/// 授权：缺失的补齐，已授权的跳过；取消：存在的移除，未授权的跳过。模型间授权不一致时不会被拉平.
/// </summary>
public class UpdateAIChannelAuthorizationCommand : IRequest<EmptyCommandResponse>, IModelValidator<UpdateAIChannelAuthorizationCommand>
{
    /// <summary>
    /// 渠道 id，由 Controller 从路由参数回填.
    /// </summary>
    public Guid ChannelId { get; set; }

    /// <summary>
    /// 待授权团队 id 集合（补齐渠道下所有私有模型缺失的授权）.
    /// </summary>
    public List<int> GrantTeamIds { get; set; } = new();

    /// <summary>
    /// 待取消授权团队 id 集合（移除渠道下所有私有模型的对应授权）.
    /// </summary>
    public List<int> RevokeTeamIds { get; set; } = new();

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<UpdateAIChannelAuthorizationCommand> validate)
    {
        // ChannelId 由 Controller 从路由参数回填，不参与请求体校验.
        validate.RuleFor(x => x.GrantTeamIds)
            .NotNull()
            .Must(ids => ids.All(id => id > 0)).WithMessage("团队 id 必须大于 0.")
            .Must(ids => ids.Distinct().Count() == ids.Count).WithMessage("团队 id 不能重复.");
        validate.RuleFor(x => x.RevokeTeamIds)
            .NotNull()
            .Must(ids => ids.All(id => id > 0)).WithMessage("团队 id 必须大于 0.")
            .Must(ids => ids.Distinct().Count() == ids.Count).WithMessage("团队 id 不能重复.");
    }
}
