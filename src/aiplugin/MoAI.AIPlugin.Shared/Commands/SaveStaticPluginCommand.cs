using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.AIPlugin.Commands;

/// <summary>
/// 保存/写回静态插件信息。静态插件默认无 DB 记录，首次保存创建记录，之后更新.
/// </summary>
public class SaveStaticPluginCommand : IRequest<EmptyCommandResponse>, IModelValidator<SaveStaticPluginCommand>
{
    /// <summary>
    /// 静态插件 key（注册表唯一标识，不可变）.
    /// </summary>
    public string PluginKey { get; init; } = string.Empty;

    /// <summary>
    /// 插件标题.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// 描述.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// 分类 id，0 表示未分类.
    /// </summary>
    public int ClassifyId { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<SaveStaticPluginCommand> validate)
    {
        validate.RuleFor(x => x.PluginKey).NotEmpty().WithMessage("插件 Key 不能为空");
        validate.RuleFor(x => x.Title).NotEmpty().WithMessage("插件标题不能为空").MaximumLength(50).WithMessage("插件标题长度不能超过 50");
        validate.RuleFor(x => x.Description).MaximumLength(255).WithMessage("描述长度不能超过 255");
    }
}
