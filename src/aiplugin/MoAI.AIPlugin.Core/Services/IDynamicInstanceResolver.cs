using MoAI.AIPlugin.Models;

namespace MoAI.AIPlugin.Services;

/// <summary>
/// 动态插件实例解析器。将动态插件实例 key 解析为其模板插件元数据与该实例的配置 JSON.
/// </summary>
public interface IDynamicInstanceResolver
{
    /// <summary>
    /// 按实例 key 解析动态插件模板与配置.
    /// </summary>
    /// <param name="instanceKey">动态插件实例 key.</param>
    /// <returns>返回解析结果；实例不存在返回 null.</returns>
    DynamicInstanceResolveResult? Resolve(string instanceKey);
}

/// <summary>
/// 动态插件实例解析结果。
/// </summary>
public class DynamicInstanceResolveResult
{
    /// <summary>
    /// 模板插件元数据.
    /// </summary>
    public PluginInfo Template { get; init; } = null!;

    /// <summary>
    /// 该实例的配置 JSON.
    /// </summary>
    public string ConfigJson { get; init; } = string.Empty;
}
