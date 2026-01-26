namespace MoAI.App.Manager.ExternalApi.Models;

/// <summary>
/// 创建系统接入响应.
/// </summary>
public class CreateExternalAppCommandResponse
{
    /// <summary>
    /// 应用id.
    /// </summary>
    public Guid AppId { get; set; }

    /// <summary>
    /// 应用密钥.
    /// </summary>
    public string Key { get; set; } = string.Empty;
}
