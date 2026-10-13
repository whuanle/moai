using System.Text.Json;
using MoAI.AIPlugin.Dynamic.HotBoard;

namespace MoAI.AIPlugin.Dynamic.HotBoard.Channels;

/// <summary>
/// 网易新闻热点榜渠道：GET m.163.com/fe/api/hot/news/flow，条目在 data.list[]；
/// 上游不提供热度值，desc 放媒体名（source），url 优先取条目自带地址、缺失时按 docid 拼 www 站文章地址.
/// </summary>
public class NeteaseNewsChannel : HotBoardChannelBase
{
    private const string RankUrl = "https://m.163.com/fe/api/hot/news/flow";

    /// <summary>
    /// Initializes a new instance of the <see cref="NeteaseNewsChannel"/> class.
    /// </summary>
    /// <param name="httpClient">HTTP 客户端.</param>
    /// <param name="gateway">调试网关地址（可空）.</param>
    public NeteaseNewsChannel(HttpClient httpClient, string? gateway)
        : base(httpClient, gateway)
    {
    }

    /// <inheritdoc/>
    public override string Id => "netease-news";

    /// <inheritdoc/>
    public override async Task<IReadOnlyList<HotBoardEntry>> FetchAsync(CancellationToken cancellationToken)
    {
        using var document = await GetJsonAsync(RankUrl, null, cancellationToken);

        var items = new List<HotBoardEntry>();
        if (document.RootElement.ValueKind == JsonValueKind.Object
            && document.RootElement.TryGetProperty("data", out var data)
            && data.ValueKind == JsonValueKind.Object
            && data.TryGetProperty("list", out var list)
            && list.ValueKind == JsonValueKind.Array)
        {
            foreach (var element in list.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var title = GetString(element, "title");
                if (string.IsNullOrWhiteSpace(title))
                {
                    continue;
                }

                var docid = GetScalar(element, "docid");
                var url = GetString(element, "url")
                    ?? (docid is null ? string.Empty : $"https://www.163.com/dy/article/{docid}.html");
                items.Add(Build(
                    title: title,
                    desc: GetString(element, "source"),
                    url: url,
                    cover: GetString(element, "imgsrc"),
                    hotValue: null));
            }
        }

        return items;
    }
}
