using System.Text.Json;
using MoAI.AIPlugin.Dynamic.HotBoard;

namespace MoAI.AIPlugin.Dynamic.HotBoard.Channels;

/// <summary>
/// 澎湃新闻热榜渠道：GET cache.thepaper.cn/contentapi/wwwIndex/rightSidebar（首页右侧热榜），
/// 条目在 data.hotNews[]，热度为点赞数（praiseTimes，字符串数字）.
/// </summary>
public class ThepaperChannel : HotBoardChannelBase
{
    private const string RankUrl = "https://cache.thepaper.cn/contentapi/wwwIndex/rightSidebar";

    /// <summary>
    /// Initializes a new instance of the <see cref="ThepaperChannel"/> class.
    /// </summary>
    /// <param name="httpClient">HTTP 客户端.</param>
    /// <param name="gateway">调试网关地址（可空）.</param>
    public ThepaperChannel(HttpClient httpClient, string? gateway)
        : base(httpClient, gateway)
    {
    }

    /// <inheritdoc/>
    public override string Id => "thepaper";

    /// <inheritdoc/>
    public override async Task<IReadOnlyList<HotBoardEntry>> FetchAsync(CancellationToken cancellationToken)
    {
        using var document = await GetJsonAsync(RankUrl, null, cancellationToken);

        var items = new List<HotBoardEntry>();
        if (document.RootElement.ValueKind == JsonValueKind.Object
            && document.RootElement.TryGetProperty("data", out var data)
            && data.ValueKind == JsonValueKind.Object
            && data.TryGetProperty("hotNews", out var hotNews)
            && hotNews.ValueKind == JsonValueKind.Array)
        {
            foreach (var element in hotNews.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var title = GetString(element, "name");
                if (string.IsNullOrWhiteSpace(title))
                {
                    continue;
                }

                var contId = GetScalar(element, "contId");
                var praise = GetScalar(element, "praiseTimes");
                items.Add(Build(
                    title: title,
                    desc: null,
                    url: contId is null ? string.Empty : $"https://www.thepaper.cn/newsDetail_forward_{contId}",
                    cover: GetString(element, "pic"),
                    hotValue: praise is not null && long.TryParse(praise, out var praiseCount) && praiseCount > 0 ? praiseCount : null));
            }
        }

        return items;
    }
}
