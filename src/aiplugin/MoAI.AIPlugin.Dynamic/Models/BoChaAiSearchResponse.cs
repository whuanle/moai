using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// 博查 AI 搜索响应结果.
/// </summary>
public class BoChaAiSearchResponse
{
    /// <summary>
    /// 原始的搜索关键字.
    /// </summary>
    [Description("原始的搜索关键字")]
    public string Query { get; set; } = string.Empty;

    /// <summary>
    /// 会话 ID.
    /// </summary>
    [Description("本次搜索的会话 ID，可用于串联同一会话的多次搜索")]
    public string? ConversationId { get; set; }

    /// <summary>
    /// 大模型生成的总结答案（Markdown）.
    /// </summary>
    [Description("大模型生成的总结答案（Markdown 文本）；请求 Answer=false 时为空")]
    public string? Answer { get; set; }

    /// <summary>
    /// 推荐追问问题.
    /// </summary>
    [Description("推荐追问问题列表；请求 Answer=false 时为空")]
    public IReadOnlyList<string> FollowUps { get; init; } = [];

    /// <summary>
    /// 参考网页.
    /// </summary>
    [Description("参考网页列表，含标题、链接、摘要、站点与发布时间")]
    public IReadOnlyList<BoChaWebPage> WebPages { get; init; } = [];

    /// <summary>
    /// 参考图片.
    /// </summary>
    [Description("参考图片列表")]
    public IReadOnlyList<BoChaWebImage> Images { get; init; } = [];

    /// <summary>
    /// 模态卡（垂域结构化数据）.
    /// </summary>
    [Description("模态卡列表，Type 标识模态卡类型（天气、百科、医疗、日历、火车、星座、贵金属、汇率、油价、手机、股票、汽车等）")]
    public IReadOnlyList<BoChaAiModelCard> ModelCards { get; init; } = [];

    /// <summary>
    /// 网页结果中是否有被安全过滤的内容.
    /// </summary>
    [Description("网页结果中是否有被安全过滤的内容")]
    public bool SomeResultsRemoved { get; set; }
}
