using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// 新闻热榜请求参数.
/// </summary>
public class HotBoardRequest
{
    /// <summary>
    /// 热榜来源平台列表（可多选）.
    /// </summary>
    [Description("热榜来源平台列表（可多选）：36kr、zhihu、weibo、juejin、toutiao、baidu、bilibili、douyin、ithome、hupu、huxiu、thepaper、geekpark、qq-news、netease-news、sina-news；也接受中文名（如 知乎、微博、头条）")]
    public List<string> Sources { get; set; } = [];
}
