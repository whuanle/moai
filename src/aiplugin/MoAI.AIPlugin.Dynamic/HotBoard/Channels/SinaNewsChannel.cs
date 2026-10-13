using System.Text.Json;
using MoAI.AIPlugin.Dynamic.HotBoard;
using MoAI.Infra.Exceptions;

namespace MoAI.AIPlugin.Dynamic.HotBoard.Channels;

/// <summary>
/// 新浪新闻总排行渠道：GET top.news.sina.com.cn/ws/GetTopDataList.php（日榜，top_time 取北京日期），
/// 响应为 "var data = {...};" 的 JS 变量赋值文本，剥壳后解析；条目在 data.data[]，
/// 热度为 top_num（字符串数字，可能带千分位逗号），desc 放媒体名（media）.
/// </summary>
public class SinaNewsChannel : HotBoardChannelBase
{
    private const string RankUrl = "https://top.news.sina.com.cn/ws/GetTopDataList.php";

    /// <summary>
    /// Initializes a new instance of the <see cref="SinaNewsChannel"/> class.
    /// </summary>
    /// <param name="httpClient">HTTP 客户端.</param>
    /// <param name="gateway">调试网关地址（可空）.</param>
    public SinaNewsChannel(HttpClient httpClient, string? gateway)
        : base(httpClient, gateway)
    {
    }

    /// <inheritdoc/>
    public override string Id => "sina-news";

    /// <inheritdoc/>
    public override async Task<IReadOnlyList<HotBoardEntry>> FetchAsync(CancellationToken cancellationToken)
    {
        // 日榜按北京时间取当天日期；新浪榜单接口无公开时区约定，固定 +8（无夏令时）
        var beijingToday = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(8)).ToString("yyyyMMdd");
        var url = $"{RankUrl}?top_type=day&top_cat=www_www_all_suda_suda&top_time={beijingToday}&top_show_num=50";
        var text = await GetStringAsync(url, null, cancellationToken);
        using var document = ParseSinaData(text);

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

                var title = GetString(element, "title");
                if (string.IsNullOrWhiteSpace(title))
                {
                    continue;
                }

                var topNum = GetScalar(element, "top_num")?.Replace(",", string.Empty);
                items.Add(Build(
                    title: title,
                    desc: GetString(element, "media"),
                    url: GetString(element, "url"),
                    cover: null,
                    hotValue: topNum is not null && long.TryParse(topNum, out var hotCount) && hotCount > 0 ? hotCount : null));
            }
        }

        return items;
    }

    /// <summary>
    /// 剥去 "var data = {...};" 的 JS 变量壳并解析 JSON；格式不符统一包装为 502 业务异常.
    /// </summary>
    /// <param name="text">响应正文.</param>
    /// <returns>返回解析后的 JSON 文档（调用方负责释放）.</returns>
    private static JsonDocument ParseSinaData(string text)
    {
        const string Prefix = "var data =";
        var body = text.Trim();
        if (!body.StartsWith(Prefix, StringComparison.Ordinal))
        {
            throw new BusinessException(502, "热榜源响应不是有效 JSON（缺少 var data 前缀）");
        }

        var json = body[Prefix.Length..].Trim().TrimEnd(';');
        try
        {
            return JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new BusinessException(502, $"热榜源响应不是有效 JSON（{ex.Message}）");
        }
    }
}
