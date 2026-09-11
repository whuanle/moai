using MoAI.Database.Entities;
using MoAI.Database.Enums;

namespace MoAI.KnowledgeGraph.Services;

/// <summary>
/// 知识图谱权限判定.
/// </summary>
public interface IKnowledgeGraphAuthorizer
{
    /// <summary>
    /// 校验当前用户对团队的访问权限，adminOnly 时要求 Admin+.
    /// </summary>
    Task<TeamRole> RequireTeamRoleAsync(long teamId, bool adminOnly, CancellationToken cancellationToken);

    /// <summary>
    /// 校验当前用户对图谱的访问权限，返回图谱与角色.
    /// </summary>
    Task<(KnowledgeGraphEntity Graph, TeamRole Role)> AuthorizeAsync(long KnowledgeGraphId, bool adminOnly, CancellationToken cancellationToken);

    /// <summary>
    /// 校验对“可写（托管）图谱”的访问；外部接入图谱抛 409.
    /// </summary>
    Task<(KnowledgeGraphEntity Graph, TeamRole Role)> AuthorizeManagedAsync(long KnowledgeGraphId, bool adminOnly, CancellationToken cancellationToken);
}
