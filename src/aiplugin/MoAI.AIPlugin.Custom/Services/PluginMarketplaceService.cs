using System.Security.Cryptography;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MoAI.Account.Services;
using MoAI.AIPlugin.Commands;
using MoAI.AIPlugin.Models;
using MoAI.AIPlugin.Queries.Responses;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.AIPlugin.Services;
using MoAI.Storage.Commands;
using MoAI.Storage.Services;

namespace MoAI.AIPlugin.Custom.Services;

/// <summary>在线插件查询、安装与下架生命周期；任何状态变更都不删除插件包或卸载程序集.</summary>
public sealed partial class PluginMarketplaceService : IPluginMarketplaceService
{
    private readonly DatabaseContext _databaseContext;
    private readonly MoAI.Infra.MoPlatformPluginClient _platformClient;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IPluginRegistry _registry;
    private readonly IStorageService _storageService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PluginMarketplaceService> _logger;
    private readonly IUserInfoFillService _userInfoFillService;

    public PluginMarketplaceService(
        DatabaseContext databaseContext,
        MoAI.Infra.MoPlatformPluginClient platformClient,
        IHttpClientFactory httpClientFactory,
        IPluginRegistry registry,
        IStorageService storageService,
        IUserInfoFillService userInfoFillService,
        IConfiguration configuration,
        ILogger<PluginMarketplaceService> logger)
    {
        _databaseContext = databaseContext;
        _platformClient = platformClient;
        _httpClientFactory = httpClientFactory;
        _registry = registry;
        _storageService = storageService;
        _userInfoFillService = userInfoFillService;
        _configuration = configuration;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<PluginMarketplacePage> QueryCatalogAsync(string? keyword, string? categoryId, int pageNo, int pageSize, CancellationToken cancellationToken)
    {
        using var pageDocument = await _platformClient.GetPublishedPluginsAsync(keyword, categoryId, pageNo, pageSize, cancellationToken).ConfigureAwait(false);
        var pageRoot = pageDocument.RootElement;
        var pageItems = new List<PluginMarketplaceItem>();
        if (TryGet(pageRoot, "items", out var onlineItems) && onlineItems.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in onlineItems.EnumerateArray())
            {
                pageItems.Add(new PluginMarketplaceItem
                {
                    PluginId = ReadString(item, "pluginId"),
                    Key = ReadString(item, "key"),
                    Name = ReadString(item, "name"),
                    // summary is the human-readable purpose shown in marketplace listings.
                    Summary = ReadString(item, "summary"),
                    IconUrl = ReadString(item, "iconUrl"),
                    LatestVersion = ReadString(item, "latestVersion"),
                    DownloadCount = ReadString(item, "downloadCount", "0"),
                    CategoryId = ReadString(item, "categoryId"),
                    CategoryName = ReadString(item, "categoryName"),
                    CategoryEmoji = ReadString(item, "categoryEmoji"),
                });
            }
        }

        var installedKeys = (await _databaseContext.PluginPackages
                .Select(x => x.PluginKey)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return new PluginMarketplacePage
        {
            Total = ReadLong(pageRoot, "total"),
            Items = pageItems.Select(x => new PluginMarketplaceItem
            {
                PluginId = x.PluginId,
                Key = x.Key,
                Name = x.Name,
                Summary = x.Summary,
                IconUrl = x.IconUrl,
                LatestVersion = x.LatestVersion,
                DownloadCount = x.DownloadCount,
                IsInstalled = installedKeys.Contains(x.Key),
                CategoryId = x.CategoryId,
                CategoryName = x.CategoryName,
                CategoryEmoji = x.CategoryEmoji,
            }).ToArray(),
        };
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PluginMarketplaceCategory>> QueryCategoriesAsync(CancellationToken cancellationToken)
    {
        using var document = await _platformClient.GetPluginCategoriesAsync(cancellationToken).ConfigureAwait(false);
        var root = document.RootElement;
        if (!TryGet(root, "items", out var items) || items.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return items.EnumerateArray()
            .Select(item => new PluginMarketplaceCategory
            {
                Id = ReadString(item, "id"),
                Name = ReadString(item, "name"),
                Emoji = ReadString(item, "emoji"),
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.Id) && !string.IsNullOrWhiteSpace(item.Name))
            .ToArray();
    }

    /// <inheritdoc/>
    public async Task<InstalledPluginItem> InstallAsync(long platformPluginId, CancellationToken cancellationToken)
    {
        using var detailDocument = await _platformClient.GetPublishedPluginDetailAsync(platformPluginId.ToString(System.Globalization.CultureInfo.InvariantCulture), cancellationToken).ConfigureAwait(false);
        var detailRoot = detailDocument.RootElement;
        var pluginKey = ReadString(detailRoot, "key").Trim();
        var detailPluginId = ReadLong(detailRoot, "pluginId");
        var detailName = ReadString(detailRoot, "name");
        var detailSummary = ReadString(detailRoot, "summary");
        var detailDescription = ReadString(detailRoot, "description");
        var detailIconUrl = ReadString(detailRoot, "iconUrl");
        var detailVersionId = 0L;
        var detailVersion = string.Empty;
        if (TryGet(detailRoot, "versions", out var versions) && versions.ValueKind == JsonValueKind.Array)
        {
            var current = versions.EnumerateArray().FirstOrDefault();
            if (current.ValueKind == JsonValueKind.Object)
            {
                detailVersionId = ReadLong(current, "versionId");
                detailVersion = ReadString(current, "version");
            }
        }

        if (!PluginKeyRegex().IsMatch(pluginKey))
        {
            throw new BusinessException("MoPlatform 插件 key 格式不符合 MoAI 插件协议.") { StatusCode = 400 };
        }

        var actualPluginId = detailPluginId;
        var platformVersionId = detailVersionId;
        if (actualPluginId != platformPluginId || platformVersionId <= 0)
        {
            throw new BusinessException("MoPlatform 未返回有效的当前插件包版本.") { StatusCode = 502 };
        }

        if (await _databaseContext.PluginPackages.AnyAsync(x => x.PluginKey == pluginKey, cancellationToken).ConfigureAwait(false)
            || await _databaseContext.Plugins.AnyAsync(x => x.PluginName == pluginKey, cancellationToken).ConfigureAwait(false)
            || _registry.IsRegistered(pluginKey))
        {
            throw new BusinessException($"插件 {pluginKey} 已安装或 key 已被占用；MoAI 不支持在线覆盖或卸载插件包.") { StatusCode = 409 };
        }

        using var manifestDocument = await _platformClient.GetPluginManifestAsync(pluginKey, detailVersion, cancellationToken).ConfigureAwait(false);
        var manifestRoot = manifestDocument.RootElement;
        var manifestElement = TryGet(manifestRoot, "manifest", out var manifestValue) ? manifestValue : default;
        var entryElement = TryGet(manifestElement, "entryAssembly", out var entryValue) ? entryValue : default;
        var manifest = new MoPlatformPluginManifest
        {
            PluginId = ReadString(manifestRoot, "pluginId"),
            Key = ReadString(manifestRoot, "key"),
            VersionId = ReadString(manifestRoot, "versionId"),
            Version = ReadString(manifestRoot, "version"),
            FileSize = ReadLong(manifestRoot, "fileSize"),
            FileSha256 = ReadString(manifestRoot, "fileSha256"),
            SchemaVersion = (int)ReadLong(manifestElement, "schemaVersion"),
            ManifestKey = ReadString(manifestElement, "key"),
            MinClientVersion = ReadString(manifestElement, "minClientVersion"),
            TargetFramework = ReadString(manifestElement, "targetFramework"),
            RootPath = ReadString(manifestElement, "rootPath"),
            EntryAssemblyPath = ReadString(entryElement, "path"),
            ManifestJson = manifestElement.ValueKind == JsonValueKind.Undefined ? "{}" : manifestElement.GetRawText(),
        };
        ValidateManifest(manifest, pluginKey, detailVersion);
        ValidateHostDependencies(manifest);
        var download = await _platformClient.GetDownloadUrlAsync(
            actualPluginId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            platformVersionId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            cancellationToken).ConfigureAwait(false);
        if (!long.TryParse(download.VersionId, out var downloadVersionId)
            || downloadVersionId != platformVersionId
            || !long.TryParse(download.FileSize, out var downloadSize)
            || !string.Equals(download.FileSha256, manifest.FileSha256, StringComparison.OrdinalIgnoreCase)
            || downloadSize != manifest.FileSize)
        {
            throw new BusinessException("插件下载响应与已获取的兼容清单不一致.") { StatusCode = 502 };
        }

        var root = PluginMarketplacePaths.ResolvePluginDirectory(_configuration);
        Directory.CreateDirectory(root);
        var staging = Path.Combine(root, ".staging-" + Guid.CreateVersion7().ToString("N"));
        Directory.CreateDirectory(staging);
        var archivePath = Path.Combine(staging, "package.zip");
        var contentPath = Path.Combine(staging, "content");
        var finalPath = Path.Combine(root, pluginKey);
        var movedFinalDirectory = false;
        try
        {
            var (actualSize, actualSha256) = await DownloadPackageAsync(download.DownloadUrl, archivePath, downloadSize, cancellationToken).ConfigureAwait(false);
            if (actualSize != manifest.FileSize || !string.Equals(actualSha256, manifest.FileSha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new BusinessException("在线插件包 SHA-256 或文件大小校验失败.") { StatusCode = 400 };
            }

            var archive = await PluginPackageArchive.ExtractAsync(archivePath, contentPath, manifest, pluginKey, cancellationToken).ConfigureAwait(false);
            var icon = archive.Icon;
            if (icon == null && !string.IsNullOrWhiteSpace(detailIconUrl))
            {
                var bytes = await DownloadIconAsync(detailIconUrl, cancellationToken).ConfigureAwait(false);
                icon = PluginPackageArchive.DetectIcon(bytes);
            }

            var avatarPath = icon == null
                ? string.Empty
                : await UploadIconAsync(icon, cancellationToken).ConfigureAwait(false);
            if (Directory.Exists(finalPath))
            {
                throw new BusinessException($"插件目录 {pluginKey} 已存在但没有安装记录；请管理员检查 configs/plugins 后再处理.") { StatusCode = 409 };
            }

            var metadata = new InstalledPluginMetadata
            {
                PluginKey = pluginKey,
                Name = FirstNonEmpty(archive.Name, detailName, pluginKey),
                Summary = FirstNonEmpty(archive.Summary, detailSummary),
                Description = FirstNonEmpty(archive.Description, detailDescription),
                Category = archive.Category,
                Version = detailVersion,
                PlatformPluginId = actualPluginId,
                PlatformVersionId = platformVersionId,
                PackageSha256 = actualSha256,
                EntryAssembly = archive.EntryAssemblyPath,
                RootPath = archive.RootPath,
                AvatarPath = avatarPath,
                Enabled = true,
                InstalledAt = DateTimeOffset.UtcNow,
            };

            await File.WriteAllTextAsync(
                Path.Combine(contentPath, PluginMarketplacePaths.MetadataFileName),
                JsonSerializer.Serialize(metadata, JsonSerializerOptions.Web),
                cancellationToken).ConfigureAwait(false);
            Directory.Move(contentPath, finalPath);
            movedFinalDirectory = true;

            await using var transaction = await _databaseContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            var localClassifyId = await EnsurePluginClassifyAsync(archive.Category, cancellationToken).ConfigureAwait(false);

            var package = new PluginPackageEntity
            {
                Id = Guid.CreateVersion7(),
                PlatformPluginId = actualPluginId,
                PlatformVersionId = platformVersionId,
                PluginKey = pluginKey,
                Version = detailVersion,
                PackageHash = actualSha256,
                EntryAssembly = archive.EntryAssemblyPath,
                ManifestJson = manifest.ManifestJson,
                MetadataJson = JsonSerializer.Serialize(metadata, JsonSerializerOptions.Web),
                Name = metadata.Name,
                Summary = metadata.Summary,
                Description = metadata.Description,
                AvatarPath = avatarPath,
                Enabled = true,
            };
            var localPluginId = Guid.CreateVersion7();
            var pluginEntity = new PluginEntity
            {
                Id = localPluginId,
                IsSystem = true,
                TeamId = 0,
                PluginId = package.Id,
                PluginName = pluginKey,
                Title = Clip(metadata.Name, 50),
                Description = Clip(metadata.Summary, 255),
                Type = (int)PluginType.NativePlugin,
                ClassifyId = localClassifyId,
                AvatarPath = avatarPath,
                IsPublic = true,
                Counter = 0,
                UsageNotes = archive.Semantic?.UsageNotes ?? string.Empty,
            };
            _databaseContext.PluginPackages.Add(package);
            _databaseContext.Plugins.Add(pluginEntity);
            if (archive.Semantic is { } semantic)
            {
                foreach (var note in semantic.Notes)
                {
                    _databaseContext.PluginSemanticNotes.Add(new PluginSemanticNoteEntity
                    {
                        Id = Guid.CreateVersion7(),
                        PluginId = localPluginId,
                        ToolKey = note.ToolKey,
                        Title = note.Title,
                        Content = note.Content,
                        Mode = note.Mode.Equals("onDemand", StringComparison.OrdinalIgnoreCase) ? 1 : 0,
                        SortOrder = note.SortOrder,
                    });
                }

                foreach (var meta in semantic.ToolMetas)
                {
                    _databaseContext.PluginToolMetas.Add(new PluginToolMetaEntity
                    {
                        Id = Guid.CreateVersion7(),
                        PluginId = localPluginId,
                        ToolKey = meta.ToolKey,
                        ExtraNotes = meta.ExtraNotes,
                        CallPolicy = meta.CallPolicy,
                        ReadOnlyHint = meta.ReadOnlyHint,
                        DestructiveHint = meta.DestructiveHint,
                    });
                }
            }

            await _databaseContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                PluginPackageRuntimeLoader.Load(finalPath, package.PluginKey, package.PackageHash, package.EntryAssembly, _registry, enabled: true);
                _logger.LogInformation("在线插件包已在当前进程动态加载并注册。PluginKey={PluginKey}, Version={Version}", package.PluginKey, package.Version);
            }
            catch (Exception ex)
            {
                package.Enabled = false;
                pluginEntity.IsPublic = false;
                await WriteMetadataEnabledAsync(finalPath, metadata with { Enabled = false }, cancellationToken).ConfigureAwait(false);
                await _databaseContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                _logger.LogError(ex, "插件包已安装但无法在当前进程加载，已自动下架。PluginKey={PluginKey}", pluginKey);
            }

            return ToInstalledItem(package);
        }
        catch
        {
            if (movedFinalDirectory)
            {
                // 仅清理本次未完成安装的 staging 产物；正式安装成功后永不提供删除路径。
                if (!await _databaseContext.PluginPackages.AnyAsync(x => x.PluginKey == pluginKey, CancellationToken.None).ConfigureAwait(false))
                {
                    Directory.Delete(finalPath, recursive: true);
                }
            }

            throw;
        }
        finally
        {
            if (Directory.Exists(staging))
            {
                Directory.Delete(staging, recursive: true);
            }
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<InstalledPluginItem>> QueryInstalledAsync(CancellationToken cancellationToken)
    {
        var packages = await _databaseContext.PluginPackages
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        var items = packages.Select(ToInstalledItem).ToArray();
        await _userInfoFillService.FillAsync(items, cancellationToken).ConfigureAwait(false);
        return items;
    }

    /// <inheritdoc/>
    public async Task<InstalledPluginItem> SetEnabledAsync(string pluginKey, bool enabled, CancellationToken cancellationToken)
    {
        var package = await _databaseContext.PluginPackages
            .FirstOrDefaultAsync(x => x.PluginKey == pluginKey, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new BusinessException("在线插件尚未安装.") { StatusCode = 404 };
        var plugin = await _databaseContext.Plugins
            .FirstOrDefaultAsync(x => x.PluginId == package.Id && x.TeamId == 0, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new BusinessException("在线插件数据库记录不存在.") { StatusCode = 404 };

        if (enabled && !Directory.Exists(Path.Combine(PluginMarketplacePaths.ResolvePluginDirectory(_configuration), package.PluginKey)))
        {
            throw new BusinessException("在线插件包文件缺失，无法上架.") { StatusCode = 409 };
        }

        package.Enabled = enabled;
        plugin.IsPublic = enabled;
        package.MetadataJson = SetMetadataEnabled(package.MetadataJson, enabled);
        await _databaseContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var directory = Path.Combine(PluginMarketplacePaths.ResolvePluginDirectory(_configuration), package.PluginKey);
        await WriteMetadataEnabledAsync(directory, enabled, cancellationToken).ConfigureAwait(false);
        if (enabled && !_registry.IsRegistered(package.PluginKey))
        {
            PluginPackageRuntimeLoader.Load(directory, package.PluginKey, package.PackageHash, package.EntryAssembly, _registry, enabled: true);
        }
        else
        {
            _registry.SetEnabled(package.PluginKey, enabled);
        }

        return ToInstalledItem(package);
    }

    private async Task<string> UploadIconAsync(PluginArchiveIcon icon, CancellationToken cancellationToken)
    {
        var sha = Convert.ToHexString(SHA256.HashData(icon.Bytes)).ToLowerInvariant();
        var objectKey = $"public/images/{sha}.{icon.Extension}";
        using var stream = new MemoryStream(icon.Bytes, writable: false);
        var result = await _storageService.UploadStreamAsync(new UploadStreamFileCommand
        {
            FileStream = stream,
            FileSize = icon.Bytes.Length,
            SHA256 = sha,
            ContentType = icon.ContentType,
            ObjectKey = objectKey,
        }, cancellationToken).ConfigureAwait(false);
        return result.ObjectKey;
    }

    private async Task<int> EnsurePluginClassifyAsync(string categoryName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(categoryName))
        {
            return 0;
        }

        var normalizedName = categoryName.Trim();
        if (normalizedName.Length > 20)
        {
            throw new BusinessException("在线插件 category 超过 20 个字符.") { StatusCode = 400 };
        }

        var existing = await _databaseContext.Classifies
            .FirstOrDefaultAsync(x => x.Type == "plugin" && x.Name == normalizedName, cancellationToken)
            .ConfigureAwait(false);
        if (existing != null)
        {
            return existing.Id;
        }

        var classify = new ClassifyEntity
        {
            Type = "plugin",
            Name = normalizedName,
            Description = normalizedName,
            Emoji = string.Empty,
        };
        _databaseContext.Classifies.Add(classify);
        await _databaseContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return classify.Id;
    }

    private async Task<(long Size, string Sha256)> DownloadPackageAsync(Uri downloadUrl, string destination, long expectedSize, CancellationToken cancellationToken)
    {
        const long maxPackageSize = 1024L * 1024 * 1024;
        if ((downloadUrl.Scheme != Uri.UriSchemeHttps && downloadUrl.Scheme != Uri.UriSchemeHttp)
            || expectedSize <= 0 || expectedSize > maxPackageSize)
        {
            throw new BusinessException("MoPlatform 插件包下载地址或大小无效.") { StatusCode = 502 };
        }

        if (!IsTrustedPlatformResource(downloadUrl))
        {
            throw new BusinessException("MoPlatform 插件包下载域名未在 MoAI 信任配置中.") { StatusCode = 502 };
        }

        using var response = await _httpClientFactory.CreateClient(MoAI.Infra.MoPlatformPluginClient.DownloadHttpClientName)
            .GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new BusinessException($"下载在线插件包失败（HTTP {(int)response.StatusCode}）.") { StatusCode = (int)response.StatusCode };
        }

        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 128 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[128 * 1024];
        long size = 0;
        while (true)
        {
            var read = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0) break;
            size += read;
            if (size > expectedSize || size > maxPackageSize)
            {
                throw new BusinessException("在线插件包实际大小超出声明值.") { StatusCode = 400 };
            }

            hash.AppendData(buffer, 0, read);
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }

        await output.FlushAsync(cancellationToken).ConfigureAwait(false);
        return (size, Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant());
    }

    private async Task<byte[]?> DownloadIconAsync(string iconUrl, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(iconUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || !IsTrustedPlatformResource(uri))
        {
            return null;
        }

        using var response = await _httpClientFactory.CreateClient(MoAI.Infra.MoPlatformPluginClient.DownloadHttpClientName)
            .GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength is > 1_048_576)
        {
            return null;
        }

        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        return bytes.Length <= 1_048_576 ? bytes : null;
    }

    private bool IsTrustedPlatformResource(Uri uri)
    {
        var allowedAuthorities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in new[] { "MoAI:MoPlatform:Endpoint", "MoAI:Storage:Endpoint" })
        {
            if (Uri.TryCreate(_configuration[key], UriKind.Absolute, out var configuredUri))
            {
                allowedAuthorities.Add(configuredUri.Authority);
            }
        }

        var configuredAllowList = _configuration["MoAI:PluginMarketplace:AllowedDownloadHosts"];
        if (!string.IsNullOrWhiteSpace(configuredAllowList))
        {
            foreach (var value in configuredAllowList.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (Uri.TryCreate("https://" + value, UriKind.Absolute, out var allowedUri))
                {
                    allowedAuthorities.Add(allowedUri.Authority);
                }
            }
        }

        return allowedAuthorities.Contains(uri.Authority);
    }

    private async Task WriteMetadataEnabledAsync(string directory, bool enabled, CancellationToken cancellationToken)
    {
        var path = Path.Combine(directory, PluginMarketplacePaths.MetadataFileName);
        var metadata = JsonSerializer.Deserialize<InstalledPluginMetadata>(await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false), JsonSerializerOptions.Web)
            ?? throw new BusinessException("插件包本地元数据无效.") { StatusCode = 500 };
        await WriteMetadataEnabledAsync(directory, metadata with { Enabled = enabled }, cancellationToken).ConfigureAwait(false);
    }

    private static Task WriteMetadataEnabledAsync(string directory, InstalledPluginMetadata metadata, CancellationToken cancellationToken) =>
        File.WriteAllTextAsync(Path.Combine(directory, PluginMarketplacePaths.MetadataFileName), JsonSerializer.Serialize(metadata, JsonSerializerOptions.Web), cancellationToken);

    private static string SetMetadataEnabled(string metadataJson, bool enabled)
    {
        var metadata = JsonSerializer.Deserialize<InstalledPluginMetadata>(metadataJson, JsonSerializerOptions.Web)
            ?? throw new BusinessException("在线插件数据库元数据无效.") { StatusCode = 500 };
        return JsonSerializer.Serialize(metadata with { Enabled = enabled }, JsonSerializerOptions.Web);
    }

    private static void ValidateManifest(MoPlatformPluginManifest manifest, string pluginKey, string pluginVersion)
    {
        if (manifest.SchemaVersion != 1)
        {
            throw new BusinessException($"不支持 MoPlatform 插件清单 schemaVersion={manifest.SchemaVersion}.") { StatusCode = 400 };
        }

        if (!string.Equals(manifest.Key, pluginKey, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(manifest.ManifestKey, pluginKey, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(manifest.Version, pluginVersion, StringComparison.Ordinal))
        {
            throw new BusinessException("在线插件详情与兼容性清单不一致.") { StatusCode = 502 };
        }

        if (!string.Equals(manifest.TargetFramework, "net10.0", StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException($"插件目标框架 {manifest.TargetFramework} 与当前 MoAI net10.0 不兼容.") { StatusCode = 400 };
        }

        if (!Regex.IsMatch(manifest.FileSha256, "^[a-fA-F0-9]{64}$", RegexOptions.CultureInvariant)
            || manifest.FileSize <= 0
            || string.IsNullOrWhiteSpace(manifest.EntryAssemblyPath))
        {
            throw new BusinessException("MoPlatform 插件清单缺少有效的包摘要、大小或入口程序集.") { StatusCode = 502 };
        }

        var clientVersion = "1.0.0";
        var packageVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        if (packageVersion != null)
        {
            clientVersion = $"{packageVersion.Major}.{packageVersion.Minor}.{packageVersion.Build}";
        }

        var configuredVersion = Environment.GetEnvironmentVariable("MOAI_PLUGIN_CLIENT_VERSION");
        if (!string.IsNullOrWhiteSpace(configuredVersion)) clientVersion = configuredVersion;
        if (!string.IsNullOrWhiteSpace(manifest.MinClientVersion)
            && CompareSemanticVersion(clientVersion, manifest.MinClientVersion) < 0)
        {
            throw new BusinessException($"插件要求 MoAI 客户端版本至少为 {manifest.MinClientVersion}，当前为 {clientVersion}.") { StatusCode = 400 };
        }
    }

    private static void ValidateHostDependencies(MoPlatformPluginManifest manifest)
    {
        using var document = JsonDocument.Parse(manifest.ManifestJson);
        if (!TryGet(document.RootElement, "dependencies", out var dependencies) || dependencies.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var dependency in dependencies.EnumerateArray())
        {
            if (!string.Equals(ReadString(dependency, "source"), "host", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var name = ReadString(dependency, "name");
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new BusinessException("插件兼容清单包含空宿主依赖名称.") { StatusCode = 400 };
            }

            Assembly assembly;
            try
            {
                assembly = AssemblyLoadContext.Default.LoadFromAssemblyName(new AssemblyName(name));
            }
            catch (Exception ex) when (ex is FileNotFoundException or FileLoadException or BadImageFormatException)
            {
                throw new BusinessException($"当前 MoAI 未提供插件宿主依赖 {name}.", ex) { StatusCode = 400 };
            }

            var requiredText = ReadString(dependency, "version");
            if (Version.TryParse(requiredText, out var requiredVersion)
                && assembly.GetName().Version is { } actualVersion
                && actualVersion < requiredVersion)
            {
                throw new BusinessException($"插件要求宿主依赖 {name} {requiredVersion}，当前 MoAI 为 {actualVersion}.") { StatusCode = 400 };
            }

            var requiredToken = ReadString(dependency, "publicKeyToken");
            var actualToken = assembly.GetName().GetPublicKeyToken();
            if (!string.IsNullOrWhiteSpace(requiredToken)
                && !string.Equals(requiredToken, actualToken == null ? string.Empty : Convert.ToHexString(actualToken).ToLowerInvariant(), StringComparison.OrdinalIgnoreCase))
            {
                throw new BusinessException($"插件宿主依赖 {name} 的公钥标记与当前 MoAI 不匹配.") { StatusCode = 400 };
            }
        }
    }

    private static int CompareSemanticVersion(string current, string required)
    {
        static int[] Parse(string version)
        {
            var core = version.Split('-', '+')[0].Split('.');
            if (core.Length is < 2 or > 3 || core.Any(x => !int.TryParse(x, out _)))
            {
                throw new BusinessException($"插件最低客户端版本不是有效 SemVer：{version}.") { StatusCode = 400 };
            }

            return [int.Parse(core[0]), int.Parse(core[1]), core.Length > 2 ? int.Parse(core[2]) : 0];
        }

        var left = Parse(current);
        var right = Parse(required);
        for (var index = 0; index < 3; index++)
        {
            var compare = left[index].CompareTo(right[index]);
            if (compare != 0) return compare;
        }

        return 0;
    }

    private static string FirstNonEmpty(params string[] values) => values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))?.Trim() ?? string.Empty;
    private static string Clip(string value, int maxLength) => value.Length <= maxLength ? value : value[..maxLength];

    private static bool TryGet(JsonElement element, string propertyName, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }

    private static string ReadString(JsonElement element, string propertyName, string fallback = "")
    {
        if (!TryGet(element, propertyName, out var value)) return fallback;
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? fallback,
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => fallback,
        };
    }

    private static long ReadLong(JsonElement element, string propertyName)
    {
        var text = ReadString(element, propertyName);
        return long.TryParse(text, out var value) ? value : 0;
    }

    private static InstalledPluginItem ToInstalledItem(PluginPackageEntity package) => new()
    {
        PluginKey = package.PluginKey,
        Name = package.Name,
        Summary = package.Summary,
        Description = package.Description,
        AvatarPath = package.AvatarPath,
        Version = package.Version,
        PackageSha256 = package.PackageHash,
        Enabled = package.Enabled,
        CreateTime = package.CreateTime,
        CreateUserId = (int)package.CreateUserId,
        UpdateTime = package.UpdateTime,
        UpdateUserId = (int)package.UpdateUserId,
    };

    [GeneratedRegex("^[a-z][a-z0-9]*(?:[._-][a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex PluginKeyRegex();
}
