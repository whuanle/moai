using System;
using System.Collections.Generic;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.AIChannel.Commands;

/// <summary>
/// 批量启用/禁用模型.
/// </summary>
public class BatchUpdateAIModelCommand : IRequest<EmptyCommandResponse>, IModelValidator<BatchUpdateAIModelCommand>
{
    /// <summary>
    /// 模型 id 集合.
    /// </summary>
    public List<Guid> ModelIds { get; init; } = new();

    /// <summary>
    /// 启用或禁用.
    /// </summary>
    public bool Enabled { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<BatchUpdateAIModelCommand> validate)
    {
        validate.RuleFor(x => x.ModelIds).NotEmpty().WithMessage("模型 id 集合不能为空.");
        validate.RuleFor(x => x.ModelIds).Must(x => x.Count <= 500).WithMessage("单次最多操作 500 个模型.");
    }
}
