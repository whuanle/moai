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
}
