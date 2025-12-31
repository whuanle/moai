namespace MoAI.App.AIAssistant.Commands.Responses;

/// <summary>
/// 上传文件结果.
/// </summary>
public class PreUploadChatFileDocumentCommandResponse
{
    /// <summary>
    /// 文件已存在,如果文件已存在则直接使用 FileId，无需再次上传.
    /// </summary>
    public bool IsExist { get; set; }

    /// <summary>
    /// 文件ID.
    /// </summary>
    public int FileId { get; set; }

    /// <summary>
    /// 文件ID.
    /// </summary>
    public string ObjectKey { get; set; }

    /// <summary>
    /// 预签名上传地址，当 IsExist = true 时字段为空.
    /// </summary>
    public Uri? UploadUrl { get; set; } = default!;

    /// <summary>
    /// 签名过期时间，当 IsExist = true 时字段为空.
    /// </summary>
    public DateTimeOffset? Expiration { get; set; } = default!;
}