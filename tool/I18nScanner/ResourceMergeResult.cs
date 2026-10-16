using System.Collections.ObjectModel;

namespace I18nScanner;

/// <summary>
/// 资源文件合并结果.
/// </summary>
public sealed class ResourceMergeResult
{
    /// <summary>
    /// 本次新增的 key（值以 key 自身兜底，待翻译）.
    /// </summary>
    public Collection<string> Added { get; } = new();

    /// <summary>
    /// 代码中已不存在的 key（已被剪除，翻译可在 git 历史找回）.
    /// </summary>
    public Collection<string> Removed { get; } = new();

    /// <summary>
    /// 沿用已有翻译的 key 数量.
    /// </summary>
    public int Kept { get; set; }

    /// <summary>
    /// 已有文件里值不是字符串而被丢弃的条目.
    /// </summary>
    public Collection<string> InvalidEntries { get; } = new();
}
