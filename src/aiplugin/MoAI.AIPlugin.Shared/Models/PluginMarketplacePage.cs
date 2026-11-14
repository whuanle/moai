namespace MoAI.AIPlugin.Models;

/// <summary>MoPlatform 在线插件分页结果.</summary>
public sealed class PluginMarketplacePage
{
    /// <summary>插件条目.</summary>
    public IReadOnlyList<PluginMarketplaceItem> Items { get; init; } = [];

    /// <summary>总数.</summary>
    public long Total { get; init; }
}

/// <summary>在线插件摘要.</summary>
public sealed class PluginMarketplaceItem
{
    /// <summary>MoPlatform 插件 id（long 字符串）.</summary>
    public string PluginId { get; init; } = string.Empty;

    /// <summary>插件 key.</summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>插件名.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>人读简介，用于在线插件列表和卡片展示.</summary>
    public string Summary { get; init; } = string.Empty;

    /// <summary>当前平台图标地址，仅用于预览；安装后使用包内图标并保存到 MoAI OSS.</summary>
    public string IconUrl { get; init; } = string.Empty;

    /// <summary>最新包版本.</summary>
    public string LatestVersion { get; init; } = string.Empty;

    /// <summary>平台下载计数.</summary>
    public string DownloadCount { get; init; } = "0";

    /// <summary>是否已在本机安装.</summary>
    public bool IsInstalled { get; init; }

    /// <summary>MoPlatform 分类 id.</summary>
    public string CategoryId { get; init; } = string.Empty;

    /// <summary>MoPlatform 分类名称.</summary>
    public string CategoryName { get; init; } = string.Empty;

    /// <summary>MoPlatform 分类图标.</summary>
    public string CategoryEmoji { get; init; } = string.Empty;
}

/// <summary>MoPlatform 在线插件分类.</summary>
public sealed class PluginMarketplaceCategory
{
    /// <summary>分类 id（long 字符串）.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>分类名称.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>分类图标.</summary>
    public string Emoji { get; init; } = string.Empty;
}
