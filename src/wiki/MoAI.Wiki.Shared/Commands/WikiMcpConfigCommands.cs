using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.Database.Enums;
using MoAI.Infra.Models;
using MoAI.Infra.Services;

namespace MoAI.Wiki.Commands;

/// <summary>
/// 知识库 MCP 接入配置响应：开关 + 该库的团队资源 key（开启过 MCP 才有；未生成时 key 相关字段为 null）.
/// </summary>
public class WikiMcpConfigResponse
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

    /// <summary>
    /// key 状态：enabled/disabled（未生成时 null）.
    /// </summary>
    public TeamResourceKeyStatus? Status { get; set; }
}

/// <summary>
/// 更新知识库 MCP 接入开关：开启时自动生成/复用该库唯一的团队资源 key（查库无则写、有则复用），关闭后端点按开关拒绝（key 保留）；需要团队 Admin 及以上角色.
/// </summary>
public class UpdateWikiMcpConfigCommand : IRequest<WikiMcpConfigResponse>, IUserIdContext, IModelValidator<UpdateWikiMcpConfigCommand>
{
    /// <summary>
    /// 知识库 id，由 Controller 从路由参数回填.
    /// </summary>
    public long WikiId { get; init; }

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
    public static void Validate(AbstractValidator<UpdateWikiMcpConfigCommand> validate)
    {
        // WikiId 由 Controller 从路由参数回填，不做校验.
    }
}

/// <summary>
/// 查询知识库 MCP 接入配置（开关 + 该库的团队资源 key），需要团队 Admin 及以上角色.
/// </summary>
public class QueryWikiMcpConfigCommand : IRequest<WikiMcpConfigResponse>, IUserIdContext, IModelValidator<QueryWikiMcpConfigCommand>
{
    /// <summary>
    /// 知识库 id，由 Controller 从路由参数回填.
    /// </summary>
    public long WikiId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryWikiMcpConfigCommand> validate)
    {
        // WikiId 由 Controller 从路由参数回填，不做校验.
    }
}
