namespace MoAI.Auth.Queries.Responses;

/// <summary>
/// 图形验证码生成结果.
/// </summary>
public class QueryCaptchaNewQueryResponse
{
    /// <summary>
    /// 验证码 id，提交时随 captchaCode 一起传回.
    /// </summary>
    public string CaptchaId { get; set; } = default!;

    /// <summary>
    /// 验证码图片内容（base64 编码，不含 data: 前缀）.
    /// </summary>
    public string Image { get; set; } = default!;

    /// <summary>
    /// 图片 MIME 类型（image/svg+xml）.
    /// </summary>
    public string ImageMimeType { get; set; } = default!;

    /// <summary>
    /// 有效期（秒）.
    /// </summary>
    public int ExpiresInSeconds { get; set; }
}
