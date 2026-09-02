using System;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.AIChannel.Commands;

/// <summary>
/// 删除 AI 模型.
/// </summary>
public class DeleteAIModelCommand : IRequest<EmptyCommandResponse>, IModelValidator<DeleteAIModelCommand>
{
    /// <summary>
    /// 模型 id.
    /// </summary>
    public Guid ModelId { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<DeleteAIModelCommand> validate)
    {
        validate.RuleFor(x => x.ModelId).NotEmpty().WithMessage("模型 id 不能为空.");
    }
}
