using MoAI.KnowledgeGraph.Models;

namespace MoAI.KnowledgeGraph.Services;

/// <summary>
/// 接入图内省缓存：内省结果热缓存（短 TTL）+ 基线快照（长期），新鲜内省时与基线 diff 出 schema 变化.
/// </summary>
public interface IKnowledgeGraphIntrospectionCache
{
    /// <summary>
    /// 获取内省结果：命中缓存直接返回；缓存失效或强制刷新时重新内省，并与基线 diff 后更新基线.
    /// </summary>
    /// <returns>内省结果、相对上次基线的变化（仅新鲜内省时非空）与是否来自缓存.</returns>
    Task<(KnowledgeGraphIntrospection Introspection, KnowledgeGraphIntrospectionDiff? Changes, bool FromCache)> GetAsync(long knowledgeGraphId, string database, bool refresh, CancellationToken cancellationToken);

    /// <summary>
    /// 删除图谱时清理指定库的缓存与基线.
    /// </summary>
    Task RemoveAsync(long knowledgeGraphId, string database, CancellationToken cancellationToken);
}
