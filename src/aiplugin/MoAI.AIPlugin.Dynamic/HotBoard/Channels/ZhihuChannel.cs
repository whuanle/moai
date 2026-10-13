using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using MoAI.AIPlugin.Dynamic.HotBoard;

namespace MoAI.AIPlugin.Dynamic.HotBoard.Channels;

/// <summary>
/// 知乎热榜渠道：GET api.zhihu.com/topstory/hot-lists/total，条目在 data[].target，
/// 热度在 detail_text（"4589 万热度"），问题链接由 target.url 末段拼回.
/// </summary>
public partial class ZhihuChannel : HotBoardChannelBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ZhihuChannel"/> class.
    /// </summary>
    /// <param name="httpClient">HTTP 客户端.</param>
    /// <param name="gateway">调试网关地址（可空）.</param>
    public ZhihuChannel(HttpClient httpClient, string? gateway)
        : base(httpClient, gateway)
    {
    }

    /// <inheritdoc/>
    public override string Id => "zhihu";

    /// <inheritdoc/>
    public override async Task<IReadOnlyList<HotBoardEntry>> FetchAsync(CancellationToken cancellationToken)
    {
        var url = "https://api.zhihu.com/topstory/hot-lists/total?limit=50";
        using var document = await GetJsonAsync(url, null, cancellationToken);

        var items = new List<HotBoardEntry>();
        if (document.RootElement.ValueKind == JsonValueKind.Object
            && document.RootElement.TryGetProperty("data", out var data)
            && data.ValueKind == JsonValueKind.Array)
        {
            foreach (var element in data.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.Object
                    || !element.TryGetProperty("target", out var target)
                    || target.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var title = GetString(target, "title");
                if (string.IsNullOrWhiteSpace(title))
                {
                    continue;
                }

                // target.url 形如 https://api.zhihu.com/questions/6783041...，末段为问题 id
                var questionId = GetString(target, "url")?.Split('/').LastOrDefault();
                var questionUrl = string.IsNullOrWhiteSpace(questionId) ? string.Empty : $"https://www.zhihu.com/question/{questionId}";

                string? cover = null;
                if (element.TryGetProperty("children", out var children)
                    && children.ValueKind == JsonValueKind.Array
                    && children.GetArrayLength() > 0
                    && children[0].ValueKind == JsonValueKind.Object)
                {
                    cover = GetString(children[0], "thumbnail");
                }

                items.Add(Build(
                    title: title,
                    desc: GetString(target, "excerpt"),
                    url: questionUrl,
                    cover: cover,
                    hotValue: ParseHotValue(GetString(element, "detail_text"))));
            }
        }

        return items;
    }

    /// <summary>
    /// 解析热度文本："4589 万热度" → 4589 * 10000；纯数字直接取值；解析失败返回 null.
    /// </summary>
    /// <param name="detailText">detail_text 热度文本.</param>
    /// <returns>返回热度值或 null.</returns>
    private static long? ParseHotValue(string? detailText)
    {
        if (string.IsNullOrWhiteSpace(detailText))
        {
            return null;
        }

        var wanMatch = WanHotRegex().Match(detailText);
        if (wanMatch.Success
            && double.TryParse(wanMatch.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var wanValue))
        {
            return (long)(wanValue * 10000);
        }

        var numberMatch = NumberRegex().Match(detailText);
        if (numberMatch.Success
            && long.TryParse(numberMatch.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        return null;
    }

    [GeneratedRegex("([\\d.]+)\\s*万")]
    private static partial Regex WanHotRegex();

    [GeneratedRegex("\\d+")]
    private static partial Regex NumberRegex();
}
