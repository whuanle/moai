using System.Text.Json;
using MoAI.AIPlugin.Dynamic.HotBoard;

namespace MoAI.AIPlugin.Dynamic.HotBoard.Channels;

/// <summary>
/// 今日头条热榜渠道：GET hot-event/hot-board，条目在 data[]（ClusterIdStr/Title/HotValue），
/// 需浏览器 UA（无 UA 会被风控拦截）.
/// </summary>
public class ToutiaoChannel : HotBoardChannelBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ToutiaoChannel"/> class.
    /// </summary>
    /// <param name="httpClient">HTTP 客户端.</param>
    /// <param name="gateway">调试网关地址（可空）.</param>
    public ToutiaoChannel(HttpClient httpClient, string? gateway)
        : base(httpClient, gateway)
    {
    }

    /// <inheritdoc/>
    public override string Id => "toutiao";

    /// <inheritdoc/>
    public override async Task<IReadOnlyList<HotBoardEntry>> FetchAsync(CancellationToken cancellationToken)
    {
        var url = "https://www.toutiao.com/hot-event/hot-board/?origin=toutiao_pc";
        using var document = await GetJsonAsync(url, null, cancellationToken);

        var items = new List<HotBoardEntry>();
        if (document.RootElement.ValueKind == JsonValueKind.Object
            && document.RootElement.TryGetProperty("data", out var data)
            && data.ValueKind == JsonValueKind.Array)
        {
            foreach (var element in data.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var title = GetString(element, "Title");
                var clusterId = GetString(element, "ClusterIdStr");
                if (string.IsNullOrWhiteSpace(title))
                {
                    continue;
                }

                var hotValue = GetScalar(element, "HotValue");
                items.Add(Build(
                    title: title,
                    desc: null,
                    url: clusterId is null ? string.Empty : $"https://www.toutiao.com/trending/{clusterId}/",
                    cover: GetNestedString(element, "Image", "url"),
                    hotValue: hotValue is not null && long.TryParse(hotValue, out var hot) ? hot : null));
            }
        }

        return items;
    }
}
