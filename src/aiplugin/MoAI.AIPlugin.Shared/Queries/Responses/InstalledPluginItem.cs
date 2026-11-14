using MoAI.Infra.Models;

namespace MoAI.AIPlugin.Queries.Responses;

/// <summary>MoAI 本地安装的在线插件.</summary>
public sealed class InstalledPluginItem : AuditsInfo
{
    /// <summary>插件 key.</summary>
    public string PluginKey { get; init; } = string.Empty;

    /// <summary>名称.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>人读用途简介.</summary>
    public string Summary { get; init; } = string.Empty;

    /// <summary>AI 使用提示.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>本地 OSS 图标对象 key.</summary>
    public string AvatarPath { get; init; } = string.Empty;

    /// <summary>当前安装版本.</summary>
    public string Version { get; init; } = string.Empty;

    /// <summary>SHA-256.</summary>
    public string PackageSha256 { get; init; } = string.Empty;

    /// <summary>是否已上架.</summary>
    public bool Enabled { get; init; }
}
