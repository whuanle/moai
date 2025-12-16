using MoAI.Infra.Models;

namespace MoAI.Wiki.Plugins.Template.Models;

/// <summary>
/// Plugin 配置.
/// </summary>
public class WikiPluginConfigSimpleItem : AuditsInfo
{
    /// <summary>
    /// 知识库 id.
    /// </summary>
    public int WikiId { get; init; }

    /// <summary>
    /// 配置 id.
    /// </summary>
    public int ConfigId { get; init; }

    /// <summary>
    /// 爬虫标题.
    /// </summary>
    public string Title { get; init; } = default!;
}
