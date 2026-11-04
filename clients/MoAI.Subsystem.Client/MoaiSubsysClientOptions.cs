namespace MoAI.Subsystem.Client;

/// <summary>
/// 子系统协作面客户端配置。对应配置节（默认 <c>MoaiSubsys</c>）：
/// <code>
/// "MoaiSubsys": {
///   "BaseUrl": "https://moai.example.com",
///   "AppId": "my-subsystem",
///   "Secret": "******",
///   "TimeoutSeconds": 100,
///   "RefreshSkewSeconds": 300
/// }
/// </code>
/// </summary>
public class MoaiSubsysClientOptions
{
    /// <summary>MoAI 服务主机根地址，如 <c>https://moai.example.com</c>。不要带 <c>/api</c> 路径（协作面端点以 <c>/api/subsys/**</c> 开头，由客户端自动拼接）。</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>接入标识。子系统为创建时自填的 appid；应用接入为 <c>moai-sa-</c> 前缀自动生成。</summary>
    public string AppId { get; set; } = string.Empty;

    /// <summary>接入密钥。应用接入（<c>moai-sa-</c> 前缀 appid）可留空走 appid 直连；子系统自填 appid 必填。</summary>
    public string Secret { get; set; } = string.Empty;

    /// <summary>HTTP 请求超时（秒）。</summary>
    public int TimeoutSeconds { get; set; } = 100;

    /// <summary>access token 提前刷新窗口（秒）。到期前该窗口内的请求会先刷新 token，默认 300（5 分钟）。机器 token 服务端有效期 2 小时。</summary>
    public int RefreshSkewSeconds { get; set; } = 300;

    /// <summary>是否 appid 直连模式：应用接入（appid 以 moai-sa- 开头）且未配置 Secret 时，直接以 appid 作为 Bearer 凭据，不再走 token 换取。</summary>
    public bool IsAppIdDirect => string.IsNullOrWhiteSpace(Secret);
}
