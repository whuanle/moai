using System.Text.Json;
using System.Text.RegularExpressions;
using MoAI.AIPlugin.Dynamic.HotBoard;

namespace MoAI.AIPlugin.Dynamic.HotBoard.Channels;

/// <summary>
/// 虎嗅快讯（24 小时）渠道：GET moment-api.huxiu.com/web-v3/moment/feed?platform=www
/// （需 Referer 指向 www.huxiu.com，否则风控），条目在 data.moment_list.datalist[]；
/// content 为 br 分段的快讯文本，首行为标题、余下为摘要；热度为点赞数（count_info.agree_num）.
/// </summary>
public partial class HuxiuChannel : HotBoardChannelBase
{
    private const string FeedUrl = "https://moment-api.huxiu.com/web-v3/moment/feed?platform=www";

    /// <summary>
    /// Initializes a new instance of the <see cref="HuxiuChannel"/> class.
    /// </summary>
    /// <param name="httpClient">HTTP 客户端.</param>
    /// <param name="gateway">调试网关地址（可空）.</param>
    public HuxiuChannel(HttpClient httpClient, string? gateway)
        : base(httpClient, gateway)
    {
    }

    /// <inheritdoc/>
    public override string Id => "huxiu";

    /// <inheritdoc/>
    public override async Task<IReadOnlyList<HotBoardEntry>> FetchAsync(CancellationToken cancellationToken)
    {
        using var document = await GetJsonAsync(FeedUrl, request =>
        {
            request.Headers.Referrer = new Uri("https://www.huxiu.com/moment/");
        }, cancellationToken);

        var items = new List<HotBoardEntry>();
        if (document.RootElement.ValueKind == JsonValueKind.Object
            && document.RootElement.TryGetProperty("data", out var data)
            && data.ValueKind == JsonValueKind.Object
            && data.TryGetProperty("moment_list", out var momentList)
            && momentList.ValueKind == JsonValueKind.Object
            && momentList.TryGetProperty("datalist", out var datalist)
            && datalist.ValueKind == JsonValueKind.Array)
        {
            foreach (var element in datalist.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var content = GetString(element, "content");
                if (string.IsNullOrWhiteSpace(content))
                {
                    continue;
                }

                var (title, desc) = SplitMomentText(content);
                if (string.IsNullOrWhiteSpace(title))
                {
                    continue;
                }

                // object_id 实测为数字 token，GetScalar 取原始文本避免长整型精度丢失
                var momentId = GetScalar(element, "object_id");
                var agree = GetNestedScalar(element, "count_info", "agree_num");
                items.Add(Build(
                    title: title,
                    desc: desc,
                    url: momentId is null ? string.Empty : $"https://www.huxiu.com/moment/{momentId}.html",
                    cover: null,
                    hotValue: agree is not null && long.TryParse(agree, out var agreeCount) && agreeCount > 0 ? agreeCount : null));
            }
        }

        return items;
    }

    /// <summary>
    /// 快讯正文按 br 分段：首段去句号为标题，余下段拼接为摘要.
    /// </summary>
    /// <param name="content">快讯正文（含 br 标签）.</param>
    /// <returns>返回标题与摘要.</returns>
    private static (string Title, string? Desc) SplitMomentText(string content)
    {
        var lines = BrTagRegex()
            .Replace(content, "\n")
            .Split('\n')
            .Select(StripHtml)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => line!.Trim())
            .ToList();
        if (lines.Count == 0)
        {
            return (string.Empty, null);
        }

        var title = lines[0].EndsWith('。') ? lines[0][..^1] : lines[0];
        var desc = lines.Count > 1 ? string.Join(' ', lines.Skip(1)) : null;
        return (title, desc);
    }

    [GeneratedRegex("(?i)<br\\s*/?>")]
    private static partial Regex BrTagRegex();
}
