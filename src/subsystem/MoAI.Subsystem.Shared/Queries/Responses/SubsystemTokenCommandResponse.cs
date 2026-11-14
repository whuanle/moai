namespace MoAI.Subsystem.Queries.Responses;

/// <summary>
/// 子系统接入 token 换取结果.
/// </summary>
public class SubsystemTokenCommandResponse
{
    /// <summary>
    /// 接入 token（JWT），机器面请求以 Bearer 方式携带.
    /// </summary>
    public string AccessToken { get; init; } = default!;

    /// <summary>
    /// 刷新 token（JWT），用于过期前轮换续期，免重发 appid+密钥.
    /// </summary>
    public string RefreshToken { get; init; } = default!;

    /// <summary>
    /// 有效期（秒）.
    /// </summary>
    public int ExpiresIn { get; init; }

    /// <summary>
    /// token 类型，固定 Bearer.
    /// </summary>
    public string TokenType { get; init; } = "Bearer";
}
