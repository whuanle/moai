using System;
using System.Collections.Generic;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.LoginSafety.Commands;

/// <summary>
/// 批量标记风险事件为已读（菜单未读角标随之清零），仅管理员.
/// </summary>
public class MarkRiskEventReadCommand : IRequest<EmptyCommandResponse>, IModelValidator<MarkRiskEventReadCommand>
{
    /// <summary>
    /// 事件 id 集合.
    /// </summary>
    public IReadOnlyList<Guid> EventIds { get; init; } = new List<Guid>();

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<MarkRiskEventReadCommand> validate)
    {
        validate.RuleFor(x => x.EventIds)
            .NotEmpty().WithMessage("事件 id 不能为空")
            .Must(x => x.Count <= 500).WithMessage("一次最多标记 500 条事件");
    }
}
