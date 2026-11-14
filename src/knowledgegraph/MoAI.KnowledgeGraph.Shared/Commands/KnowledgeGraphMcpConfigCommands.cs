using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Infra.Services;

namespace MoAI.KnowledgeGraph.Commands;

/// <summary>
/// 知识图谱 MCP 接入配置响应：开关 + 该图的团队资源 key（开启过 MCP 才有；未生成时 key 相关字段为 null）.
/// </summary>
public class KnowledgeGraphMcpConfigResponse
{
    /// <summary>
    /// 是否开启 MCP 接入.
    /// </summary>
    public bool IsMcpEnabled { get; set; }

    /// <summary>
    /// 资源 key 行 id（未生成时 null）.
    /// </summary>
    public Guid? TeamResourceKeyId { get; set; }

    /// <summary>
    /// key 原文（moai-tr- 前缀，未生成时 null）.
    /// </summary>
    public string? Key { get; set; }

    /// <summary>
    /// key 展示前缀（未生成时 null）.
    /// </summary>
    public string? KeyPrefix { get; set; }
}

/// <summary>
/// 更新知识图谱 MCP 接入开关（仅托管图）：开启时自动生成/复用该图唯一的团队资源 key（查库无则写、有则复用），关闭后端点按开关拒绝（key 保留）；需要团队 Admin 及以上角色.
/// </summary>
public class UpdateKnowledgeGraphMcpConfigCommand : IRequest<KnowledgeGraphMcpConfigResponse>, IUserIdContext, IModelValidator<UpdateKnowledgeGraphMcpConfigCommand>
{
    /// <summary>
    /// 图谱 id，由 Controller 从路由参数回填.
    /// </summary>
    public long KnowledgeGraphId { get; init; }

    /// <summary>
    /// 是否开启 MCP 接入.
    /// </summary>
    public bool IsMcpEnabled { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<UpdateKnowledgeGraphMcpConfigCommand> validate)
    {
        // KnowledgeGraphId 由 Controller 从路由参数回填，不做校验.
    }
}

/// <summary>
/// 查询知识图谱 MCP 接入配置（开关 + 该图的团队资源 key），需要团队 Admin 及以上角色.
/// </summary>
public class QueryKnowledgeGraphMcpConfigCommand : IRequest<KnowledgeGraphMcpConfigResponse>, IUserIdContext, IModelValidator<QueryKnowledgeGraphMcpConfigCommand>
{
    /// <summary>
    /// 图谱 id，由 Controller 从路由参数回填.
    /// </summary>
    public long KnowledgeGraphId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryKnowledgeGraphMcpConfigCommand> validate)
    {
        // KnowledgeGraphId 由 Controller 从路由参数回填，不做校验.
    }
}
