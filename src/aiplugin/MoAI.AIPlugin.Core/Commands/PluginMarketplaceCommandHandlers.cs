using MediatR;
using MoAI.AIPlugin.Commands;
using MoAI.AIPlugin.Models;
using MoAI.AIPlugin.Queries.Responses;
using MoAI.AIPlugin.Services;

namespace MoAI.AIPlugin.Handlers;

/// <summary>在线插件目录查询.</summary>
public sealed class QueryPluginMarketplaceCommandHandler : IRequestHandler<QueryPluginMarketplaceCommand, PluginMarketplacePage>
{
    private readonly IPluginMarketplaceService _service;
    public QueryPluginMarketplaceCommandHandler(IPluginMarketplaceService service) => _service = service;

    public Task<PluginMarketplacePage> Handle(QueryPluginMarketplaceCommand request, CancellationToken cancellationToken) =>
        _service.QueryCatalogAsync(request.Keyword, request.CategoryId, request.PageNo, request.PageSize, cancellationToken);
}

/// <summary>MoPlatform 在线插件分类查询.</summary>
public sealed class QueryPluginMarketplaceCategoriesCommandHandler : IRequestHandler<QueryPluginMarketplaceCategoriesCommand, IReadOnlyList<PluginMarketplaceCategory>>
{
    private readonly IPluginMarketplaceService _service;
    public QueryPluginMarketplaceCategoriesCommandHandler(IPluginMarketplaceService service) => _service = service;

    public Task<IReadOnlyList<PluginMarketplaceCategory>> Handle(QueryPluginMarketplaceCategoriesCommand request, CancellationToken cancellationToken) =>
        _service.QueryCategoriesAsync(cancellationToken);
}

/// <summary>在线插件安装.</summary>
public sealed class InstallOnlinePluginCommandHandler : IRequestHandler<InstallOnlinePluginCommand, InstalledPluginItem>
{
    private readonly IPluginMarketplaceService _service;
    public InstallOnlinePluginCommandHandler(IPluginMarketplaceService service) => _service = service;

    public Task<InstalledPluginItem> Handle(InstallOnlinePluginCommand request, CancellationToken cancellationToken) =>
        _service.InstallAsync(request.PlatformPluginId, cancellationToken);
}

/// <summary>本地在线插件列表.</summary>
public sealed class QueryInstalledPluginsCommandHandler : IRequestHandler<QueryInstalledPluginsCommand, IReadOnlyList<InstalledPluginItem>>
{
    private readonly IPluginMarketplaceService _service;
    public QueryInstalledPluginsCommandHandler(IPluginMarketplaceService service) => _service = service;

    public Task<IReadOnlyList<InstalledPluginItem>> Handle(QueryInstalledPluginsCommand request, CancellationToken cancellationToken) =>
        _service.QueryInstalledAsync(cancellationToken);
}

/// <summary>在线插件上架/下架.</summary>
public sealed class SetOnlinePluginStatusCommandHandler : IRequestHandler<SetOnlinePluginStatusCommand, InstalledPluginItem>
{
    private readonly IPluginMarketplaceService _service;
    public SetOnlinePluginStatusCommandHandler(IPluginMarketplaceService service) => _service = service;

    public Task<InstalledPluginItem> Handle(SetOnlinePluginStatusCommand request, CancellationToken cancellationToken) =>
        _service.SetEnabledAsync(request.PluginKey, request.Enabled, cancellationToken);
}
