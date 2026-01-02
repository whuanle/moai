namespace MoAI.App.Commands.Responses;

/// <summary>
/// 重置系统接入密钥响应.
/// </summary>
public class ResetExternalAppKeyCommandResponse
{
    /// <summary>
    /// 新的应用密钥.
    /// </summary>
    public string Key { get; set; } = string.Empty;
}
