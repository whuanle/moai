using System.Text.Json;
using MoAI.AIPlugin.Dynamic.HotBoard;

namespace MoAI.AIPlugin.Dynamic.HotBoard.Channels;

/// <summary>
/// 36氪热榜渠道：POST gateway.36kr.com/api/mis/nav/home/nav/rank/hot（人气榜），
/// 条目在 data.hotRankList[]，标题/封面/热度在 templateMaterial 内.
/// </summary>
public class Kr36Channel : HotBoardChannelBase
{
    private const string RankUrl = "https://gateway.36kr.com/api/mis/nav/home/nav/rank/hot";

    /// <summary>
    /// Initializes a new instance of the <see cref="Kr36Channel"/> class.
    /// </summary>
    /// <param name="httpClient">HTTP 客户端.</param>
    /// <param name="gateway">调试网关地址（可空）.</param>
    public Kr36Channel(HttpClient httpClient, string? gateway)
        : base(httpClient, gateway)
    {
    }

    /// <inheritdoc/>
    public override string Id => "36kr";

    /// <inheritdoc/>
    public override async Task<IReadOnlyList<HotBoardEntry>> FetchAsync(CancellationToken cancellationToken)
    {
        var body = $$"""
            {"partner_id":"wap","param":{"siteId":1,"platformId":2},"timestamp":{{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}}}
            """;
        using var document = await PostJsonAsync(RankUrl, body, null, cancellationToken);

        var items = new List<HotBoardEntry>();
        if (document.RootElement.ValueKind == JsonValueKind.Object
            && document.RootElement.TryGetProperty("data", out var data)
            && data.ValueKind == JsonValueKind.Object
            && data.TryGetProperty("hotRankList", out var rankList)
            && rankList.ValueKind == JsonValueKind.Array)
        {
            foreach (var element in rankList.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.Object
                    || !element.TryGetProperty("templateMaterial", out var material)
                    || material.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var title = GetString(material, "widgetTitle");
                // itemId 实测为数字 token，GetScalar 取原始文本避免长整型精度丢失
                var itemId = GetScalar(element, "itemId");
                if (string.IsNullOrWhiteSpace(title))
                {
                    continue;
                }

                var collect = GetScalar(material, "statCollect");
                items.Add(Build(
                    title: title,
                    desc: null,
                    url: itemId is null ? string.Empty : $"https://www.36kr.com/p/{itemId}",
                    cover: GetString(material, "widgetImage"),
                    hotValue: collect is not null && long.TryParse(collect, out var collectCount) && collectCount > 0 ? collectCount : null));
            }
        }

        return items;
    }
}
