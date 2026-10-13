using MoAI.Settings.Models;

namespace MoAI.Settings.Services;

/// <summary>
/// Agent 运行时默认参数读取服务，供 AI 模块感知系统级默认值（超级管理员在系统设置页调整）.
/// </summary>
public interface IAgentSettingsService
{
    /// <summary>
    /// 获取上下文压缩默认参数；存储值缺失或非法时回退内置默认值.
    /// </summary>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>返回压缩默认参数.</returns>
    Task<AgentCompactionDefaultsSettings> GetAgentCompactionDefaultsAsync(CancellationToken cancellationToken);
}
