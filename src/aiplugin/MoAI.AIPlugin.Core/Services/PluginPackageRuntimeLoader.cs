using System.Reflection;
using System.Text.Json;
using MoAI.AIPlugin.Models;
using MoAI.AIPlugin.Services;
using MoAI.Infra.Exceptions;

namespace MoAI.AIPlugin.Services;

/// <summary>已安装插件包加载与注册入口；加载上下文不可回收，运行时不提供卸载能力.</summary>
public static class PluginPackageRuntimeLoader
{
    /// <summary>加载并注册已安装包.</summary>
    public static PluginPackageLoadContext Load(
        string pluginDirectory,
        string pluginKey,
        string packageHash,
        string entryAssemblyRelativePath,
        IPluginRegistry registry,
        bool enabled)
    {
        var fullDirectory = Path.GetFullPath(pluginDirectory);
        if (!string.Equals(Path.GetFileName(fullDirectory), pluginKey, StringComparison.Ordinal))
        {
            throw new BusinessException("插件包目录名与数据库 pluginKey 不一致.") { StatusCode = 400 };
        }

        // 先触发注册表扫描并检查冲突，再加载当前 package assembly；否则首次 GetAll 会把刚加载的下架包
        // 当作普通 AppDomain 程序集以默认 enabled=true 注册。
        if (registry.IsRegistered(pluginKey))
        {
            throw new BusinessException($"插件 key 已由其他程序集注册：{pluginKey}.") { StatusCode = 409 };
        }

        var metadataPath = Path.Combine(fullDirectory, PluginMarketplacePaths.MetadataFileName);
        var metadata = DeserializeMetadata(File.ReadAllText(metadataPath));
        if (!string.Equals(metadata.PluginKey, pluginKey, StringComparison.Ordinal)
            || !string.Equals(metadata.PackageSha256, packageHash, StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException("插件包本地元数据与数据库安装快照不一致.") { StatusCode = 400 };
        }

        var entryPath = Path.GetFullPath(Path.Combine(fullDirectory, entryAssemblyRelativePath.Replace('/', Path.DirectorySeparatorChar)));
        var prefix = fullDirectory + Path.DirectorySeparatorChar;
        if (!entryPath.StartsWith(prefix, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)
            || !File.Exists(entryPath))
        {
            throw new BusinessException("插件包入口程序集不存在或路径越界.") { StatusCode = 400 };
        }

        var loadContext = new PluginPackageLoadContext(entryPath);
        var assembly = loadContext.LoadFromAssemblyPath(entryPath);
        var plugins = GetPluginInfos(assembly);
        if (plugins.Count != 1 || !string.Equals(plugins[0].Key, pluginKey, StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException("插件包必须且只能声明一个与安装 key 一致的 [AiPlugin] 插件.") { StatusCode = 400 };
        }

        registry.RegisterAssembly(assembly, enabled);
        return loadContext;
    }

    /// <summary>Reads the package snapshot using the same web JSON conventions used during installation.</summary>
    internal static InstalledPluginMetadata DeserializeMetadata(string json) =>
        JsonSerializer.Deserialize<InstalledPluginMetadata>(json, JsonSerializerOptions.Web)
        ?? throw new BusinessException("插件包本地元数据无效.") { StatusCode = 400 };

    private static IReadOnlyList<PluginInfo> GetPluginInfos(Assembly assembly)
    {
        var result = new List<PluginInfo>();
        foreach (var type in assembly.GetTypes())
        {
            if (PluginTypeHelper.TryGetPluginInfo(type) is { } info)
            {
                result.Add(info);
            }
        }

        return result;
    }
}
