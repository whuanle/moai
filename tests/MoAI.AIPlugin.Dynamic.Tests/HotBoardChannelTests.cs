using MoAI.AIPlugin.Dynamic.HotBoard;
using MoAI.AIPlugin.Dynamic.HotBoard.Channels;
using Xunit;

namespace MoAI.AIPlugin.Dynamic.Tests;

/// <summary>
/// 热榜渠道解析测试：按各平台真实报文结构回放响应，断言归一化条目（不发起网络请求）.
/// </summary>
public class HotBoardChannelTests
{
    [Fact]
    public async Task Zhihu_ParsesTargetsAndWanHotValue()
    {
        var (items, _) = await RunAsync(
            client => new ZhihuChannel(client, null),
            _ => StubHttpHandler.Json(new
            {
                data = new object[]
                {
                    new
                    {
                        target = new { title = "如何看待某事件", excerpt = "事件摘录", url = "https://api.zhihu.com/questions/6783041", id = 7173456789012345676L },
                        detail_text = "4589 万热度",
                        children = new object[] { new { thumbnail = "https://pic1.zhimg.com/t.jpg" } },
                    },
                    new
                    {
                        target = new { title = "第二条问题", url = "https://api.zhihu.com/questions/42" },
                        detail_text = "1234 热度",
                    },
                },
            }));

        Assert.Equal(2, items.Count);
        Assert.Equal("如何看待某事件", items[0].Title);
        Assert.Equal("https://www.zhihu.com/question/6783041", items[0].Url);
        Assert.Equal("事件摘录", items[0].Desc);
        Assert.Equal("https://pic1.zhimg.com/t.jpg", items[0].Cover);
        Assert.Equal(45890000L, items[0].HotValue);
        Assert.Equal("https://www.zhihu.com/question/42", items[1].Url);
        Assert.Equal(1234L, items[1].HotValue);
        Assert.Null(items[1].Cover);
    }

    [Fact]
    public async Task Weibo_ParsesWordsAndSearchLinks()
    {
        var (items, _) = await RunAsync(
            client => new WeiboChannel(client, null),
            _ => StubHttpHandler.Json(new
            {
                data = new
                {
                    realtime = new object[]
                    {
                        new { word = "某热搜词条", word_scheme = "#某热搜词条#", mid = "5123456" },
                        new { word = "第二词条" },
                    },
                },
            }));

        Assert.Equal(2, items.Count);
        Assert.Equal("某热搜词条", items[0].Title);
        Assert.Equal("https://s.weibo.com/weibo?q=" + Uri.EscapeDataString("某热搜词条"), items[0].Url);
        Assert.Equal("#某热搜词条#", items[0].Desc);
        Assert.Null(items[0].HotValue);
        Assert.Equal("第二词条", items[1].Title);
        Assert.Equal("#第二词条#", items[1].Desc);
    }

    [Fact]
    public async Task Kr36_PostsRankApiAndPreservesNumericItemId()
    {
        var (items, handler) = await RunAsync(
            client => new Kr36Channel(client, null),
            _ => StubHttpHandler.Json(new
            {
                data = new
                {
                    hotRankList = new object[]
                    {
                        new
                        {
                            itemId = 7171234567890123456L,
                            templateMaterial = new { widgetTitle = "36氪热文", widgetImage = "https://img.36kr.com/a.png", statCollect = "456" },
                        },
                        new { templateMaterial = new { widgetTitle = "无收藏文章" } },
                    },
                },
            }));

        Assert.Equal("POST", Assert.Single(handler.RequestMethods));
        Assert.Contains("gateway.36kr.com/api/mis/nav/home/nav/rank/hot", Assert.Single(handler.RequestUris), StringComparison.Ordinal);
        Assert.Equal(2, items.Count);
        // itemId 为长整型，必须以原始文本进 URL 不丢精度
        Assert.Equal("https://www.36kr.com/p/7171234567890123456", items[0].Url);
        Assert.Equal("https://img.36kr.com/a.png", items[0].Cover);
        Assert.Equal(456L, items[0].HotValue);
        Assert.Equal(string.Empty, items[1].Url);
        Assert.Null(items[1].HotValue);
    }

    [Fact]
    public async Task Toutiao_ParsesClusterAndHotValue()
    {
        var (items, _) = await RunAsync(
            client => new ToutiaoChannel(client, null),
            _ => StubHttpHandler.Json(new
            {
                data = new object[]
                {
                    new { Title = "头条热点事件", ClusterIdStr = "7301234567890123456", HotValue = "987654", Image = new { url = "https://p3-sign.toutiaoimg.com/x~tplv-a.jpg" } },
                    new { Title = "无图条目", ClusterIdStr = "730999", HotValue = 12345 },
                },
            }));

        Assert.Equal(2, items.Count);
        Assert.Equal("https://www.toutiao.com/trending/7301234567890123456/", items[0].Url);
        Assert.Equal(987654L, items[0].HotValue);
        Assert.Equal("https://p3-sign.toutiaoimg.com/x~tplv-a.jpg", items[0].Cover);
        Assert.Equal(12345L, items[1].HotValue);
        Assert.Null(items[1].Cover);
    }

    [Fact]
    public async Task Juejin_ParsesArticleRankAndViewCount()
    {
        string? referrer = null;
        var (items, handler) = await RunAsync(
            client => new JuejinChannel(client, null),
            request =>
            {
                referrer = request.Headers.Referrer?.ToString();
                return StubHttpHandler.Json(new
                {
                    err_no = 0,
                    err_msg = "success",
                    data = new object[]
                    {
                        new
                        {
                            content = new { content_id = "7694835192107860009", title = "掘金热文标题", brief = "文章摘要", cover_image = "" },
                            content_counter = new { view = 3686, like = 46, hot_rank = 2367 },
                        },
                        new
                        {
                            content = new { content_id = "7694844501786050596", title = "零阅读文章" },
                            content_counter = new { view = 0 },
                        },
                    },
                });
            });

        Assert.StartsWith("https://juejin.cn/", referrer, StringComparison.Ordinal);
        Assert.Equal(2, items.Count);
        Assert.Equal("掘金热文标题", items[0].Title);
        Assert.Equal("https://juejin.cn/post/7694835192107860009", items[0].Url);
        Assert.Equal("文章摘要", items[0].Desc);
        Assert.Equal(3686L, items[0].HotValue);
        Assert.Equal("https://juejin.cn/post/7694844501786050596", items[1].Url);
        Assert.Null(items[1].HotValue);
    }

    [Fact]
    public async Task Baidu_ExtractsSDataAndDrillsNestedBoard()
    {
        const string sData = """{"data":{"cards":[{"content":[{"word":"百度热搜词","desc":"词条描述","query":"百度热搜词","hotScore":"4965021","img":"https://b.bdstatic.com/a.png"},{"content":[{"word":"子榜词条","query":"子榜词条","hotTag":"热"}]}]}]}}""";
        var (items, _) = await RunAsync(
            client => new BaiduChannel(client, null),
            _ => StubHttpHandler.Text($"<html><body><script><!--s-data:{sData}--></script></body></html>"));

        Assert.Equal(2, items.Count);
        Assert.Equal("百度热搜词", items[0].Title);
        Assert.Equal("https://www.baidu.com/s?wd=" + Uri.EscapeDataString("百度热搜词"), items[0].Url);
        Assert.Equal("词条描述", items[0].Desc);
        Assert.Equal(4965021L, items[0].HotValue);
        Assert.Equal("https://b.bdstatic.com/a.png", items[0].Cover);
        // hotTag "热" 无数字 → 热度为空
        Assert.Equal("子榜词条", items[1].Title);
        Assert.Null(items[1].HotValue);
    }

    [Fact]
    public async Task Bilibili_SignsWbiAndParsesRankingList()
    {
        // WBI 混淆表按 img_key+sub_key 拼接后取前 32 位下标重排，桩 key 需各 32 字符防越界
        const string navPayload = """{"wbi_img":{"img_url":"https://i0.hdslb.com/bfs/wbi/aaaabbbbccccddddeeeeffffgggghhhh.png","sub_url":"https://i0.hdslb.com/bfs/wbi/11112222333344445555666677778888.png"}}""";
        var rankingPayload = new
        {
            data = new
            {
                list = new object[]
                {
                    new
                    {
                        bvid = "BV1xx411c7mD",
                        title = "B站热门视频",
                        desc = "视频简介",
                        pic = "http://i0.hdslb.com/bfs/a.jpg",
                        short_link_v2 = "https://b23.tv/BV1xx411c7mD",
                        stat = new { view = 523456 },
                    },
                    new { bvid = "BV1yy411d7mE", title = "无简介视频", stat = new { view = 100 } },
                },
            },
        };

        var (items, handler) = await RunAsync(
            client => new BilibiliChannel(client, null),
            request =>
            {
                var uri = request.RequestUri!.ToString();
                return uri.Contains("/nav", StringComparison.Ordinal)
                    ? StubHttpHandler.Text(navPayload, "application/json")
                    : StubHttpHandler.Json(rankingPayload);
            });

        // 首次运行需 nav 取 WBI key，再请求 ranking/v2（带 w_rid 签名）
        Assert.Contains(handler.RequestUris, uri => uri.Contains("w_rid=", StringComparison.Ordinal));
        Assert.Equal(2, items.Count);
        Assert.Equal("B站热门视频", items[0].Title);
        Assert.Equal("https://b23.tv/BV1xx411c7mD", items[0].Url);
        Assert.Equal("https://i0.hdslb.com/bfs/a.jpg", items[0].Cover);
        Assert.Equal(523456L, items[0].HotValue);
        Assert.Equal("该视频暂无简介", items[1].Desc);
    }

    [Fact]
    public async Task Douyin_PrefetchesCsrfCookieAndParsesWordList()
    {
        string? cookieHeader = null;
        var (items, handler) = await RunAsync(
            client => new DouyinChannel(client, null),
            request =>
            {
                var uri = request.RequestUri!.ToString();
                if (uri.Contains("login_guiding_strategy", StringComparison.Ordinal))
                {
                    var response = StubHttpHandler.Json(new { });
                    response.Headers.Add("Set-Cookie", "passport_csrf_token=stub_token; path=/; domain=douyin.com");
                    return response;
                }

                cookieHeader = string.Join(';', request.Headers.GetValues("Cookie"));
                return StubHttpHandler.Json(new
                {
                    data = new
                    {
                        word_list = new object[]
                        {
                            new { word = "抖音热点词", sentence_id = "7311234567890123456", hot_value = "523456" },
                            new { word = "无链接热点", hot_value = 42 },
                        },
                    },
                });
            });

        Assert.Equal(2, handler.RequestUris.Count);
        Assert.Contains("passport_csrf_token=stub_token", cookieHeader, StringComparison.Ordinal);
        Assert.Equal(2, items.Count);
        Assert.Equal("https://www.douyin.com/hot/7311234567890123456", items[0].Url);
        Assert.Equal(523456L, items[0].HotValue);
        Assert.Equal(42L, items[1].HotValue);
    }

    [Fact]
    public async Task Ithome_ParsesHtmlListAndRewritesArticleUrl()
    {
        const string html = """
            <html><body>
            <div class="rank-box">
              <div class="placeholder">
                <a class="plc-title" href="https://m.ithome.com/html/701456.htm">IT之家热文标题</a>
                <span class="review-num">66</span>
                <img data-original="https://img.ithome.com/a.png">
              </div>
              <div class="placeholder">
                <a class="plc-title" href="https://m.ithome.com/live/701457.htm">IT之家快讯标题</a>
                <span class="review-num">3 条评论</span>
              </div>
            </div>
            </body></html>
            """;
        var (items, _) = await RunAsync(
            client => new IthomeChannel(client, null),
            _ => StubHttpHandler.Text(html));

        Assert.Equal(2, items.Count);
        Assert.Equal("IT之家热文标题", items[0].Title);
        Assert.Equal("https://www.ithome.com/0/701/456.htm", items[0].Url);
        Assert.Equal(66L, items[0].HotValue);
        Assert.Equal("https://img.ithome.com/a.png", items[0].Cover);
        Assert.Equal("https://www.ithome.com/0/701/457.htm", items[1].Url);
        Assert.Equal(3L, items[1].HotValue);
        Assert.Null(items[1].Cover);
    }

    [Fact]
    public async Task Hupu_ParsesTopicThreads()
    {
        var (items, _) = await RunAsync(
            client => new HupuChannel(client, null),
            _ => StubHttpHandler.Json(new
            {
                data = new
                {
                    topicThreads = new object[]
                    {
                        new { title = "步行街热帖", tid = 31415926L, replies = 876 },
                        new { title = "冷帖" },
                    },
                },
            }));

        Assert.Equal(2, items.Count);
        Assert.Equal("https://bbs.hupu.com/31415926.html", items[0].Url);
        Assert.Equal(876L, items[0].HotValue);
        Assert.Equal(string.Empty, items[1].Url);
        Assert.Null(items[1].HotValue);
    }

    [Fact]
    public async Task Huxiu_SplitsMomentTextByBr()
    {
        var (items, _) = await RunAsync(
            client => new HuxiuChannel(client, null),
            _ => StubHttpHandler.Json(new
            {
                data = new
                {
                    moment_list = new
                    {
                        datalist = new object[]
                        {
                            new
                            {
                                content = "虎嗅快讯首句标题。<br>第二段摘要内容。",
                                object_id = 98765432101L,
                                count_info = new { agree_num = "456" },
                            },
                        },
                    },
                },
            }));

        var item = Assert.Single(items);
        Assert.Equal("虎嗅快讯首句标题", item.Title);
        // 仅标题去句号，摘要保留原文
        Assert.Equal("第二段摘要内容。", item.Desc);
        Assert.Equal("https://www.huxiu.com/moment/98765432101.html", item.Url);
        Assert.Equal(456L, item.HotValue);
    }

    [Fact]
    public async Task Netease_PrefersItemUrlAndFallsBackDocid()
    {
        var (items, _) = await RunAsync(
            client => new NeteaseNewsChannel(client, null),
            _ => StubHttpHandler.Json(new
            {
                data = new
                {
                    list = new object[]
                    {
                        new { title = "网易热点一", docid = "ABC123", source = "新浪科技", imgsrc = "https://nimg.ws.126.net/a.png" },
                        new { title = "网易热点二", docid = "DEF456", url = "https://www.163.com/dy/article/DEF456.html", source = "南方都市报" },
                    },
                },
            }));

        Assert.Equal(2, items.Count);
        Assert.Equal("https://www.163.com/dy/article/ABC123.html", items[0].Url);
        Assert.Equal("新浪科技", items[0].Desc);
        Assert.Equal("https://nimg.ws.126.net/a.png", items[0].Cover);
        Assert.Equal("https://www.163.com/dy/article/DEF456.html", items[1].Url);
        Assert.Null(items[1].HotValue);
    }

    [Fact]
    public async Task QqNews_SkipsTipBannerAndParsesHotScore()
    {
        var (items, _) = await RunAsync(
            client => new QqNewsChannel(client, null),
            _ => StubHttpHandler.Json(new
            {
                idlist = new object[]
                {
                    new
                    {
                        newslist = new object[]
                        {
                            new { id = "TIP20250101", title = "横幅提示语" },
                            new { id = "20250102A0ABCDE", title = "腾讯热点", @abstract = "新闻摘要", miniProShareImage = "https://inews.gtimg.com/a.jpg", hotEvent = new { hotScore = "2637459" } },
                        },
                    },
                },
            }));

        var item = Assert.Single(items);
        Assert.Equal("腾讯热点", item.Title);
        Assert.Equal("https://new.qq.com/rain/a/20250102A0ABCDE", item.Url);
        Assert.Equal("新闻摘要", item.Desc);
        Assert.Equal(2637459L, item.HotValue);
    }

    [Fact]
    public async Task SinaNews_StripsJsVarShellAndThousandSeparators()
    {
        const string body = """var data = {"data":[{"title":"新浪头条","id":"1001","url":"https://finance.sina.com.cn/a","media":"新浪科技","top_num":"4,567"},{"title":"新浪第二条","id":"1002","url":"https://finance.sina.com.cn/b","media":"澎湃","top_num":"89"}]};""";
        var (items, _) = await RunAsync(
            client => new SinaNewsChannel(client, null),
            _ => StubHttpHandler.Text(body, "application/json"));

        Assert.Equal(2, items.Count);
        Assert.Equal("新浪头条", items[0].Title);
        Assert.Equal("https://finance.sina.com.cn/a", items[0].Url);
        Assert.Equal("新浪科技", items[0].Desc);
        Assert.Equal(4567L, items[0].HotValue);
        Assert.Equal(89L, items[1].HotValue);
    }

    [Fact]
    public async Task SinaNews_InvalidShell_ThrowsBusinessException()
    {
        var handler = new StubHttpHandler(_ => StubHttpHandler.Text("unexpected body"));
        using var client = new HttpClient(handler);
        var channel = new SinaNewsChannel(client, null);

        await Assert.ThrowsAsync<MoAI.Infra.Exceptions.BusinessException>(() => channel.FetchAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Thepaper_ParsesHotNews()
    {
        var (items, _) = await RunAsync(
            client => new ThepaperChannel(client, null),
            _ => StubHttpHandler.Json(new
            {
                data = new
                {
                    hotNews = new object[]
                    {
                        new { name = "澎湃热点", contId = "7890123", pic = "https://img.thepaper.cn/a.jpg", praiseTimes = "1234" },
                        new { name = "澎湃第二条", contId = "7890124" },
                    },
                },
            }));

        Assert.Equal(2, items.Count);
        Assert.Equal("https://www.thepaper.cn/newsDetail_forward_7890123", items[0].Url);
        Assert.Equal("https://img.thepaper.cn/a.jpg", items[0].Cover);
        Assert.Equal(1234L, items[0].HotValue);
        Assert.Equal("https://www.thepaper.cn/newsDetail_forward_7890124", items[1].Url);
        Assert.Null(items[1].HotValue);
    }

    [Fact]
    public async Task Geekpark_ParsesHomepagePosts()
    {
        var (items, _) = await RunAsync(
            client => new GeekparkChannel(client, null),
            _ => StubHttpHandler.Json(new
            {
                homepage_posts = new object[]
                {
                    new { post = new { id = 5123456L, title = "极客公园热文", @abstract = "文章摘要", cover_url = "https://images.geekpark.net/a.png", views = "12345" } },
                    new { post = new { id = 5123457L, title = "零阅读文章", @abstract = "", views = "0" } },
                },
            }));

        Assert.Equal(2, items.Count);
        Assert.Equal("极客公园热文", items[0].Title);
        Assert.Equal("文章摘要", items[0].Desc);
        Assert.Equal("https://www.geekpark.net/news/5123456", items[0].Url);
        Assert.Equal("https://images.geekpark.net/a.png", items[0].Cover);
        Assert.Equal(12345L, items[0].HotValue);
        Assert.Null(items[1].HotValue);
    }

    private static async Task<(IReadOnlyList<HotBoardEntry> Items, StubHttpHandler Handler)> RunAsync(
        Func<HttpClient, IHotBoardChannel> createChannel,
        Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        var handler = new StubHttpHandler(responder);
        using var client = new HttpClient(handler);
        var channel = createChannel(client);
        var items = await channel.FetchAsync(CancellationToken.None);
        return (items, handler);
    }
}
