namespace MoAI.WeixinWork.Queries.Responses;

/// <summary>
/// 企业微信应用连接列表响应.
/// </summary>
public class QueryWeixinWorkAppsCommandResponse
{
    /// <summary>
    /// 团队 id.
    /// </summary>
    public long TeamId { get; set; }

    /// <summary>
    /// 我在该团队中的角色：0=Member 1=Admin 2=Owner.
    /// </summary>
    public int MyRole { get; set; }

    /// <summary>
    /// 企业微信应用连接集合.
    /// </summary>
    public IReadOnlyList<WeixinWorkAppItem> Items { get; set; } = new List<WeixinWorkAppItem>();
}
