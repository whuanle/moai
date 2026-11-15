using System.Diagnostics;

namespace MoAI.Infra;

/// <summary>
/// 系统常用信息.
/// </summary>
public static class AppConst
{
    /// <summary>
    /// 程序根路径.
    /// </summary>
    public static readonly string AppPath = AppDomain.CurrentDomain.SetupInformation.ApplicationBase!;

    /// <summary>
    /// 配置文件路径.
    /// </summary>
    public static readonly string ConfigsPath = Path.Combine(AppPath, "configs");

    /// <summary>
    /// 配置文件模板路径.
    /// </summary>
    public static readonly string ConfigsTemplate = Path.Combine(AppPath, "configs_template");

    /// <summary>
    /// RSA.
    /// </summary>
    public static readonly string PrivateRSA = Path.Combine(ConfigsPath, "rsa_private.key");

    /// <summary>
    /// 源.
    /// </summary>
    public static readonly ActivitySource ActivitySource = new ActivitySource("MoAI");

    /// <summary>
    /// AI 应用调试埋点事件源名（系统设置 Debug=true 时，AI 侧 UseOpenTelemetry 与可观察性 AddSource 必须配对使用）.
    /// </summary>
    public const string AiTraceSourceName = "AAA";

    /// <summary>
    /// 用户鉴权版本 token 声明：签发时写入用户当前 <c>user.auth_version</c>，每次请求与分布式安全态比对，
    /// 不一致即视为会话已吊销（禁用/启用/强制下线/改密后立即失效）.
    /// </summary>
    public const string AuthVersionClaim = "auth_version";
}
