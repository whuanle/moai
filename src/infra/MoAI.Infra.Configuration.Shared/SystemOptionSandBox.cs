using Microsoft.AspNetCore.Mvc.Formatters;
using MoAI.Infra.Models;

namespace MoAI.Infra;

/// <summary>
/// OpenSandBox 沙箱配置.
/// </summary>
public class SystemOptionSandBox
{
    /// <summary>
    /// 沙箱服务地址，如 http://192.168.50.199:18123.
    /// </summary>
    public string Address { get; init; } = string.Empty;

    /// <summary>
    /// 访问密钥（可选；本地部署通常无需鉴权）.
    /// </summary>
    public string ApiKey { get; init; } = string.Empty;

    /// <summary>
    /// 默认沙箱镜像，需为 code-interpreter 镜像以支持代码解释器.
    /// </summary>
    public string Image { get; init; } = "sandbox-registry.cn-zhangjiakou.cr.aliyuncs.com/opensandbox/code-interpreter:v1.1.0";

    /// <summary>
    /// 沙箱默认存活时间（秒），默认 900 秒（15 分钟）.
    /// </summary>
    public int TimeoutSeconds { get; init; } = 900;

    /// <summary>
    /// 续期阈值（秒）：剩余存活时间低于该值时自动续期，默认 300 秒（5 分钟）.
    /// </summary>
    public int RenewThresholdSeconds { get; init; } = 300;
}
