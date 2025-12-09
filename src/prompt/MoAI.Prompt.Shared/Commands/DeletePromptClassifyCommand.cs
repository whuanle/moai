using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Prompt.Commands;

/// <summary>
/// 删除提示词分类.
/// </summary>
public class DeletePromptClassifyCommand : IRequest<EmptyCommandResponse>, IModelValidator<DeletePromptClassifyCommand>
{
    /// <summary>
    /// 分类 id.
    /// </summary>
    public int ClassifyId { get; init; }

    /// <inheritdoc/>
    public void Validate(AbstractValidator<DeletePromptClassifyCommand> validate)
    {
        validate.RuleFor(x => x.ClassifyId).NotEmpty().WithMessage("分类ID不能为空");
    }
}
