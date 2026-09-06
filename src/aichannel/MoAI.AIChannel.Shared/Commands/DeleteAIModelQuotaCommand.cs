using System;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.AIChannel.Commands;

/// <summary>
/// 移除模型额度规则：公开模型移除全局额度（teamId=0），私有模型移除指定团队额度；移除后不再限额.
/// </summary>
public class DeleteAIModelQuotaCommand : IRequest<EmptyCommandResponse>, IModelValidator<DeleteAIModelQuotaCommand>
{
    /// <summary>
    /// 模型 id，由 Controller 从路由参数回填.
    /// </summary>
    public Guid ModelId { get; set; }

    /// <summary>
    /// 额度主体团队 id，0=模型全局额度（公开模型）.
    /// </summary>
    public int TeamId { get; set; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<DeleteAIModelQuotaCommand> validate)
    {
        validate.RuleFor(x => x.TeamId).GreaterThanOrEqualTo(0).WithMessage("团队 id 不能为负数.");
    }
}
