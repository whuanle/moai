using System.Text.Json;
using MoAI.AIPlugin.Dynamic.HotBoard;

namespace MoAI.AIPlugin.Dynamic.HotBoard.Channels;

/// <summary>
/// 腾讯新闻热点榜渠道：GET r.inews.qq.com/gw/event/hot_ranking_list（每 10 分钟更新），
/// 条目在 idlist[0].newslist[]，首条为 TIP 横幅（id 以 TIP 开头）跳过；热度为 hotEvent.hotScore.
/// </summary>
public class QqNewsChannel : HotBoardChannelBase
{
    private const string RankUrl = "https://r.inews.qq.com/gw/event/hot_ranking_list?page_size=50";

    /// <summary>
    /// Initializes a new instance of the <see cref="QqNewsChannel"/> class.
    /// </summary>
    /// <param name="httpClient">HTTP 客户端.</param>
    /// <param name="gateway">调试网关地址（可空）.</param>
    public QqNewsChannel(HttpClient httpClient, string? gateway)
        : base(httpClient, gateway)
    {
    }

    /// <inheritdoc/>
    public override string Id => "qq-news";

    /// <inheritdoc/>
    public override async Task<IReadOnlyList<HotBoardEntry>> FetchAsync(CancellationToken cancellationToken)
    {
        using var document = await GetJsonAsync(RankUrl, null, cancellationToken);

        var items = new List<HotBoardEntry>();
        if (document.RootElement.ValueKind == JsonValueKind.Object
            && document.RootElement.TryGetProperty("idlist", out var idlist)
            && idlist.ValueKind == JsonValueKind.Array
            && idlist.GetArrayLength() > 0)
        {
            var first = idlist[0];
            if (first.ValueKind != JsonValueKind.Object
                || !first.TryGetProperty("newslist", out var newslist)
                || newslist.ValueKind != JsonValueKind.Array)
            {
                return items;
            }

            foreach (var element in newslist.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var id = GetScalar(element, "id");
                if (string.IsNullOrWhiteSpace(id) || id.StartsWith("TIP", StringComparison.Ordinal))
                {
                    continue;
                }

                var title = GetString(element, "title");
                if (string.IsNullOrWhiteSpace(title))
                {
                    continue;
                }

                var hotScore = GetNestedScalar(element, "hotEvent", "hotScore");
                items.Add(Build(
                    title: title,
                    desc: GetString(element, "abstract"),
                    url: $"https://new.qq.com/rain/a/{id}",
                    cover: GetString(element, "miniProShareImage"),
                    hotValue: hotScore is not null && long.TryParse(hotScore, out var score) && score > 0 ? score : null));
            }
        }

        return items;
    }
}
