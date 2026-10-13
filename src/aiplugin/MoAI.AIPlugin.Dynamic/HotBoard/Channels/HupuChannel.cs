using System.Text.Json;
using MoAI.AIPlugin.Dynamic.HotBoard;

namespace MoAI.AIPlugin.Dynamic.HotBoard.Channels;

/// <summary>
/// 虎扑步行街热帖渠道：GET m.hupu.com/api/v2/bbs/topicThreads（topicId=1 主干道），
/// 条目在 data.topicThreads[]，热度为回复数.
/// </summary>
public class HupuChannel : HotBoardChannelBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HupuChannel"/> class.
    /// </summary>
    /// <param name="httpClient">HTTP 客户端.</param>
    /// <param name="gateway">调试网关地址（可空）.</param>
    public HupuChannel(HttpClient httpClient, string? gateway)
        : base(httpClient, gateway)
    {
    }

    /// <inheritdoc/>
    public override string Id => "hupu";

    /// <inheritdoc/>
    public override async Task<IReadOnlyList<HotBoardEntry>> FetchAsync(CancellationToken cancellationToken)
    {
        var url = "https://m.hupu.com/api/v2/bbs/topicThreads?topicId=1&page=1";
        using var document = await GetJsonAsync(url, null, cancellationToken);

        var items = new List<HotBoardEntry>();
        if (document.RootElement.ValueKind == JsonValueKind.Object
            && document.RootElement.TryGetProperty("data", out var data)
            && data.ValueKind == JsonValueKind.Object
            && data.TryGetProperty("topicThreads", out var threads)
            && threads.ValueKind == JsonValueKind.Array)
        {
            foreach (var element in threads.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var title = GetString(element, "title");
                // tid 实测为数字 token，GetScalar 取原始文本
                var tid = GetScalar(element, "tid");
                if (string.IsNullOrWhiteSpace(title))
                {
                    continue;
                }

                var replies = GetScalar(element, "replies");
                items.Add(Build(
                    title: title,
                    desc: null,
                    url: tid is null ? string.Empty : $"https://bbs.hupu.com/{tid}.html",
                    cover: null,
                    hotValue: replies is not null && long.TryParse(replies, out var replyCount) ? replyCount : null));
            }
        }

        return items;
    }
}
