using MoAI.Infra.Models;
using MoAI.Wiki.Models;

namespace MoAI.Wiki.Plugins.Feishu.Models;

/// <summary>
/// 配置.
/// </summary>
public class QueryWikiFeishuConfigCommandResponse : AuditsInfo
{
    /// <summary>
    /// 知识库id.
    /// </summary>
    public int WikiId { get; init; }

    /// <summary>
    /// id.
    /// </summary>
    public int ConfigId { get; init; }

    /// <summary>
    /// 插件标题.
    /// </summary>
    public string Title { get; set; } = default!;

    /// <summary>
    /// 运行信息.
    /// </summary>
    public string WorkMessage { get; set; } = default!;

    /// <summary>
    /// 状态.
    /// </summary>
    public WorkerState WorkState { get; set; }

    /// <summary>
    /// 配置.
    /// </summary>
    public WikiFeishuConfig Config { get; init; } = default!;
}