namespace MoAI.Infra.Models;

/// <summary>
/// 客户端信息.
/// </summary>
public class ClientInfo
{
    /// <summary>
    /// IP.
    /// </summary>
    public string IP { get; init; } = string.Empty;

    /// <summary>
    /// UserAgent.
    /// </summary>
    public string UserAgent { get; init; } = string.Empty;
}
