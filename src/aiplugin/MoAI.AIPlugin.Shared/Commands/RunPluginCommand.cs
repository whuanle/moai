using FluentValidation;
using MediatR;
using MoAI.AIPlugin.Models;

namespace MoAI.AIPlugin.Commands;

/// <summary>
/// 执行插件，由执行引擎按 key 解析并运行.
/// </summary>
public class RunPluginCommand : IRequest<PluginRunResult>, IModelValidator<RunPluginCommand>
{
    /// <summary>
    /// 插件 key.
    /// </summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>
    /// 自定义插件（MCP/OpenAPI）的函数名称；为空时插件仅有一个函数才可执行.
    /// </summary>
    public string? Function { get; init; }

    /// <summary>
    /// 请求参数 JSON 字符串.
    /// </summary>
    public string RequestJson { get; init; } = string.Empty;

    /// <summary>
    /// 动态插件的配置 JSON 字符串，静态插件可忽略.
    /// </summary>
    public string? ConfigJson { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<RunPluginCommand> validate)
    {
        validate.RuleFor(x => x.Key).NotEmpty().WithMessage("插件 Key 不能为空");
        validate.RuleFor(x => x.RequestJson).NotEmpty().WithMessage("请求参数不能为空");
    }
}
