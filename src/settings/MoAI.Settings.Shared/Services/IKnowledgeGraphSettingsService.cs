using MoAI.Settings.Models;

namespace MoAI.Settings.Services;

/// <summary>
/// 知识图谱图数据库配置读取服务，供业务模块感知图数据库能力.
/// </summary>
public interface IKnowledgeGraphSettingsService
{
    /// <summary>
    /// 获取图数据库配置（未开启时 Enabled 为 false，连接信息为空字符串）.
    /// </summary>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>返回 <see cref="KnowledgeGraphStoreSettings"/>.</returns>
    Task<KnowledgeGraphStoreSettings> GetAsync(CancellationToken cancellationToken);
}
