using MoAI.Settings.Models;

namespace MoAI.Settings.Services;

/// <summary>
/// 沙箱上限设置读取服务，供业务模块感知每个应用的沙箱资源约束.
/// </summary>
public interface ISandboxSettingsService
{
    /// <summary>
    /// 获取沙箱资源上限（存活时间 / CPU / 内存），值非法或缺失时回退内置默认值.
    /// </summary>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>返回上限（含解析后的毫核数与字节数）.</returns>
    Task<SandboxLimitsSettings> GetLimitsAsync(CancellationToken cancellationToken);
}
