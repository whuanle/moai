using FluentValidation;
using MediatR;
using MoAI.AIPlugin.Models;
using MoAI.Infra.Models;

namespace MoAI.AIPlugin.Commands;

/// <summary>
/// 执行插件，由执行引擎按 key 解析并运行.
/// </summary>
public class RunPluginCommand : IRequest<PluginRunResult>, IModelValidator<RunPluginCommand>, IUserIdContext
{
    /// <inheritdoc/>
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    public UserType ContextUserType { get; init; }

    /// <summary>
    /// 插件 key.
    /// </summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>
    /// 工具标识：自定义插件（MCP/OpenAPI）为函数名称；多工具动态插件为 <c>[AiPluginTool]</c> 的工具 key；
    /// 为空时单工具插件可执行.
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
