namespace MoAI.App.Queries.Responses;

/// <summary>
/// 重置应用接入密钥结果：新密钥原文一次性返回.
/// </summary>
public class ResetAccessAppSecretCommandResponse
{
    /// <summary>
    /// 新接入密钥原文（仅此一次返回，请妥善保存）.
    /// </summary>
    public string Secret { get; set; } = default!;
}
