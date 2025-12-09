using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Prompt.Commands;

/// <summary>
/// 修改提示词分类.
/// </summary>
public class UpdatePromptClassifyCommand : IRequest<EmptyCommandResponse>, IModelValidator<UpdatePromptClassifyCommand>
{
    /// <summary>
    /// 分类 id.
    /// </summary>
    public int ClassifyId { get; init; }

    /// <summary>
    /// 分类名称.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 分类描述.
    /// </summary>
    public string? Description { get; init; } = string.Empty;

    /// <inheritdoc/>
    public void Validate(AbstractValidator<UpdatePromptClassifyCommand> validate)
    {
        validate.RuleFor(x => x.ClassifyId).NotEmpty().WithMessage("分类ID不能为空");
        validate.RuleFor(x => x.Name)
            .NotEmpty().WithMessage("名称不能为空")
            .MaximumLength(20).WithMessage("名称不能超过20个字符");
    }
}
