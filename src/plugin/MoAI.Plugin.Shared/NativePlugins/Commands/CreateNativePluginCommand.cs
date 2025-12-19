using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Plugin.NativePlugins.Commands;

/// <summary>
/// 创建内置插件实例.
/// </summary>
public class CreateNativePluginCommand : IRequest<SimpleInt>, IModelValidator<CreateNativePluginCommand>
{
    /// <summary>
    /// 内置插件模板 key.
    /// </summary>
    public string TemplatePluginKey { get; init; } = string.Empty;

    /// <summary>
    /// 插件名称，只能纯字母，用于给 AI 使用.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// 插件标题，可中文.
    /// </summary>
    public string Title { get; init; } = default!;

    /// <summary>
    /// 描述.
    /// </summary>
    public string Description { get; init; } = default!;

    /// <summary>
    /// 分类 id.
    /// </summary>
    public int ClassifyId { get; init; }

    /// <summary>
    /// 是否公开.
    /// </summary>
    public bool IsPublic { get; init; } = default!;

    /// <summary>
    /// 参数.
    /// </summary>
    public string Config { get; init; } = string.Empty!;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<CreateNativePluginCommand> validate)
    {
        validate.RuleFor(x => x.Title)
            .NotEmpty().WithMessage("插件标题长度在 2-30 之间.")
            .Length(2, 30).WithMessage("插件标题长度在 2-30 之间.");

        validate.RuleFor(x => x.TemplatePluginKey)
            .NotEmpty().WithMessage("内置插件模板 key 不正确.");

        validate.RuleFor(x => x.Config)
            .NotEmpty().WithMessage("插件配置不能为空.");

        validate.RuleFor(x => x.ClassifyId).NotEmpty().WithMessage("分类id不正确.");

        validate.RuleFor(x => x.Name)
            .NotEmpty().WithMessage("插件名称长度在 2-30 之间.")
            .Length(2, 30).WithMessage("插件名称长度在 2-30 之间.")
            .Matches("^[a-zA-Z_]+$").WithMessage("插件名称只能包含字母下划线.");

        validate.RuleFor(x => x.Description).MaximumLength(255).WithMessage("分类描述不能超过255个字符.");
    }
}