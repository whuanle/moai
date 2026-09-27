using MoAI.Database.Enums;
using MoAI.Infra.Models;

namespace MoAI.App.Models;

/// <summary>
/// 外部 token 上下文：从外部 token claims 解析出的调用方身份与归属团队（团队级资源访问）.
/// </summary>
public class ExternalTokenContext
{
    /// <summary>
    /// 主体类型：应用接入 = <see cref="UserType.ExternalApp"/>，外部用户 = <see cref="UserType.External"/>.
    /// </summary>
    public UserType SubjectType { get; init; }

    /// <summary>
    /// token sub 原文：应用 token 为接入 id，用户 token 为外部用户 id.
    /// </summary>
    public string SubjectId { get; init; } = default!;

    /// <summary>
    /// 外部用户 id（仅用户 token 有值，其余为 0）.
    /// </summary>
    public long ExternalId { get; init; }

    /// <summary>
    /// 归属团队 id.
    /// </summary>
    public long TeamId { get; init; }

    /// <summary>
    /// 来源应用接入 id，匿名 token 为 null.
    /// </summary>
    public Guid? AccessAppId { get; init; }

    /// <summary>
    /// 功能范围；旧格式 token（无 scope 声明）默认全量知识库范围，保持向后兼容.
    /// </summary>
    public TeamApiKeyScopes Scopes { get; init; } = DefaultScopes;

    /// <summary>
    /// 旧格式 token 的兼容范围：知识库读+写+MCP 预留+知识图谱读+写（旧 token 时代外部资源未分范围）.
    /// </summary>
    public const TeamApiKeyScopes DefaultScopes = TeamApiKeyScopeCodes.ExternalDimensions;

    /// <summary>
    /// 当前绑定应用 id（用户 token），应用 token 为 null.
    /// </summary>
    public Guid? AppId { get; init; }

    /// <summary>
    /// 外部身份标识（用户 token），应用 token 为 null.
    /// </summary>
    public string? ExternalUserId { get; init; }

    /// <summary>
    /// 外部用户显示名（用户 token）.
    /// </summary>
    public string? Nickname { get; init; }

    /// <summary>
    /// 是否为 key 直连认证：请求头直接携带应用接入 key（moai-ac-），
    /// 而非换取的外部 token；会话面端点据此为应用接入 key 解析直连会话身份.
    /// </summary>
    public bool IsKeyDirect { get; init; }
}
