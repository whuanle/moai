namespace MoAI.Gateway.Queries.Responses;

/// <summary>
/// 创建网关 API Key 响应，secret 仅此一次返回，服务端只保存 sha256.
/// </summary>
public class CreateTeamApiKeyCommandResponse
{
    /// <summary>
    /// 密钥 id.
    /// </summary>
    public Guid ApiKeyId { get; set; }

    /// <summary>
    /// 密钥原文，仅创建时返回一次.
    /// </summary>
    public string Secret { get; set; } = string.Empty;

    /// <summary>
    /// 密钥前缀，用于列表展示.
    /// </summary>
    public string KeyPrefix { get; set; } = string.Empty;
}
