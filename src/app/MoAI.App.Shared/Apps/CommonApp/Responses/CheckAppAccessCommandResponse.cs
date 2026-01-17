namespace MoAI.App.Apps.CommonApp.Responses;

/// <summary>
/// 检查用户是否有权访问应用的响应.
/// </summary>
public class CheckAppAccessCommandResponse
{
    /// <summary>
    /// 是否有权访问.
    /// </summary>
    public bool HasAccess { get; init; }
}
