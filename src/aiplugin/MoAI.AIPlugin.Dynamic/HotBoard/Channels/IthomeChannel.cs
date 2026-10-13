using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using MoAI.AIPlugin.Dynamic.HotBoard;

namespace MoAI.AIPlugin.Dynamic.HotBoard.Channels;

/// <summary>
/// IT之家热榜渠道：GET m.ithome.com/rankm/（HTML 页），AngleSharp 解析
/// .rank-box .placeholder 列表，短链（html/live/{id}.htm）改写为 www 站标准文章地址.
/// </summary>
public partial class IthomeChannel : HotBoardChannelBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="IthomeChannel"/> class.
    /// </summary>
    /// <param name="httpClient">HTTP 客户端.</param>
    /// <param name="gateway">调试网关地址（可空）.</param>
    public IthomeChannel(HttpClient httpClient, string? gateway)
        : base(httpClient, gateway)
    {
    }

    /// <inheritdoc/>
    public override string Id => "ithome";

    /// <inheritdoc/>
    public override async Task<IReadOnlyList<HotBoardEntry>> FetchAsync(CancellationToken cancellationToken)
    {
        var url = "https://m.ithome.com/rankm/";
        var html = await GetStringAsync(url, null, cancellationToken);

        var items = new List<HotBoardEntry>();
        var parser = new HtmlParser();
        using var document = await parser.ParseDocumentAsync(html, cancellationToken);
        foreach (var node in document.QuerySelectorAll(".rank-box .placeholder"))
        {
            var title = node.QuerySelector(".plc-title")?.TextContent?.Trim();
            if (string.IsNullOrWhiteSpace(title))
            {
                continue;
            }

            var href = node.QuerySelector("a")?.GetAttribute("href")?.Trim();
            var articleId = href is null ? null : ExtractArticleId(href);
            var reviewText = node.QuerySelector(".review-num")?.TextContent?.Trim();
            var reviewDigits = reviewText is null ? string.Empty : new string(reviewText.Where(char.IsDigit).ToArray());
            var cover = node.QuerySelector("img")?.GetAttribute("data-original")?.Trim();
            long? hotValue = reviewDigits.Length > 0 && long.TryParse(reviewDigits, out var reviewCount) ? reviewCount : null;

            items.Add(Build(
                title: title,
                desc: null,
                url: articleId is null ? href ?? string.Empty : BuildArticleUrl(articleId),
                cover: cover,
                hotValue: hotValue));
        }

        return items;
    }

    /// <summary>
    /// 从短链提取文章数字 id（html/123456.htm 或 live/123456.htm）.
    /// </summary>
    /// <param name="href">短链地址.</param>
    /// <returns>返回数字 id，匹配失败返回 null.</returns>
    private static string? ExtractArticleId(string href)
    {
        var match = ArticleLinkRegex().Match(href);
        return match.Success ? match.Groups[1].Value : null;
    }

    /// <summary>
    /// 数字 id 拼 www 站文章地址（id 前 3 位一段 + 余下一段，IT之家固定路径规则）.
    /// </summary>
    /// <param name="articleId">文章数字 id.</param>
    /// <returns>返回文章地址.</returns>
    private static string BuildArticleUrl(string articleId)
    {
        var head = articleId.Length <= 3 ? articleId : articleId[..3];
        var tail = articleId.Length <= 3 ? string.Empty : articleId[3..];
        return $"https://www.ithome.com/0/{head}/{tail}.htm";
    }

    [GeneratedRegex("(?:html|live)/(\\d+)\\.htm")]
    private static partial Regex ArticleLinkRegex();
}
