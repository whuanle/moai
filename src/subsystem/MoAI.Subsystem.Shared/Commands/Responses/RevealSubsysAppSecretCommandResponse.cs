namespace MoAI.Subsystem.Commands.Responses;

/// <summary>
/// 回显密钥结果：原文以请求方信封 key 的 AES-256-GCM 密文返回，前端解密后展示/复制.
/// </summary>
public class RevealSubsysAppSecretCommandResponse
{
    /// <summary>
    /// 密钥展示掩码（前 4 + **** + 后 4，便于核对）.
    /// </summary>
    public string Mask { get; init; } = string.Empty;

    /// <summary>
    /// GCM 随机向量（base64，12 字节）.
    /// </summary>
    public string Iv { get; init; } = string.Empty;

    /// <summary>
    /// AES-256-GCM 密文+tag（base64，用请求信封 key 解密）.
    /// </summary>
    public string Data { get; init; } = string.Empty;
}
