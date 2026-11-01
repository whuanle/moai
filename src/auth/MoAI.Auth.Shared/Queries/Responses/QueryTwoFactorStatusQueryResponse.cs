namespace MoAI.Auth.Queries.Responses;

/// <summary>
/// 两步验证（2FA）状态.
/// </summary>
public class QueryTwoFactorStatusQueryResponse
{
    /// <summary>
    /// 当前用户是否已绑定并启用两步验证.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// 系统是否强制开启两步验证（强制时用户不可关闭）.
    /// </summary>
    public bool Forced { get; set; }
}
