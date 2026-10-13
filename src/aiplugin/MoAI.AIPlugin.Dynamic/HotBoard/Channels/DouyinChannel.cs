using System.Text.Json;
using System.Text.RegularExpressions;
using MoAI.AIPlugin.Dynamic.HotBoard;

namespace MoAI.AIPlugin.Dynamic.HotBoard.Channels;

/// <summary>
/// 抖音热点渠道：先访问 passport 登录引导接口拿 passport_csrf_token（Set-Cookie），
/// 再 GET aweme/v1/web/hot/search/list 携带该 Cookie，条目在 data.word_list[].
/// </summary>
public partial class DouyinChannel : HotBoardChannelBase
{
    private const string CsrfCookieUrl = "https://www.douyin.com/passport/general/login_guiding_strategy/?aid=6383";

    private const string HotListUrl = "https://www.douyin.com/aweme/v1/web/hot/search/list/?device_platform=webapp&aid=6383&channel=channel_pc_web&detail_list=1";

    /// <summary>
    /// Initializes a new instance of the <see cref="DouyinChannel"/> class.
    /// </summary>
    /// <param name="httpClient">HTTP 客户端.</param>
    /// <param name="gateway">调试网关地址（可空）.</param>
    public DouyinChannel(HttpClient httpClient, string? gateway)
        : base(httpClient, gateway)
    {
    }

    /// <inheritdoc/>
    public override string Id => "douyin";

    /// <inheritdoc/>
    public override async Task<IReadOnlyList<HotBoardEntry>> FetchAsync(CancellationToken cancellationToken)
    {
        var csrfToken = await GetCsrfTokenAsync(cancellationToken);
        using var document = await GetJsonAsync(HotListUrl, request =>
        {
            request.Headers.Referrer = new Uri("https://www.douyin.com/");
            request.Headers.TryAddWithoutValidation("Cookie", $"passport_csrf_token={csrfToken}");
        }, cancellationToken);

        var items = new List<HotBoardEntry>();
        if (document.RootElement.ValueKind == JsonValueKind.Object
            && document.RootElement.TryGetProperty("data", out var data)
            && data.ValueKind == JsonValueKind.Object
            && data.TryGetProperty("word_list", out var wordList)
            && wordList.ValueKind == JsonValueKind.Array)
        {
            foreach (var element in wordList.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var title = GetString(element, "word");
                if (string.IsNullOrWhiteSpace(title))
                {
                    continue;
                }

                var sentenceId = GetString(element, "sentence_id");
                items.Add(Build(
                    title: title,
                    desc: null,
                    url: sentenceId is null ? string.Empty : $"https://www.douyin.com/hot/{sentenceId}",
                    cover: null,
                    hotValue: GetHotValue(element)));
            }
        }

        return items;
    }

    /// <summary>
    /// 获取临时 CSRF Token：读登录引导接口的 Set-Cookie，取 passport_csrf_token 值；失败返回空串（仍尝试裸拉）.
    /// </summary>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>返回 token 或空串.</returns>
    private async Task<string> GetCsrfTokenAsync(CancellationToken cancellationToken)
    {
        try
        {
            var cookies = await GetResponseHeaderValuesAsync(CsrfCookieUrl, "Set-Cookie", null, cancellationToken);
            foreach (var cookie in cookies)
            {
                var match = CsrfTokenRegex().Match(cookie);
                if (match.Success)
                {
                    return match.Groups[1].Value;
                }
            }
        }
        catch (MoAI.Infra.Exceptions.BusinessException)
        {
            // CSRF 预取失败不阻断主流程：热榜接口对空 token 通常仍放行
        }

        return string.Empty;
    }

    /// <summary>
    /// 读取热度值（数字或数字串）.
    /// </summary>
    /// <param name="element">条目 JSON.</param>
    /// <returns>返回热度值或 null.</returns>
    private static long? GetHotValue(JsonElement element)
    {
        var scalar = GetScalar(element, "hot_value");
        return scalar is not null && long.TryParse(scalar, out var value) ? value : null;
    }

    [GeneratedRegex("passport_csrf_token=([^;]*)")]
    private static partial Regex CsrfTokenRegex();
}
