namespace MoAI.Storage.Models;

/// <summary>
/// 完成文件上传响应.
/// </summary>
public class CompleteFileUploadCommandResponse
{
    /// <summary>
    /// 文件 ID.
    /// </summary>
    public long FileId { get; init; }

    /// <summary>
    /// 文件 ObjectKey.
    /// </summary>
    public string ObjectKey { get; init; } = default!;

    /// <summary>
    /// 文件公开访问地址（公开文件返回 /static 地址，私有文件为空）.
    /// </summary>
    public string? AccessUrl { get; init; }
}
