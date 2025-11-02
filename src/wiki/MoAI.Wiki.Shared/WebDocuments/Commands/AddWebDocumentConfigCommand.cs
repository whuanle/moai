using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Wiki.WebDocuments.Commands;

/// <summary>
/// 添加一个网页爬取配置.
/// </summary>
public class AddWebDocumentConfigCommand : IRequest<SimpleInt>
{
    /// <summary>
    /// 知识库 id.
    /// </summary>
    public int WikiId { get; init; }

    /// <summary>
    /// 标题.
    /// </summary>
    public string Title { get; set; } = default!;

    /// <summary>
    /// 页面地址.
    /// </summary>
    public Uri Address { get; set; } = default!;

    /// <summary>
    /// 限制自动爬取的网页都在该路径之下,limit_address跟address必须具有相同域名.
    /// </summary>
    public Uri? LimitAddress { get; set; }

    /// <summary>
    /// 最大抓取数量.
    /// </summary>
    public int LimitMaxCount { get; set; }

    /// <summary>
    /// 是否抓取其它页面.
    /// </summary>
    public bool IsCrawlOther { get; set; }

    /// <summary>
    /// 是否自动向量化.
    /// </summary>
    public bool IsAutoEmbedding { get; set; }

    /// <summary>
    /// 等待js加载完成.
    /// </summary>
    public bool IsWaitJs { get; set; }
}
