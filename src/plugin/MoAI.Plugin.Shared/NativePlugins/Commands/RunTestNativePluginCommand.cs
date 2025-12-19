using FluentValidation;
using MediatR;
using MoAI.Plugin.NativePlugins.Models;

namespace MoAI.Plugin.NativePlugins.Commands;

/// <summary>
/// 使用参数测试插件.
/// </summary>
public class RunTestNativePluginCommand : IRequest<RunTestNativePluginCommandResponse>, IModelValidator<RunTestNativePluginCommand>
{
    /// <summary>
    /// 插件模板 key，必填.
    /// </summary>
    public string TemplatePluginKey { get; init; } = string.Empty;

    /// <summary>
    /// 如果是工具插件，可不填写.
    /// </summary>
    public int? PluginId { get; init; }

    /// <summary>
    /// 运行参数.
    /// </summary>
    public string Params { get; init; } = string.Empty;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<RunTestNativePluginCommand> validate)
    {
        validate.RuleFor(x => x.TemplatePluginKey)
            .NotEmpty().WithMessage("插件模板 key 不能为空");

        validate.RuleFor(x => x.Params)
            .NotEmpty().WithMessage("运行参数不能为空");
    }
}
