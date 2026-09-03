using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.AIPlugin.Commands;

/// <summary>
/// 保存动态插件实例。创建时填实例 key + 模板 key + 配置；更新时实例 key 不可变.
/// </summary>
public class SaveDynamicPluginCommand : IRequest<EmptyCommandResponse>, IModelValidator<SaveDynamicPluginCommand>
{
    /// <summary>
    /// 实例 key（用户填，小写+下划线，最长 30，全局唯一）；更新时不可变.
    /// </summary>
    public string PluginKey { get; init; } = string.Empty;

    /// <summary>
    /// 模板 key（后端代码模型的 key，如 dynamic_greet）.
    /// </summary>
    public string TempleteKey { get; init; } = string.Empty;

    /// <summary>
    /// 实例标题（展示名称）.
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

    /// <summary>
    /// 实例配置 JSON.
    /// </summary>
    public string Config { get; init; } = string.Empty;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<SaveDynamicPluginCommand> validate)
    {
        validate.RuleFor(x => x.PluginKey)
            .NotEmpty().WithMessage("实例 Key 不能为空")
            .Matches("^[a-z_][a-z0-9_]*$").WithMessage("实例 Key 只能是小写字母、数字和下划线，且不能以数字开头")
            .MaximumLength(30).WithMessage("实例 Key 长度不能超过 30");

        validate.RuleFor(x => x.TempleteKey)
            .NotEmpty().WithMessage("模板 Key 不能为空");

        validate.RuleFor(x => x.Title)
            .NotEmpty().WithMessage("实例标题不能为空")
            .MaximumLength(30).WithMessage("实例标题长度不能超过 30");

        validate.RuleFor(x => x.Description)
            .MaximumLength(255).WithMessage("描述长度不能超过 255");
    }
}
