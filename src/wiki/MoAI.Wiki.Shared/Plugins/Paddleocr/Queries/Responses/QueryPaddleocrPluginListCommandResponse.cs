using MoAI.Wiki.Plugins.Paddleocr.Models;

namespace MoAI.Wiki.Plugins.Paddleocr.Queries.Responses;

/// <summary>
/// 查询可用的飞桨 OCR 插件列表响应.
/// </summary>
public class QueryPaddleocrPluginListCommandResponse
{
    /// <summary>
    /// 插件列表.
    /// </summary>
    public IReadOnlyCollection<PaddleocrPluginItem> Items { get; init; } = Array.Empty<PaddleocrPluginItem>();
}
