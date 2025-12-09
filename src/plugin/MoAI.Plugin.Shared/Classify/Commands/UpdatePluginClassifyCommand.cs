using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Plugin.Classify.Commands;

/// <summary>
/// 修改插件分类.
/// </summary>
public class UpdatePluginClassifyCommand : IRequest<EmptyCommandResponse>, IModelValidator<UpdatePluginClassifyCommand>
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

    /// <summary>
    ///  验证模型.
    /// </summary>
    /// <param name="validate">验证器.</param>
    public void Validate(FluentValidation.AbstractValidator<UpdatePluginClassifyCommand> validate)
    {
        validate.RuleFor(x => x.ClassifyId).NotEmpty().WithMessage("分类id不正确.");

        validate.RuleFor(x => x.Name).NotEmpty().WithMessage("分类名称不能为空.")
            .MaximumLength(10).WithMessage("分类名称不能超过10个字符.");

        validate.RuleFor(x => x.Description).MaximumLength(255).WithMessage("分类描述不能超过255个字符.");
    }
}
