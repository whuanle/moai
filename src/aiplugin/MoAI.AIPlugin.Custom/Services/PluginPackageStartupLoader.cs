using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MoAI.AIPlugin.Services;
using MoAI.Database;

namespace MoAI.AIPlugin.Custom.Services;

/// <summary>启动时扫描 configs/plugins 下每个插件目录并加载主程序集；单包失败不会阻断宿主.</summary>
public sealed class PluginPackageStartupLoader : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IPluginRegistry _registry;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PluginPackageStartupLoader> _logger;
    private readonly List<PluginPackageLoadContext> _loadContexts = [];

    public PluginPackageStartupLoader(
        IServiceScopeFactory scopeFactory,
        IPluginRegistry registry,
        IConfiguration configuration,
        ILogger<PluginPackageStartupLoader> logger)
    {
        _scopeFactory = scopeFactory;
        _registry = registry;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var root = PluginMarketplacePaths.ResolvePluginDirectory(_configuration);
        Directory.CreateDirectory(root);
        await using var scope = _scopeFactory.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
        var packages = await database.PluginPackages
            .AsNoTracking()
            .ToDictionaryAsync(x => x.PluginKey, StringComparer.OrdinalIgnoreCase, cancellationToken)
            .ConfigureAwait(false);
        foreach (var directory in PluginMarketplacePaths.EnumeratePackageDirectories(root))
        {
            var pluginKey = Path.GetFileName(directory);
            try
            {
                var metadataPath = Path.Combine(directory, PluginMarketplacePaths.MetadataFileName);
                if (!File.Exists(metadataPath))
                {
                    _logger.LogWarning("插件目录缺少本地元数据，已跳过。PluginKey={PluginKey}, Directory={Directory}", pluginKey, directory);
                    continue;
                }

                var metadata = PluginPackageRuntimeLoader.DeserializeMetadata(
                    await File.ReadAllTextAsync(metadataPath, cancellationToken).ConfigureAwait(false));
                packages.TryGetValue(metadata.PluginKey, out var package);
                _loadContexts.Add(PluginPackageRuntimeLoader.Load(
                    directory,
                    metadata.PluginKey,
                    package?.PackageHash ?? metadata.PackageSha256,
                    package?.EntryAssembly ?? metadata.EntryAssembly,
                    _registry,
                    package?.Enabled ?? metadata.Enabled));
                _logger.LogInformation(
                    "已从本地插件目录加载主程序集 {PluginKey} {Version} Enabled={Enabled}",
                    metadata.PluginKey,
                    metadata.Version,
                    package?.Enabled ?? metadata.Enabled);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "在线插件包加载失败，已隔离该包且不影响宿主启动。PluginKey={PluginKey}", pluginKey);
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
