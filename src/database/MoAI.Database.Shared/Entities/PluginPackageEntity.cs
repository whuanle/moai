using MoAI.Database.Audits;

namespace MoAI.Database.Entities;

/// <summary>
/// 从 MoPlatform 安装的在线插件包及其本地快照元数据.
/// </summary>
public partial class PluginPackageEntity : IFullAudited
{
    /// <summary>主键.</summary>
    public Guid Id { get; set; }

    /// <summary>平台插件 id.</summary>
    public long PlatformPluginId { get; set; }

    /// <summary>平台当前包版本 id.</summary>
    public long PlatformVersionId { get; set; }

    /// <summary>不可变插件 key.</summary>
    public string PluginKey { get; set; } = string.Empty;

    /// <summary>包版本.</summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>下载包 SHA-256（小写十六进制）.</summary>
    public string PackageHash { get; set; } = string.Empty;

    /// <summary>相对于插件目录的入口程序集路径.</summary>
    public string EntryAssembly { get; set; } = string.Empty;

    /// <summary>与源 ZIP 同步保存的兼容清单 JSON.</summary>
    public string ManifestJson { get; set; } = "{}";

    /// <summary>安装时的本地展示信息快照 JSON.</summary>
    public string MetadataJson { get; set; } = "{}";

    /// <summary>平台名称.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>平台简短说明.</summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>平台详细描述.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>MoAI OSS 头像对象 key.</summary>
    public string AvatarPath { get; set; } = string.Empty;

    /// <summary>是否上架启用；false 时文件和程序集仍保留，不卸载.</summary>
    public bool Enabled { get; set; }

    /// <summary>创建人.</summary>
    public long CreateUserId { get; set; }

    /// <summary>创建时间.</summary>
    public DateTimeOffset CreateTime { get; set; }

    /// <summary>最后修改人.</summary>
    public long UpdateUserId { get; set; }

    /// <summary>更新时间.</summary>
    public DateTimeOffset UpdateTime { get; set; }

    /// <summary>软删除.</summary>
    public long IsDeleted { get; set; }
}
