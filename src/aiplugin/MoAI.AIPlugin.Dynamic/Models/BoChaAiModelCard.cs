using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// 博查 AI 搜索返回的模态卡（垂域结构化数据）.
/// </summary>
public class BoChaAiModelCard
{
    /// <summary>
    /// 模态卡类型.
    /// </summary>
    [Description("模态卡类型，如 weather_china_v2（国内天气）、baike_pro_v2（百科专业版）、stock_v2（股票）、douyin（抖音短视频）")]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// 模态卡标题.
    /// </summary>
    [Description("模态卡标题")]
    public string? Name { get; set; }

    /// <summary>
    /// 跳转链接.
    /// </summary>
    [Description("模态卡对应的跳转链接")]
    public string? Url { get; set; }

    /// <summary>
    /// 内容的简短描述.
    /// </summary>
    [Description("模态卡内容的简短描述")]
    public string? Snippet { get; set; }

    /// <summary>
    /// 结构化摘要.
    /// </summary>
    [Description("模态卡的结构化摘要，通常为该垂域的结构化文本数据（如逐日天气、金价走势等）")]
    public string? Summary { get; set; }

    /// <summary>
    /// 数据来源网站名称.
    /// </summary>
    [Description("数据来源网站名称")]
    public string? SiteName { get; set; }

    /// <summary>
    /// 数据来源网站图标.
    /// </summary>
    [Description("数据来源网站图标 URL")]
    public string? SiteIcon { get; set; }

    /// <summary>
    /// 数据发布时间（UTC+8）.
    /// </summary>
    [Description("数据发布时间（UTC+8）")]
    public string? DatePublished { get; set; }
}
