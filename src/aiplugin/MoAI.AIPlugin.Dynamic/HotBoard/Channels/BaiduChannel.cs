using System.Text.Json;
using System.Text.RegularExpressions;
using MoAI.AIPlugin.Dynamic.HotBoard;

namespace MoAI.AIPlugin.Dynamic.HotBoard.Channels;

/// <summary>
/// 百度热搜渠道：GET top.baidu.com/board?tab=realtime（HTML 页），从
/// <c><!--s-data:{json}--></c> 注释中抠出整榜数据（data.cards[0].content，可能再嵌套一层 content）.
/// </summary>
public partial class BaiduChannel : HotBoardChannelBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BaiduChannel"/> class.
    /// </summary>
    /// <param name="httpClient">HTTP 客户端.</param>
    /// <param name="gateway">调试网关地址（可空）.</param>
    public BaiduChannel(HttpClient httpClient, string? gateway)
        : base(httpClient, gateway)
    {
    }

    /// <inheritdoc/>
    public override string Id => "baidu";

    /// <inheritdoc/>
    public override async Task<IReadOnlyList<HotBoardEntry>> FetchAsync(CancellationToken cancellationToken)
    {
        var url = "https://top.baidu.com/board?tab=realtime";
        var html = await GetStringAsync(url, null, cancellationToken);

        var items = new List<HotBoardEntry>();
        var match = SDataRegex().Match(html);
        if (!match.Success)
        {
            return items;
        }

        using var document = JsonDocument.Parse(match.Groups[1].Value);
        var content = FindCardContent(document.RootElement);
        if (content.ValueKind != JsonValueKind.Array)
        {
            return items;
        }

        foreach (var element in EnumerateLeafItems(content))
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var title = GetString(element, "word") ?? GetString(element, "title");
            if (string.IsNullOrWhiteSpace(title))
            {
                continue;
            }

            var query = GetString(element, "query") ?? title;
            items.Add(Build(
                title: title,
                desc: GetString(element, "desc"),
                url: "https://www.baidu.com/s?wd=" + Uri.EscapeDataString(query),
                cover: GetString(element, "img") ?? GetNestedString(element, "imgInfo", "src"),
                hotValue: ParseHotValue(GetScalar(element, "hotScore") ?? GetString(element, "hotTag"))));
        }

        return items;
    }

    /// <summary>
    /// 定位榜单条目数组：优先 data.cards[0].content，回退根对象 cards[0].content.
    /// </summary>
    /// <param name="root">s-data JSON 根元素.</param>
    /// <returns>返回 content 数组（缺失返回 Undefined 元素）.</returns>
    private static JsonElement FindCardContent(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object)
        {
            var content = GetCardContent(data);
            if (content.ValueKind == JsonValueKind.Array)
            {
                return content;
            }
        }

        return root.ValueKind == JsonValueKind.Object ? GetCardContent(root) : default;
    }

    /// <summary>
    /// 读取对象上 cards[0].content.
    /// </summary>
    /// <param name="element">JSON 对象.</param>
    /// <returns>返回 content 数组（缺失返回 Undefined 元素）.</returns>
    private static JsonElement GetCardContent(JsonElement element)
    {
        if (element.TryGetProperty("cards", out var cards)
            && cards.ValueKind == JsonValueKind.Array
            && cards.GetArrayLength() > 0
            && cards[0].ValueKind == JsonValueKind.Object
            && cards[0].TryGetProperty("content", out var content)
            && content.ValueKind == JsonValueKind.Array
            && content.GetArrayLength() > 0)
        {
            return content;
        }

        return default;
    }

    /// <summary>
    /// 展开条目：部分卡片首元素自带嵌套 content 子数组（子榜），有则下钻.
    /// </summary>
    /// <param name="content">卡片 content 数组.</param>
    /// <returns>返回叶子条目序列.</returns>
    private static IEnumerable<JsonElement> EnumerateLeafItems(JsonElement content)
    {
        foreach (var element in content.EnumerateArray())
        {
            if (element.ValueKind == JsonValueKind.Object
                && element.TryGetProperty("content", out var nested)
                && nested.ValueKind == JsonValueKind.Array)
            {
                foreach (var leaf in nested.EnumerateArray())
                {
                    yield return leaf;
                }
            }
            else
            {
                yield return element;
            }
        }
    }

    /// <summary>
    /// 解析热度值：hotScore 为数字串直接取值；hotTag（如"热"）取其中数字；均无返回 null.
    /// </summary>
    /// <param name="hotText">热度文本.</param>
    /// <returns>返回热度值或 null.</returns>
    private static long? ParseHotValue(string? hotText)
    {
        if (string.IsNullOrWhiteSpace(hotText))
        {
            return null;
        }

        var digits = new string(hotText.Where(char.IsDigit).ToArray());
        return digits.Length > 0 && long.TryParse(digits, out var value) && value > 0 ? value : null;
    }

    [GeneratedRegex("<!--s-data:(.*?)-->", RegexOptions.Singleline)]
    private static partial Regex SDataRegex();
}
