using System;
using System.Collections.Generic;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.AIChannel.Commands;

/// <summary>
/// 批量删除模型（软删除）.
/// </summary>
public class BatchDeleteAIModelCommand : IRequest<EmptyCommandResponse>, IModelValidator<BatchDeleteAIModelCommand>
{
    /// <summary>
    /// 模型 id 集合.
    /// </summary>
    public List<Guid> ModelIds { get; init; } = new();

    /// <summary>
    /// 所属团队 id，0=系统渠道；由 Controller 按路由回填，请求体无需传递.
    /// </summary>
    public int TeamId { get; set; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<BatchDeleteAIModelCommand> validate)
    {
        validate.RuleFor(x => x.ModelIds).NotEmpty().WithMessage("模型 id 集合不能为空.");
        validate.RuleFor(x => x.ModelIds).Must(x => x.Count <= 500).WithMessage("单次最多操作 500 个模型.");
    }
}
