using MoAI.Database.Enums;
using MoAI.Infra.Models;

namespace MoAI.App.Models;

/// <summary>
/// 外部 token 上下文：从外部 token claims 解析出的调用方身份与归属团队（团队级资源访问）.
/// </summary>
public class ExternalTokenContext
{
    /// <summary>
    /// 主体类型：应用接入 = <see cref="UserType.ExternalApp"/>，外部用户 = <see cref="UserType.External"/>，
    /// 访问用户（内部应用访问点身份穿透）= <see cref="UserType.Normal"/>.
    /// </summary>
    public UserType SubjectType { get; init; }

    /// <summary>
    /// token sub 原文：应用 token 为接入 id，用户 token 为外部用户 id，访问用户 token 为站内用户 id.
    /// </summary>
    public string SubjectId { get; init; } = default!;

    /// <summary>
    /// 外部用户 id（仅用户 token 有值，其余为 0）.
    /// </summary>
    public long ExternalId { get; init; }

    /// <summary>
    /// 站内用户 id（仅访问用户 token 有值，其余为 0）.
    /// </summary>
    public long UserId { get; init; }

    /// <summary>
    /// 归属团队 id.
    /// </summary>
    public long TeamId { get; init; }

    /// <summary>
    /// 来源应用接入 id，匿名 token 为 null.
    /// </summary>
    public Guid? AccessAppId { get; init; }

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
    /// 是否为 key 直连认证：请求头直接携带 key（moai-ac- / moai-tr-），
    /// 而非换取的外部 token；会话面端点据此为 key 直连解析直连会话身份.
    /// </summary>
    public bool IsKeyDirect { get; init; }

    /// <summary>
    /// 是否为团队资源 key（moai-tr-）直连：仅能访问绑定资源的端点（见 <see cref="ResourceKeyType"/>/<see cref="ResourceKeyId"/>），
    /// 应用接入 key（moai-ac-）与外部 token 均为 false（团队级全量访问 /api/teamres）.
    /// </summary>
    public bool IsResourceKey { get; init; }

    /// <summary>
    /// 资源 key 的资源类型（仅 <see cref="IsResourceKey"/> 时有值）.
    /// </summary>
    public TeamResourceType ResourceKeyType { get; init; }

    /// <summary>
    /// 资源 key 绑定的资源 id（字符串形态）：app 行=应用 Guid、wiki 行=wikiId、knowledge_graph 行=kgId.
    /// </summary>
    public string ResourceKeyId { get; init; } = string.Empty;
}
