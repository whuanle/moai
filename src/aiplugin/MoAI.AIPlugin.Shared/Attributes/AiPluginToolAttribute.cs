using System;

namespace MoAI.AIPlugin.Attributes;

/// <summary>
/// 标记动态插件模板类中的一个工具方法：一个模板可通过多个本特性方法对外暴露多个工具.
/// 方法签名约定：<c>Task&lt;TResponse&gt; XxxAsync(TRequest request, CancellationToken cancellationToken)</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class AiPluginToolAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AiPluginToolAttribute"/> class.
    /// </summary>
    /// <param name="key">工具唯一标识（模板内唯一，小写+下划线，不区分大小写）.</param>
    public AiPluginToolAttribute(string key)
    {
        Key = key;
    }

    /// <summary>
    /// 工具唯一标识（模板内唯一）.
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// 工具展示标题.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// 工具描述（进入模型工具说明）.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// 参数运行示例 JSON（可选项；缺省时读取请求类型上的 static <c>GetParamsExampleValue()</c>）.
    /// </summary>
    public string? ParamsExample { get; init; }
}
