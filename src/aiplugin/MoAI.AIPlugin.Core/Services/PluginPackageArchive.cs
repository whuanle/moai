using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;
using MoAI.AIPlugin.Models;
using MoAI.Infra.Exceptions;

namespace MoAI.AIPlugin.Services;

/// <summary>在线插件 ZIP 的安全解压与包内展示元数据读取.</summary>
public static partial class PluginPackageArchive
{
    private const int MaxEntries = 10_000;
    private const long MaxExpandedBytes = 1024L * 1024 * 1024;
    private const int MaxSemanticBytes = 1024 * 1024;
    private static readonly JsonSerializerOptions SemanticJsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>校验 ZIP 并安全解压；不执行或反射加载包内容.</summary>
    public static async Task<PluginArchiveResult> ExtractAsync(
        string archivePath,
        string stagingDirectory,
        MoPlatformPluginManifest manifest,
        string expectedKey,
        CancellationToken cancellationToken)
    {
        var rootPath = NormalizeRootPath(manifest.RootPath);
        Directory.CreateDirectory(stagingDirectory);
        using var archive = ZipFile.OpenRead(archivePath);
        if (archive.Entries.Count is 0 or > MaxEntries)
        {
            throw new BusinessException("在线插件包 ZIP 条目数为空或超过 10,000 条.") { StatusCode = 400 };
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long declaredExpandedBytes = 0;
        long copiedBytes = 0;
        foreach (var entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000)
            {
                throw new BusinessException("在线插件包包含符号链接，已拒绝安装.") { StatusCode = 400 };
            }

            var isDirectory = entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\');
            var entryPath = NormalizeEntryPath(isDirectory ? entry.FullName.TrimEnd('/', '\\') : entry.FullName);
            if (isDirectory && string.Equals(entryPath, rootPath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!string.IsNullOrEmpty(rootPath))
            {
                if (!entryPath.StartsWith(rootPath + "/", StringComparison.OrdinalIgnoreCase))
                {
                    throw new BusinessException("在线插件包文件不符合清单中的 rootPath.") { StatusCode = 400 };
                }

                entryPath = entryPath[(rootPath.Length + 1)..];
            }

            if (isDirectory)
            {
                continue;
            }

            if (!seen.Add(entryPath))
            {
                throw new BusinessException("在线插件包包含重复路径.") { StatusCode = 400 };
            }

            if (entry.Length < 0 || entry.Length > MaxExpandedBytes - declaredExpandedBytes)
            {
                throw new BusinessException("在线插件包解压后超过 1 GiB 上限.") { StatusCode = 400 };
            }

            declaredExpandedBytes += entry.Length;

            var destination = Path.GetFullPath(Path.Combine(stagingDirectory, entryPath.Replace('/', Path.DirectorySeparatorChar)));
            var rootPrefix = Path.GetFullPath(stagingDirectory) + Path.DirectorySeparatorChar;
            if (!destination.StartsWith(rootPrefix, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            {
                throw new BusinessException("在线插件包包含越界文件路径.") { StatusCode = 400 };
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            await using var input = entry.Open();
            await using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
            var buffer = new byte[64 * 1024];
            long entryCopied = 0;
            while (true)
            {
                var read = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                entryCopied += read;
                copiedBytes += read;
                if (entryCopied > entry.Length || copiedBytes > MaxExpandedBytes)
                {
                    throw new BusinessException("在线插件包实际解压内容超出 ZIP 声明或大小上限.") { StatusCode = 400 };
                }

                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            }

            if (entryCopied != entry.Length)
            {
                throw new BusinessException("在线插件包文件长度与 ZIP 声明不一致.") { StatusCode = 400 };
            }
        }

        var descriptorPath = Path.Combine(stagingDirectory, "plugin.json");
        var descriptor = ReadDescriptor(descriptorPath, expectedKey, manifest.Version);
        if (descriptor.Category.Length > 20)
        {
            throw new BusinessException("在线插件 plugin.json category 超过 20 个字符.") { StatusCode = 400 };
        }
        var entryRelativePath = NormalizeEntryPath(manifest.EntryAssemblyPath);
        var entryAssembly = Path.GetFullPath(Path.Combine(stagingDirectory, entryRelativePath.Replace('/', Path.DirectorySeparatorChar)));
        var stagePrefix = Path.GetFullPath(stagingDirectory) + Path.DirectorySeparatorChar;
        if (!entryAssembly.StartsWith(stagePrefix, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)
            || !File.Exists(entryAssembly)
            || !string.Equals(Path.GetExtension(entryAssembly), ".dll", StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException("在线插件清单入口程序集不存在或路径无效.") { StatusCode = 400 };
        }

        var icon = await ReadIconAsync(stagingDirectory, cancellationToken).ConfigureAwait(false);
        var semantic = await ReadSemanticAsync(stagingDirectory, cancellationToken).ConfigureAwait(false);
        return new PluginArchiveResult
        {
            Name = string.IsNullOrWhiteSpace(descriptor.Name) ? expectedKey : descriptor.Name,
            Summary = descriptor.Summary,
            Description = descriptor.Description,
            Category = descriptor.Category,
            EntryAssemblyPath = entryRelativePath,
            RootPath = rootPath,
            Icon = icon,
            Semantic = semantic,
        };
    }

    private static async Task<PluginSemanticBundle?> ReadSemanticAsync(string root, CancellationToken cancellationToken)
    {
        var path = Path.Combine(root, "semantic.json");
        if (!File.Exists(path))
        {
            return null;
        }

        var length = new FileInfo(path).Length;
        if (length is <= 0 or > MaxSemanticBytes)
        {
            throw new BusinessException("在线插件语义文件为空或超过 1 MiB.") { StatusCode = 400 };
        }

        PluginSemanticBundle semantic;
        try
        {
            var json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
            semantic = JsonSerializer.Deserialize<PluginSemanticBundle>(json, SemanticJsonOptions)
                ?? throw new JsonException("semantic.json 内容为空.");
        }
        catch (JsonException exception)
        {
            throw new BusinessException($"在线插件 semantic.json 无效：{exception.Message}") { StatusCode = 400 };
        }

        semantic.Validate();
        return semantic;
    }

    private static Descriptor ReadDescriptor(string path, string expectedKey, string expectedVersion)
    {
        if (!File.Exists(path))
        {
            return new Descriptor();
        }

        if (new FileInfo(path).Length > 4 * 1024 * 1024)
        {
            throw new BusinessException("插件包 plugin.json 超过 4 MiB 上限.") { StatusCode = 400 };
        }

        using var document = JsonDocument.Parse(File.ReadAllBytes(path), new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip, MaxDepth = 32 });
        var root = document.RootElement;
        var key = ReadProperty(root, "key");
        var version = ReadProperty(root, "version");
        if (!string.IsNullOrWhiteSpace(key) && !string.Equals(key, expectedKey, StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException("插件包 plugin.json 的 key 与 MoPlatform 清单不一致.") { StatusCode = 400 };
        }

        if (!string.IsNullOrWhiteSpace(version) && !string.Equals(version, expectedVersion, StringComparison.Ordinal))
        {
            throw new BusinessException("插件包 plugin.json 的 version 与 MoPlatform 清单不一致.") { StatusCode = 400 };
        }

        return new Descriptor
        {
            Name = ReadProperty(root, "name"),
            Summary = ReadProperty(root, "summary"),
            Description = ReadProperty(root, "description"),
            Category = ReadProperty(root, "category"),
        };
    }

    private static async Task<PluginArchiveIcon?> ReadIconAsync(string root, CancellationToken cancellationToken)
    {
        foreach (var extension in new[] { ".png", ".jpg", ".jpeg", ".gif", ".webp" })
        {
            var path = Path.Combine(root, "icon" + extension);
            if (!File.Exists(path))
            {
                continue;
            }

            var info = new FileInfo(path);
            if (info.Length <= 0 || info.Length > 1_048_576)
            {
                continue;
            }

            var bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
            var icon = DetectIcon(bytes);
            if (icon != null)
            {
                return icon;
            }
        }

        return null;
    }

    /// <summary>按文件头验证图片类型，仅接受 PNG/JPEG/GIF/WebP.</summary>
    public static PluginArchiveIcon? DetectIcon(byte[]? bytes)
    {
        if (bytes == null || bytes.Length == 0 || bytes.Length > 1_048_576) return null;
        if (bytes.Length >= 8
            && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47
            && bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A)
        {
            return new PluginArchiveIcon(bytes, "png", "image/png");
        }
        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF) return new PluginArchiveIcon(bytes, "jpg", "image/jpeg");
        if (bytes.Length >= 6 && (System.Text.Encoding.ASCII.GetString(bytes, 0, 6) is "GIF87a" or "GIF89a")) return new PluginArchiveIcon(bytes, "gif", "image/gif");
        if (bytes.Length >= 12 && System.Text.Encoding.ASCII.GetString(bytes, 0, 4) == "RIFF" && System.Text.Encoding.ASCII.GetString(bytes, 8, 4) == "WEBP") return new PluginArchiveIcon(bytes, "webp", "image/webp");
        return null;
    }

    private static string NormalizeRootPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return string.Empty;
        var normalized = NormalizeEntryPath(path.TrimEnd('/', '\\'));
        return normalized;
    }

    private static string NormalizeEntryPath(string path)
    {
        var normalized = path.Replace('\\', '/');
        if (string.IsNullOrWhiteSpace(normalized)
            || normalized.StartsWith("/", StringComparison.Ordinal)
            || normalized.Contains(':')
            || normalized.Length > 512
            || normalized.Any(char.IsControl))
        {
            throw new BusinessException("在线插件包包含非法文件路径.") { StatusCode = 400 };
        }

        var segments = normalized.Split('/');
        foreach (var segment in segments)
        {
            if (segment is "" or "." or ".." || segment.EndsWith('.') || segment.EndsWith(' ')
                || segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
                || segment.IndexOfAny(['<', '>', ':', '"', '|', '?', '*']) >= 0
                || ReservedNameRegex().IsMatch(segment))
            {
                throw new BusinessException("在线插件包包含不安全的文件路径.") { StatusCode = 400 };
            }
        }

        return string.Join('/', segments);
    }

    private static string ReadProperty(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object) return string.Empty;
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase)
                && property.Value.ValueKind == JsonValueKind.String)
            {
                return property.Value.GetString()?.Trim() ?? string.Empty;
            }
        }

        return string.Empty;
    }

    [GeneratedRegex("^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(\\..*)?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ReservedNameRegex();

    private sealed class Descriptor
    {
        /// <summary>插件名称.</summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>plugin.json summary：人读用途简介.</summary>
        public string Summary { get; init; } = string.Empty;

        /// <summary>plugin.json description：AI 使用提示.</summary>
        public string Description { get; init; } = string.Empty;

        /// <summary>分类名称.</summary>
        public string Category { get; init; } = string.Empty;
    }
}

/// <summary>验证后的插件包内展示资源.</summary>
public sealed class PluginArchiveResult
{
    /// <summary>插件名称.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>人读用途简介.</summary>
    public string Summary { get; init; } = string.Empty;

    /// <summary>AI 使用提示.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>分类名称.</summary>
    public string Category { get; init; } = string.Empty;

    /// <summary>入口程序集的相对路径.</summary>
    public string EntryAssemblyPath { get; init; } = string.Empty;

    /// <summary>插件包内的根目录.</summary>
    public string RootPath { get; init; } = string.Empty;

    /// <summary>插件图标；不存在时为空.</summary>
    public PluginArchiveIcon? Icon { get; init; }

    /// <summary>插件语义配置；不存在时为空.</summary>
    public PluginSemanticBundle? Semantic { get; init; }
}

/// <summary>semantic.json v1：插件级使用须知、语义条目和工具元数据.</summary>
public sealed class PluginSemanticBundle
{
    public int FormatVersion { get; init; }
    public string UsageNotes { get; init; } = string.Empty;
    public List<PluginSemanticBundleNote> Notes { get; init; } = [];
    public List<PluginSemanticBundleToolMeta> ToolMetas { get; init; } = [];

    public void Validate()
    {
        if (FormatVersion != 1)
        {
            throw new BusinessException($"不支持 semantic.json formatVersion={FormatVersion}.") { StatusCode = 400 };
        }

        if (UsageNotes is null || Notes is null || ToolMetas is null
            || UsageNotes.Length > 2000 || Notes.Count > 50 || ToolMetas.Count > 100)
        {
            throw new BusinessException("semantic.json 使用须知、语义条目或工具元数据超过数量上限.") { StatusCode = 400 };
        }

        foreach (var note in Notes)
        {
            if (note is null)
            {
                throw new BusinessException("semantic.json notes 项不能为空.") { StatusCode = 400 };
            }

            var mode = note.Mode ?? string.Empty;
            var title = note.Title ?? string.Empty;
            var toolKey = note.ToolKey ?? string.Empty;
            var content = note.Content ?? string.Empty;
            var always = mode.Equals("always", StringComparison.OrdinalIgnoreCase);
            var onDemand = mode.Equals("onDemand", StringComparison.OrdinalIgnoreCase);
            if (title.Length is 0 or > 100
                || toolKey.Length > 200
                || note.SortOrder < 0
                || (!always && !onDemand)
                || content.Length > (always ? 4000 : 12000))
            {
                throw new BusinessException("semantic.json notes 字段无效或超出长度限制.") { StatusCode = 400 };
            }
        }

        if (ToolMetas.Any(x => x is null))
        {
            throw new BusinessException("semantic.json toolMetas 项不能为空.") { StatusCode = 400 };
        }

        if (ToolMetas.Select(x => x.ToolKey ?? string.Empty).Distinct(StringComparer.Ordinal).Count() != ToolMetas.Count)
        {
            throw new BusinessException("semantic.json toolMetas.toolKey 不可重复.") { StatusCode = 400 };
        }

        foreach (var meta in ToolMetas)
        {
            if (meta is null)
            {
                throw new BusinessException("semantic.json toolMetas 项不能为空.") { StatusCode = 400 };
            }

            var toolKey = meta.ToolKey ?? string.Empty;
            var extraNotes = meta.ExtraNotes ?? string.Empty;
            var callPolicy = meta.CallPolicy ?? string.Empty;
            if (toolKey.Length is 0 or > 200 || extraNotes.Length > 2000)
            {
                throw new BusinessException("semantic.json toolMetas 字段无效或超出长度限制.") { StatusCode = 400 };
            }

            if (!PluginToolCallPolicy.TryValidate(callPolicy, out var error))
            {
                throw new BusinessException(error ?? "semantic.json toolMetas.callPolicy 无效.") { StatusCode = 400 };
            }
        }
    }
}

public sealed class PluginSemanticBundleNote
{
    public string ToolKey { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public string Mode { get; init; } = "always";
    public int SortOrder { get; init; }
}

public sealed class PluginSemanticBundleToolMeta
{
    public string ToolKey { get; init; } = string.Empty;
    public string ExtraNotes { get; init; } = string.Empty;
    public string CallPolicy { get; init; } = string.Empty;
    public bool ReadOnlyHint { get; init; }
    public bool DestructiveHint { get; init; }
}

/// <summary>验证通过的插件图标内容.</summary>
public sealed record PluginArchiveIcon(byte[] Bytes, string Extension, string ContentType);

/// <summary>MoPlatform 兼容性清单中用于安装前校验的字段.</summary>
public sealed class MoPlatformPluginManifest
{
    public string PluginId { get; init; } = string.Empty;
    public string Key { get; init; } = string.Empty;
    public string VersionId { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public long FileSize { get; init; }
    public string FileSha256 { get; init; } = string.Empty;
    public int SchemaVersion { get; init; }
    public string ManifestKey { get; init; } = string.Empty;
    public string MinClientVersion { get; init; } = string.Empty;
    public string TargetFramework { get; init; } = string.Empty;
    public string RootPath { get; init; } = string.Empty;
    public string EntryAssemblyPath { get; init; } = string.Empty;
    public string ManifestJson { get; init; } = "{}";
}
