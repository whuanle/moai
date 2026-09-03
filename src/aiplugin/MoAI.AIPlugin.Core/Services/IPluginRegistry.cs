using System.Collections.Generic;
using System.Reflection;
using MoAI.AIPlugin.Models;

namespace MoAI.AIPlugin.Services;

/// <summary>
/// 插件注册与发现服务。负责扫描插件程序集、生成并缓存 <see cref="PluginInfo"/> 元数据.
/// </summary>
public interface IPluginRegistry
{
    /// <summary>
    /// 获取全部已发现插件.
    /// </summary>
    /// <returns>插件元数据列表.</returns>
    IReadOnlyList<PluginInfo> GetAll();

    /// <summary>
    /// 按 key 获取插件元数据，不存在返回 null.
    /// </summary>
    /// <param name="key">插件 key.</param>
    /// <returns>返回 <see cref="PluginInfo"/>.</returns>
    PluginInfo? Get(string key);

    /// <summary>
    /// 手动注册一个插件程序集（用于动态加载尚未被 CLR 加载的程序集）.
    /// </summary>
    /// <param name="assembly">插件程序集.</param>
    void RegisterAssembly(Assembly assembly);
}
