using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Prompt.Commands;

/// <summary>
/// 更新提示词.
/// </summary>
public class UpdatePromptCommand : IRequest<EmptyCommandResponse>, IModelValidator<UpdatePromptCommand>
{
    /// <summary>
    /// 新的分类id.
    /// </summary>
    public int PromptClassId { get; init; }

    /// <summary>
    /// 提示词 id.
    /// </summary>
    public int PromptId { get; init; }

    /// <summary>
    /// 名称.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// 描述.
    /// </summary>
    public string Description { get; init; } = default!;

    /// <summary>
    /// 助手设定,markdown.
    /// </summary>
    public string Content { get; init; } = default!;

    /// <summary>
    /// 是否公开.
    /// </summary>
    public bool IsPublic { get; init; }

    /// <inheritdoc/>
    public void Validate(AbstractValidator<UpdatePromptCommand> validate)
    {
        validate.RuleFor(x => x.PromptId).NotEmpty().WithMessage("提示词ID不能为空");
        validate.RuleFor(x => x.Name)
            .NotEmpty().WithMessage("名称不能为空")
            .MaximumLength(50).WithMessage("名称不能超过50个字符");
        validate.RuleFor(x => x.Description)
            .NotEmpty().WithMessage("描述不能为空")
            .MaximumLength(200).WithMessage("描述不能超过200个字符");
        validate.RuleFor(x => x.PromptClassId).NotEmpty().WithMessage("分类ID不能为空");
    }
}
