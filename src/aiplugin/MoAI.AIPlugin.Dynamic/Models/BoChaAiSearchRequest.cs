using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// 博查 AI 搜索请求参数.
/// </summary>
public class BoChaAiSearchRequest
{
    /// <summary>
    /// 用户的搜索内容.
    /// </summary>
    [Description("用户的搜索内容，可以是关键词或自然语言问题，例如「西瓜的功效与作用」")]
    public string Query { get; set; } = string.Empty;

    /// <summary>
    /// 搜索指定时间范围内的网页.
    /// </summary>
    [Description("搜索时间范围：noLimit（默认）、oneDay、oneWeek、oneMonth、oneYear")]
    public string Freshness { get; set; } = "noLimit";

    /// <summary>
    /// 指定搜索的 site 范围.
    /// </summary>
    [Description("仅在这些网站内搜索，多个域名用 | 或 , 分隔（最多 100 个），例如 qq.com|m.163.com；也可直接在 Query 中写 site:qq.com 关键词；不填表示不限制")]
    public string? Include { get; set; }

    /// <summary>
    /// 返回结果的条数.
    /// </summary>
    [Description("每次搜索返回的参考网页数量，可填 1-50，默认 10")]
    public int Count { get; set; } = 10;

    /// <summary>
    /// 是否使用大模型生成总结答案与追问问题.
    /// </summary>
    [Description("是否调用大模型返回总结答案与追问问题，默认 true；设为 false 则只返回参考网页、图片与模态卡，耗时更短")]
    public bool Answer { get; set; } = true;
}
