using System.Text.Json;
using MoAI.AIPlugin.Dynamic.HotBoard;

namespace MoAI.AIPlugin.Dynamic.HotBoard.Channels;

/// <summary>
/// 极客公园热门文章渠道：GET mainssl.geekpark.net/api/v2，条目在 homepage_posts[].post，
/// 热度为阅读数（views，字符串数字）.
/// </summary>
public class GeekparkChannel : HotBoardChannelBase
{
    private const string RankUrl = "https://mainssl.geekpark.net/api/v2";

    /// <summary>
    /// Initializes a new instance of the <see cref="GeekparkChannel"/> class.
    /// </summary>
    /// <param name="httpClient">HTTP 客户端.</param>
    /// <param name="gateway">调试网关地址（可空）.</param>
    public GeekparkChannel(HttpClient httpClient, string? gateway)
        : base(httpClient, gateway)
    {
    }

    /// <inheritdoc/>
    public override string Id => "geekpark";

    /// <inheritdoc/>
    public override async Task<IReadOnlyList<HotBoardEntry>> FetchAsync(CancellationToken cancellationToken)
    {
        using var document = await GetJsonAsync(RankUrl, null, cancellationToken);

        var items = new List<HotBoardEntry>();
        if (document.RootElement.ValueKind == JsonValueKind.Object
            && document.RootElement.TryGetProperty("homepage_posts", out var homepagePosts)
            && homepagePosts.ValueKind == JsonValueKind.Array)
        {
            foreach (var wrapper in homepagePosts.EnumerateArray())
            {
                if (wrapper.ValueKind != JsonValueKind.Object
                    || !wrapper.TryGetProperty("post", out var element)
                    || element.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var title = GetString(element, "title");
                if (string.IsNullOrWhiteSpace(title))
                {
                    continue;
                }

                var postId = GetScalar(element, "id");
                var views = GetScalar(element, "views");
                long? hotValue = views is not null && long.TryParse(views, out var viewCount) && viewCount > 0 ? viewCount : null;

                items.Add(Build(
                    title: title,
                    desc: GetString(element, "abstract"),
                    url: postId is null ? string.Empty : $"https://www.geekpark.net/news/{postId}",
                    cover: GetString(element, "cover_url"),
                    hotValue: hotValue));
            }
        }

        return items;
    }
}
