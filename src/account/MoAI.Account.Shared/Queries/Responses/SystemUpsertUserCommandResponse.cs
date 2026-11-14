namespace MoAI.Account.Queries.Responses;

/// <summary>
/// 系统级用户写入结果.
/// </summary>
public class SystemUpsertUserCommandResponse
{
    /// <summary>
    /// 用户 id.
    /// </summary>
    public long UserId { get; set; }

    /// <summary>
    /// 是否为新建（false=按 userName 命中已有用户并更新）.
    /// </summary>
    public bool Created { get; set; }

    /// <summary>
    /// 初始密码明文（仅创建且未提供密码时自动生成并返回一次；更新路径恒为空）.
    /// </summary>
    public string? InitialPassword { get; set; }

    /// <summary>
    /// 本次是否完成第三方身份绑定（true=新建绑定或幂等复用已有绑定；未携带 oauth 绑定字段时恒为 false）.
    /// </summary>
    public bool OauthBound { get; set; }
}
