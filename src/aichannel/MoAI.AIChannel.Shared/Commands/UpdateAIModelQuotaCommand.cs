using System;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.AIChannel.Commands;

/// <summary>
/// 设置模型额度规则：公开模型固定 teamId=0（所有团队共享），私有模型按已授权团队单独设置.
/// </summary>
public class UpdateAIModelQuotaCommand : IRequest<EmptyCommandResponse>, IModelValidator<UpdateAIModelQuotaCommand>
{
    /// <summary>
    /// 模型 id，由 Controller 从路由参数回填.
    /// </summary>
    public Guid ModelId { get; set; }

    /// <summary>
    /// 额度主体团队 id，0=模型全局额度（公开模型）.
    /// </summary>
    public int TeamId { get; set; }

    /// <summary>
    /// 重置周期单位：0=不重置(总量一次性) 1=小时 2=天 3=周 4=月.
    /// </summary>
    public int PeriodUnit { get; set; }

    /// <summary>
    /// 重置周期长度，与 PeriodUnit 配合，例如每 8 小时=(8,1)、每天=(1,2)；PeriodUnit=0 时无效.
    /// </summary>
    public int PeriodValue { get; set; }

    /// <summary>
    /// 每个重置周期内的 tokens 上限.
    /// </summary>
    public long LimitValue { get; set; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<UpdateAIModelQuotaCommand> validate)
    {
        validate.RuleFor(x => x.TeamId).GreaterThanOrEqualTo(0).WithMessage("团队 id 不能为负数.");
        validate.RuleFor(x => x.PeriodUnit).InclusiveBetween(0, 4).WithMessage("重置周期单位只支持 0-4.");
        validate.RuleFor(x => x.PeriodValue).GreaterThan(0).When(x => x.PeriodUnit != 0).WithMessage("重置周期长度必须大于 0.");
        validate.RuleFor(x => x.LimitValue).GreaterThan(0).WithMessage("额度上限必须大于 0.");
    }
}
