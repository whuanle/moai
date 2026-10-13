using System.Text.Json;
using MoAI.AIPlugin.Dynamic.HotBoard;

namespace MoAI.AIPlugin.Dynamic.HotBoard.Channels;

/// <summary>
/// 微博热搜渠道：GET weibo.com/ajax/side/hotSearch（需浏览器 UA + Referer，否则风控），
/// 条目在 data.realtime[]，上游无热度值，搜索链接由词条拼出.
/// </summary>
public class WeiboChannel : HotBoardChannelBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WeiboChannel"/> class.
    /// </summary>
    /// <param name="httpClient">HTTP 客户端.</param>
    /// <param name="gateway">调试网关地址（可空）.</param>
    public WeiboChannel(HttpClient httpClient, string? gateway)
        : base(httpClient, gateway)
    {
    }

    /// <inheritdoc/>
    public override string Id => "weibo";

    /// <inheritdoc/>
    public override async Task<IReadOnlyList<HotBoardEntry>> FetchAsync(CancellationToken cancellationToken)
    {
        var url = "https://weibo.com/ajax/side/hotSearch";
        using var document = await GetJsonAsync(url, request =>
        {
            request.Headers.Referrer = new Uri("https://weibo.com/");
        }, cancellationToken);

        var items = new List<HotBoardEntry>();
        if (document.RootElement.ValueKind == JsonValueKind.Object
            && document.RootElement.TryGetProperty("data", out var data)
            && data.ValueKind == JsonValueKind.Object
            && data.TryGetProperty("realtime", out var realtime)
            && realtime.ValueKind == JsonValueKind.Array)
        {
            foreach (var element in realtime.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var word = GetString(element, "word");
                var wordScheme = GetString(element, "word_scheme");
                var title = word ?? wordScheme;
                if (string.IsNullOrWhiteSpace(title))
                {
                    continue;
                }

                items.Add(Build(
                    title: title,
                    desc: wordScheme ?? $"#{title}#",
                    url: "https://s.weibo.com/weibo?q=" + Uri.EscapeDataString(title),
                    cover: null,
                    hotValue: null));
            }
        }

        return items;
    }
}
