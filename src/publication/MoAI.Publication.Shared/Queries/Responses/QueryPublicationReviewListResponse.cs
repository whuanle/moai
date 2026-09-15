using MoAI.Infra.Models;

namespace MoAI.Publication.Queries.Responses;

/// <summary>
/// 上架审核列表响应.
/// </summary>
public class QueryPublicationReviewListResponse
{
    /// <summary>
    /// 上架审核记录.
    /// </summary>
    public IReadOnlyList<PublicationReviewItem> Items { get; set; } = [];
}
