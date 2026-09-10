using MoAI.Settings.Models;

namespace MoAI.Settings.Services;

/// <summary>
/// 知识图谱配置读取服务，供知识库等业务模块感知 Neo4j 知识图谱能力.
/// </summary>
public interface IKnowledgeGraphSettingsService
{
    /// <summary>
    /// 获取知识图谱配置（未开启时 Enabled 为 false，连接信息为空字符串）.
    /// </summary>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>返回 <see cref="Neo4jKnowledgeGraphSettings"/>.</returns>
    Task<Neo4jKnowledgeGraphSettings> GetAsync(CancellationToken cancellationToken);
}
