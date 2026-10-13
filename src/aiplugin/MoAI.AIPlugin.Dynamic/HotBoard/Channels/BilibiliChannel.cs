using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using MoAI.AIPlugin.Dynamic.HotBoard;
using MoAI.Infra.Exceptions;

namespace MoAI.AIPlugin.Dynamic.HotBoard.Channels;

/// <summary>
/// B站热门渠道：ranking/v2 接口需 WBI 签名（nav 接口取 img/sub key，混淆表重排后
/// md5(有序参数 + mixinKey) 得 w_rid）；主接口空榜或风控时回退旧版 ranking 接口（无签名）.
/// </summary>
public partial class BilibiliChannel : HotBoardChannelBase
{
    private const string NavUrl = "https://api.bilibili.com/x/web-interface/nav";

    /// <summary>
    /// WBI 混淆表（B站官方前端混淆常量，把 img_key+sub_key 重排后取前 32 位）.
    /// </summary>
    private static readonly int[] MixinKeyEncTab =
    [
        46, 47, 18, 2, 53, 8, 23, 32, 15, 50, 10, 31, 58, 3, 45, 35, 27, 43, 5, 49, 33, 9, 42, 19,
        29, 28, 14, 39, 12, 38, 41, 13, 37, 48, 7, 16, 24, 55, 40, 61, 26, 17, 0, 1, 60, 51, 30, 4,
        22, 25, 54, 21, 56, 59, 6, 63, 57, 62, 11, 36, 20, 34, 44, 52,
    ];

    private static readonly object KeyCacheLock = new();
    private static string? _cachedImgKey;
    private static string? _cachedSubKey;
    private static DateTimeOffset _keyCacheTime;

    /// <summary>
    /// Initializes a new instance of the <see cref="BilibiliChannel"/> class.
    /// </summary>
    /// <param name="httpClient">HTTP 客户端.</param>
    /// <param name="gateway">调试网关地址（可空）.</param>
    public BilibiliChannel(HttpClient httpClient, string? gateway)
        : base(httpClient, gateway)
    {
    }

    /// <inheritdoc/>
    public override string Id => "bilibili";

    /// <inheritdoc/>
    public override async Task<IReadOnlyList<HotBoardEntry>> FetchAsync(CancellationToken cancellationToken)
    {
        var items = await FetchFromRankingV2Async(cancellationToken);
        if (items.Count > 0)
        {
            return items;
        }

        return await FetchFromLegacyRankingAsync(cancellationToken);
    }

    /// <summary>
    /// 主接口：ranking/v2（WBI 签名，rid=0 全站榜）；接口异常/空榜返回空列表交给回退接口.
    /// </summary>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>返回归一化条目.</returns>
    private async Task<IReadOnlyList<HotBoardEntry>> FetchFromRankingV2Async(CancellationToken cancellationToken)
    {
        try
        {
            var (imgKey, subKey) = await GetWbiKeysAsync(cancellationToken);
            var wts = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var signed = SignWbi(new Dictionary<string, string> { ["rid"] = "0", ["type"] = "all", ["wts"] = wts.ToString() }, imgKey, subKey);
            var url = $"https://api.bilibili.com/x/web-interface/ranking/v2?{signed}";
            using var document = await GetJsonAsync(url, request =>
            {
                request.Headers.Referrer = new Uri("https://www.bilibili.com/ranking/all");
            }, cancellationToken);

            return ParseRankingList(document.RootElement, useLegacyFields: false);
        }
        catch (BusinessException)
        {
            // 主接口风控/失败交给回退接口
            return [];
        }
    }

    /// <summary>
    /// 回退接口：旧版 ranking（jsonp 参数对返回值无影响，不带签名）；接口异常返回空列表.
    /// </summary>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>返回归一化条目.</returns>
    private async Task<IReadOnlyList<HotBoardEntry>> FetchFromLegacyRankingAsync(CancellationToken cancellationToken)
    {
        try
        {
            var url = "https://api.bilibili.com/x/web-interface/ranking?jsonp=jsonp&rid=0&type=all";
            using var document = await GetJsonAsync(url, request =>
            {
                request.Headers.Referrer = new Uri("https://www.bilibili.com/ranking/all");
            }, cancellationToken);

            return ParseRankingList(document.RootElement, useLegacyFields: true);
        }
        catch (BusinessException)
        {
            return [];
        }
    }

    /// <summary>
    /// 解析 ranking 响应 data.list[]：bvid/title/desc/pic/stat.view，
    /// 回退接口结构为 video_review 字段.
    /// </summary>
    /// <param name="root">响应根元素.</param>
    /// <param name="useLegacyFields">是否回退接口字段.</param>
    /// <returns>返回归一化条目.</returns>
    private static List<HotBoardEntry> ParseRankingList(JsonElement root, bool useLegacyFields)
    {
        var items = new List<HotBoardEntry>();
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("data", out var data)
            || data.ValueKind != JsonValueKind.Object
            || !data.TryGetProperty("list", out var list)
            || list.ValueKind != JsonValueKind.Array)
        {
            return items;
        }

        foreach (var element in list.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var title = GetString(element, "title");
            var bvid = GetString(element, "bvid");
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(bvid))
            {
                continue;
            }

            long? hot;
            if (useLegacyFields)
            {
                var review = GetScalar(element, "video_review");
                hot = review is not null && long.TryParse(review, out var reviewValue) ? reviewValue : null;
            }
            else
            {
                var view = GetNestedScalar(element, "stat", "view");
                hot = view is not null && long.TryParse(view, out var viewValue) ? viewValue : null;
            }

            items.Add(Build(
                title: title,
                desc: GetString(element, "desc") ?? "该视频暂无简介",
                url: GetString(element, "short_link_v2") ?? $"https://www.bilibili.com/video/{bvid}",
                cover: HttpToHttps(GetString(element, "pic")),
                hotValue: hot));
        }

        return items;
    }

    /// <summary>
    /// WBI 签名：参数按 key 排序、value 过滤 !'()*，urlencode 拼接后 md5(query+mixinKey) 得 w_rid.
    /// </summary>
    /// <param name="parameters">请求参数（含 wts）.</param>
    /// <param name="imgKey">img key.</param>
    /// <param name="subKey">sub key.</param>
    /// <returns>返回已签名查询串.</returns>
    private static string SignWbi(Dictionary<string, string> parameters, string imgKey, string subKey)
    {
        var mixinKey = string.Concat(MixinKeyEncTab.Take(32).Select(n => (imgKey + subKey)[n]));
        var query = string.Join('&', parameters
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv =>
            {
                var value = IllegalCharactersRegex().Replace(kv.Value, string.Empty);
                return $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(value)}";
            }));
        var wRid = Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(query + mixinKey))).ToLowerInvariant();
        return $"{query}&w_rid={wRid}";
    }

    /// <summary>
    /// 获取 WBI key（nav 接口 wbi_img 的 URL 文件名），进程内缓存 1 小时（官方每日轮换）.
    /// </summary>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>返回 (imgKey, subKey).</returns>
    private async Task<(string ImgKey, string SubKey)> GetWbiKeysAsync(CancellationToken cancellationToken)
    {
        lock (KeyCacheLock)
        {
            if (_cachedImgKey is not null && _cachedSubKey is not null
                && DateTimeOffset.UtcNow - _keyCacheTime < TimeSpan.FromHours(1))
            {
                return (_cachedImgKey, _cachedSubKey);
            }
        }

        using var document = await GetJsonAsync(NavUrl, request =>
        {
            request.Headers.Referrer = new Uri("https://www.bilibili.com/");
        }, cancellationToken);

        string? imgUrl = null;
        string? subUrl = null;
        if (document.RootElement.ValueKind == JsonValueKind.Object
            && document.RootElement.TryGetProperty("wbi_img", out var wbiImg)
            && wbiImg.ValueKind == JsonValueKind.Object)
        {
            imgUrl = GetString(wbiImg, "img_url");
            subUrl = GetString(wbiImg, "sub_url");
        }

        if (string.IsNullOrWhiteSpace(imgUrl) || string.IsNullOrWhiteSpace(subUrl))
        {
            throw new BusinessException(502, "B站热榜拉取失败：nav 接口缺少 wbi_img");
        }

        var imgKey = ExtractKeyFromUrl(imgUrl);
        var subKey = ExtractKeyFromUrl(subUrl);
        lock (KeyCacheLock)
        {
            _cachedImgKey = imgKey;
            _cachedSubKey = subKey;
            _keyCacheTime = DateTimeOffset.UtcNow;
        }

        return (imgKey, subKey);
    }

    /// <summary>
    /// 从 wbi_img URL 提取 key（文件名去扩展名）.
    /// </summary>
    /// <param name="url">图片地址.</param>
    /// <returns>返回 key.</returns>
    private static string ExtractKeyFromUrl(string url)
    {
        var lastSlash = url.LastIndexOf('/');
        var lastDot = url.LastIndexOf('.');
        if (lastSlash < 0 || lastDot <= lastSlash)
        {
            return url;
        }

        return url[(lastSlash + 1)..lastDot];
    }

    /// <summary>
    /// http 封面升级 https.
    /// </summary>
    /// <param name="url">封面地址.</param>
    /// <returns>返回处理后的地址.</returns>
    private static string? HttpToHttps(string? url)
    {
        return url is null ? null : url.StartsWith("http:", StringComparison.OrdinalIgnoreCase) ? $"https:{url[5..]}" : url;
    }

    [GeneratedRegex("[!'()*]")]
    private static partial Regex IllegalCharactersRegex();
}
