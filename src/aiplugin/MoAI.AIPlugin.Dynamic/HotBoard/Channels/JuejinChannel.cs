using System.Text.Json;
using MoAI.AIPlugin.Dynamic.HotBoard;

namespace MoAI.AIPlugin.Dynamic.HotBoard.Channels;

/// <summary>
/// 稀土掘金文章榜渠道：GET content_api/v1/content/article_rank（category_id=1 综合榜），
/// 条目在 data[]，标题/摘要在 content 内，热度取 content_counter.view（阅读数）.
/// </summary>
public class JuejinChannel : HotBoardChannelBase
{
    private const string RankUrl = "https://api.juejin.cn/content_api/v1/content/article_rank?category_id=1&type=hot";

    /// <summary>
    /// Initializes a new instance of the <see cref="JuejinChannel"/> class.
    /// </summary>
    /// <param name="httpClient">HTTP 客户端.</param>
    /// <param name="gateway">调试网关地址（可空）.</param>
    public JuejinChannel(HttpClient httpClient, string? gateway)
        : base(httpClient, gateway)
    {
    }

    /// <inheritdoc/>
    public override string Id => "juejin";

    /// <inheritdoc/>
    public override async Task<IReadOnlyList<HotBoardEntry>> FetchAsync(CancellationToken cancellationToken)
    {
        using var document = await GetJsonAsync(RankUrl, request =>
        {
            request.Headers.Referrer = new Uri("https://juejin.cn/hot/articles");
        }, cancellationToken);

        var items = new List<HotBoardEntry>();
        if (document.RootElement.ValueKind == JsonValueKind.Object
            && document.RootElement.TryGetProperty("data", out var data)
            && data.ValueKind == JsonValueKind.Array)
        {
            foreach (var element in data.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.Object
                    || !element.TryGetProperty("content", out var content)
                    || content.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var title = GetString(content, "title");
                if (string.IsNullOrWhiteSpace(title))
                {
                    continue;
                }

                var contentId = GetString(content, "content_id");
                var view = GetNestedScalar(element, "content_counter", "view");
                items.Add(Build(
                    title: title,
                    desc: GetString(content, "brief"),
                    url: string.IsNullOrWhiteSpace(contentId) ? string.Empty : $"https://juejin.cn/post/{contentId}",
                    cover: GetString(content, "cover_image"),
                    hotValue: view is not null && long.TryParse(view, out var viewCount) && viewCount > 0 ? viewCount : null));
            }
        }

        return items;
    }
}
