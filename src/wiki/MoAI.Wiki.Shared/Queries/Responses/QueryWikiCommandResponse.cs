using System;

namespace MoAI.Wiki.Queries.Responses;

/// <summary>
/// 知识库详情响应.
/// wiki 只承载向量模型与维度；元数据模型与切片参数由每次触发文档向量化时按需传入.
/// </summary>
public class QueryWikiCommandResponse
{
    /// <summary>
    /// 知识库 id.
    /// </summary>
    public long WikiId { get; set; }

    /// <summary>
    /// 所属团队 id.
    /// </summary>
    public long TeamId { get; set; }

    /// <summary>
    /// 知识库名称.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// 知识库简介.
    /// </summary>
    public string Description { get; set; } = default!;

    /// <summary>
    /// 是否公开，公开后所有人都可以使用（只读），但非团队成员不能进入操作.
    /// </summary>
    public bool IsPublic { get; set; }

    /// <summary>
    /// 我在所属团队中的角色：0=Owner 1=Admin 2=Member；非成员访问公开库时为 0.
    /// </summary>
    public int MyRole { get; set; }

    /// <summary>
    /// 知识库头像的 ObjectKey（空串=未设置）.
    /// </summary>
    public string AvatarPath { get; set; } = default!;

    /// <summary>
    /// 创建时间.
    /// </summary>
    public DateTimeOffset CreateTime { get; set; }

    /// <summary>
    /// 向量化模型 id.
    /// </summary>
    public Guid EmbeddingModelId { get; set; }

    /// <summary>
    /// 向量化模型名称.
    /// </summary>
    public string EmbeddingModelName { get; set; } = default!;

    /// <summary>
    /// 知识库向量维度（1-2000）.
    /// </summary>
    public int EmbeddingDimensions { get; set; }

    /// <summary>
    /// 向量化模型与维度配置是否已被锁定（已有文档被向量化）.
    /// </summary>
    public bool IsLock { get; set; }

    /// <summary>
    /// 重排序模型 id，为空表示未绑定（可选配置）.
    /// </summary>
    public Guid? RerankModelId { get; set; }

    /// <summary>
    /// 重排序模型名称，未绑定时为空串.
    /// </summary>
    public string RerankModelName { get; set; } = default!;
}
