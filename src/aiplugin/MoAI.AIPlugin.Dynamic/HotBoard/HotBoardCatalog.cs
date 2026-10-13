using MoAI.AIPlugin.Dynamic.HotBoard.Channels;

namespace MoAI.AIPlugin.Dynamic.HotBoard;

/// <summary>
/// 热榜渠道目录：平台 id、中文名、常见别名与渠道构造器，插件按来源标识查表构造渠道.
/// </summary>
public static class HotBoardCatalog
{
    /// <summary>
    /// 别名表：中文名/常见叫法 → 渠道 id（匹配前统一小写化）.
    /// </summary>
    private static readonly Dictionary<string, string> Aliases = new(StringComparer.Ordinal)
    {
        ["36氪"] = "36kr",
        ["36kr"] = "36kr",
        ["知乎"] = "zhihu",
        ["zhihu"] = "zhihu",
        ["微博"] = "weibo",
        ["weibo"] = "weibo",
        ["掘金"] = "juejin",
        ["稀土掘金"] = "juejin",
        ["juejin"] = "juejin",
        ["头条"] = "toutiao",
        ["今日头条"] = "toutiao",
        ["toutiao"] = "toutiao",
        ["百度"] = "baidu",
        ["百度热搜"] = "baidu",
        ["baidu"] = "baidu",
        ["b站"] = "bilibili",
        ["哔哩哔哩"] = "bilibili",
        ["bilibili"] = "bilibili",
        ["抖音"] = "douyin",
        ["douyin"] = "douyin",
        ["it之家"] = "ithome",
        ["ithome"] = "ithome",
        ["虎扑"] = "hupu",
        ["hupu"] = "hupu",
        ["虎嗅"] = "huxiu",
        ["huxiu"] = "huxiu",
        ["澎湃"] = "thepaper",
        ["澎湃新闻"] = "thepaper",
        ["thepaper"] = "thepaper",
        ["极客公园"] = "geekpark",
        ["geekpark"] = "geekpark",
        ["腾讯新闻"] = "qq-news",
        ["qq-news"] = "qq-news",
        ["网易新闻"] = "netease-news",
        ["netease-news"] = "netease-news",
        ["新浪新闻"] = "sina-news",
        ["sina-news"] = "sina-news",
    };

    /// <summary>
    /// 渠道构造器表：渠道 id → 构造委托.
    /// </summary>
    private static readonly Dictionary<string, Func<HttpClient, string?, IHotBoardChannel>> Factories = new(StringComparer.Ordinal)
    {
        ["36kr"] = (client, gateway) => new Kr36Channel(client, gateway),
        ["zhihu"] = (client, gateway) => new ZhihuChannel(client, gateway),
        ["weibo"] = (client, gateway) => new WeiboChannel(client, gateway),
        ["juejin"] = (client, gateway) => new JuejinChannel(client, gateway),
        ["toutiao"] = (client, gateway) => new ToutiaoChannel(client, gateway),
        ["baidu"] = (client, gateway) => new BaiduChannel(client, gateway),
        ["bilibili"] = (client, gateway) => new BilibiliChannel(client, gateway),
        ["douyin"] = (client, gateway) => new DouyinChannel(client, gateway),
        ["ithome"] = (client, gateway) => new IthomeChannel(client, gateway),
        ["hupu"] = (client, gateway) => new HupuChannel(client, gateway),
        ["huxiu"] = (client, gateway) => new HuxiuChannel(client, gateway),
        ["thepaper"] = (client, gateway) => new ThepaperChannel(client, gateway),
        ["geekpark"] = (client, gateway) => new GeekparkChannel(client, gateway),
        ["qq-news"] = (client, gateway) => new QqNewsChannel(client, gateway),
        ["netease-news"] = (client, gateway) => new NeteaseNewsChannel(client, gateway),
        ["sina-news"] = (client, gateway) => new SinaNewsChannel(client, gateway),
    };

    /// <summary>
    /// 全部渠道 id（展示顺序）.
    /// </summary>
    public static IReadOnlyList<string> Ids { get; } = Factories.Keys.Order(StringComparer.Ordinal).ToList();

    /// <summary>
    /// 拼接可选值提示（错误消息用）.
    /// </summary>
    /// <returns>返回形如 36kr、zhihu、weibo… 的提示文本.</returns>
    public static string DescribeIds()
    {
        return string.Join('、', Ids);
    }

    /// <summary>
    /// 渠道中文名.
    /// </summary>
    /// <param name="id">渠道 id.</param>
    /// <returns>返回中文名，未知 id 原样返回.</returns>
    public static string DisplayNameOf(string id)
    {
        return id switch
        {
            "36kr" => "36氪",
            "zhihu" => "知乎",
            "weibo" => "微博",
            "juejin" => "稀土掘金",
            "toutiao" => "今日头条",
            "baidu" => "百度热搜",
            "bilibili" => "哔哩哔哩",
            "douyin" => "抖音",
            "ithome" => "IT之家",
            "hupu" => "虎扑",
            "huxiu" => "虎嗅",
            "thepaper" => "澎湃新闻",
            "geekpark" => "极客公园",
            "qq-news" => "腾讯新闻",
            "netease-news" => "网易新闻",
            "sina-news" => "新浪新闻",
            _ => id,
        };
    }

    /// <summary>
    /// 归一化来源输入：去空白、小写化、别名映射、按出现顺序去重.
    /// </summary>
    /// <param name="inputs">用户输入的来源集合.</param>
    /// <param name="ids">归一化后的渠道 id（按出现顺序去重）.</param>
    /// <param name="unknown">无法识别的输入原样保留.</param>
    /// <returns>返回是否至少解析出一个有效渠道.</returns>
    public static bool TryNormalize(IEnumerable<string?>? inputs, out List<string> ids, out List<string> unknown)
    {
        ids = [];
        unknown = [];
        if (inputs is null)
        {
            return false;
        }

        foreach (var input in inputs)
        {
            var key = input?.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(key))
            {
                continue;
            }

            if (!Aliases.TryGetValue(key, out var id))
            {
                unknown.Add(key);
                continue;
            }

            if (!ids.Contains(id))
            {
                ids.Add(id);
            }
        }

        return ids.Count > 0;
    }

    /// <summary>
    /// 构造渠道实例.
    /// </summary>
    /// <param name="id">渠道 id.</param>
    /// <param name="httpClient">HTTP 客户端.</param>
    /// <param name="gateway">调试网关（可空）.</param>
    /// <returns>返回渠道实例，未知 id 抛异常.</returns>
    public static IHotBoardChannel Create(string id, HttpClient httpClient, string? gateway)
    {
        if (!Factories.TryGetValue(id, out var factory))
        {
            throw new ArgumentException($"未知热榜渠道：{id}", nameof(id));
        }

        return factory(httpClient, gateway);
    }
}
