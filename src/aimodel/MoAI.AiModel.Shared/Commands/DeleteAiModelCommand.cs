using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using MoAI.Infra.Defaults;
using MoAI.Infra.Models;

namespace MoAI.AiModel.Commands;

/// <summary>
/// 删除 ai 模型.
/// </summary>
public class DeleteAiModelCommand : IRequest<EmptyCommandResponse>, IModelValidator<DeleteAiModelCommand>
{
    /// <summary>
    /// 模型 id.
    /// </summary>
    public int AiModelId { get; init; }

    /// <inheritdoc/>
    public void Validate(AbstractValidator<DeleteAiModelCommand> validate)
    {
        validate.RuleFor(x => x.AiModelId)
            .NotEmpty().WithMessage("模型id有误");
    }
}
