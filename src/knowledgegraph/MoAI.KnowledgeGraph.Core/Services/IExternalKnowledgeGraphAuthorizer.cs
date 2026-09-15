using MoAI.Database.Entities;

namespace MoAI.KnowledgeGraph.Services;

/// <summary>
/// 外部应用 token 的知识图谱授权器：应用 token 携带 TeamId，对团队资源权限等价团队 Admin.
/// </summary>
public interface IExternalKnowledgeGraphAuthorizer
{
    /// <summary>
    /// 校验图谱属于外部调用方团队；不存在或跨团队一律 404（对外不泄露存在性）；write 且接入模式抛 409 只读.
    /// </summary>
    /// <param name="knowledgeGraphId">图谱 id.</param>
    /// <param name="teamId">外部调用方团队 id.</param>
    /// <param name="write">是否写操作.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>返回校验通过的知识图谱.</returns>
    Task<KnowledgeGraphEntity> AuthorizeAsync(long knowledgeGraphId, long teamId, bool write, CancellationToken cancellationToken);
}
