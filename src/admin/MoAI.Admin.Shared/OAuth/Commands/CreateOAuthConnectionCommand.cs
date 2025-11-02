using MediatR;
using MoAI.Infra.Models;
using MoAI.Login.Models;

namespace MoAI.Login.Commands;

/// <summary>
/// 创建 OAuth2.0 连接配置.
/// </summary>
public class CreateOAuthConnectionCommand : IRequest<EmptyCommandResponse>
{
    /// <summary>
    /// 认证名称.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// 提供商.
    /// </summary>
    public OAuthPrivider Provider { get; init; } = default!;

    /// <summary>
    /// 应用key.
    /// </summary>
    public string Key { get; init; } = default!;

    /// <summary>
    /// 密钥.
    /// </summary>
    public string Secret { get; init; } = default!;

    /// <summary>
    /// 图标地址.
    /// </summary>
    public string IconUrl { get; init; } = default!;

    /// <summary>
    /// 发现端口.
    /// </summary>
    public Uri WellKnown { get; init; } = default!;
}