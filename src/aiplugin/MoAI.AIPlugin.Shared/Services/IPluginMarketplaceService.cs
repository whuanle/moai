using MoAI.AIPlugin.Models;
using MoAI.AIPlugin.Queries.Responses;

namespace MoAI.AIPlugin.Services;

/// <summary>MoPlatform 在线插件目录与本地安装生命周期服务.</summary>
public interface IPluginMarketplaceService
{
    /// <summary>搜索在线插件目录.</summary>
    Task<PluginMarketplacePage> QueryCatalogAsync(string? keyword, string? categoryId, int pageNo, int pageSize, CancellationToken cancellationToken);

    /// <summary>查询 MoPlatform 插件分类.</summary>
    Task<IReadOnlyList<PluginMarketplaceCategory>> QueryCategoriesAsync(CancellationToken cancellationToken);

    /// <summary>安装已上架插件包.</summary>
    Task<InstalledPluginItem> InstallAsync(long platformPluginId, CancellationToken cancellationToken);

    /// <summary>查询本地安装插件.</summary>
    Task<IReadOnlyList<InstalledPluginItem>> QueryInstalledAsync(CancellationToken cancellationToken);

    /// <summary>设置本地插件上架状态，不删除文件、不卸载程序集.</summary>
    Task<InstalledPluginItem> SetEnabledAsync(string pluginKey, bool enabled, CancellationToken cancellationToken);
}
