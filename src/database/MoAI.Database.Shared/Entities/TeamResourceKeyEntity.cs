using System;
using MoAI.Database.Audits;
using MoAI.Database.Enums;

#pragma warning disable CA1051
#pragma warning disable SA1401
#pragma warning disable SA1600
#pragma warning disable SA1601
#pragma warning disable SA1204
namespace MoAI.Database.Entities;

/// <summary>
/// 团队资源 key：团队内资源（应用 ACP/A2A、知识库、知识图谱）各自的外部访问凭证，
/// key 明文存储等值命中（moai-tr- 前缀），无密钥不可换 token，仅支持 key 直连；
/// 同一（团队, 资源类型, 资源）仅一把 key，重置=同行重新生成 key 值（行 id 不变，直连身份延续）.
/// </summary>
public partial class TeamResourceKeyEntity : IFullAudited
{
    /// <summary>
    /// id.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 归属团队 id.
    /// </summary>
    public int TeamId { get; set; }

    /// <summary>
    /// 资源类型：1=app 团队应用（ACP/A2A）2=wiki 知识库 3=knowledge_graph 知识图谱.
    /// </summary>
    public TeamResourceType ResourceType { get; set; }

    /// <summary>
    /// 资源 id（字符串形态）：app 行=应用 Guid、wiki 行=wikiId、knowledge_graph 行=kgId；一个资源一把 key.
    /// </summary>
    public string ResourceId { get; set; } = default!;

    /// <summary>
    /// key 原文（moai-tr- 前缀，明文存储等值命中）.
    /// </summary>
    public string Key { get; set; } = default!;

    /// <summary>
    /// key 展示前缀（前 12 位，列表回显用）.
    /// </summary>
    public string KeyPrefix { get; set; } = string.Empty;

    /// <summary>
    /// 状态：停用后直连调用立即拒绝.
    /// </summary>
    public TeamResourceKeyStatus Status { get; set; }

    /// <summary>
    /// 创建人.
    /// </summary>
    public long CreateUserId { get; set; }

    /// <summary>
    /// 创建时间.
    /// </summary>
    public DateTimeOffset CreateTime { get; set; }

    /// <summary>
    /// 更新人.
    /// </summary>
    public long UpdateUserId { get; set; }

    /// <summary>
    /// 更新时间.
    /// </summary>
    public DateTimeOffset UpdateTime { get; set; }

    /// <summary>
    /// 软删除.
    /// </summary>
    public long IsDeleted { get; set; }
}
