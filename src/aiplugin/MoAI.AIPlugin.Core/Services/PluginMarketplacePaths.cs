using Microsoft.Extensions.Configuration;

namespace MoAI.AIPlugin.Services;

/// <summary>在线插件包目录与本地元数据布局.</summary>
public static class PluginMarketplacePaths
{
    /// <summary>MoAI 包元数据文件名.</summary>
    public const string MetadataFileName = "moai.plugin.json";

    /// <summary>解析插件目录；默认定位部署/仓库根目录下的 configs/plugins.</summary>
    public static string ResolvePluginDirectory(IConfiguration configuration)
    {
        var configured = configuration["MoAI:PluginMarketplace:Directory"];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return Path.GetFullPath(configured, Directory.GetCurrentDirectory());
        }

        for (var current = new DirectoryInfo(Directory.GetCurrentDirectory()); current != null; current = current.Parent)
        {
            var configs = Path.Combine(current.FullName, "configs");
            if (Directory.Exists(configs))
            {
                return Path.Combine(configs, "plugins");
            }
        }

        return Path.Combine(AppContext.BaseDirectory, "configs", "plugins");
    }

    internal static IEnumerable<string> EnumeratePackageDirectories(string root)
    {
        if (!Directory.Exists(root))
        {
            return [];
        }

        return Directory.EnumerateDirectories(root)
            .Where(directory => !Path.GetFileName(directory).StartsWith(".", StringComparison.Ordinal))
            .OrderBy(directory => directory, StringComparer.OrdinalIgnoreCase);
    }
}

/// <summary>本地安装插件元数据，保存为包目录下的 moai.plugin.json.</summary>
public sealed record InstalledPluginMetadata
{
    /// <summary>插件 key.</summary>
    public string PluginKey { get; init; } = string.Empty;

    /// <summary>插件名称.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>人读用途简介.</summary>
    public string Summary { get; init; } = string.Empty;

    /// <summary>AI 使用提示，保留插件能力、参数、限制和调用建议.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>分类.</summary>
    public string Category { get; init; } = string.Empty;

    /// <summary>当前安装版本.</summary>
    public string Version { get; init; } = string.Empty;

    /// <summary>MoPlatform 插件 id.</summary>
    public long PlatformPluginId { get; init; }

    /// <summary>MoPlatform 插件版本 id.</summary>
    public long PlatformVersionId { get; init; }

    /// <summary>插件包 SHA-256.</summary>
    public string PackageSha256 { get; init; } = string.Empty;

    /// <summary>入口程序集相对路径.</summary>
    public string EntryAssembly { get; init; } = string.Empty;

    /// <summary>插件根目录.</summary>
    public string RootPath { get; init; } = string.Empty;

    /// <summary>头像对象路径.</summary>
    public string AvatarPath { get; init; } = string.Empty;

    /// <summary>插件是否启用.</summary>
    public bool Enabled { get; init; }

    /// <summary>安装时间.</summary>
    public DateTimeOffset InstalledAt { get; init; }
}
