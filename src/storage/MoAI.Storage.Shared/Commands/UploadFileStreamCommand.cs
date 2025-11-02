using MediatR;

namespace MoAI.Storage.Commands;

/// <summary>
/// 上传文件流.
/// </summary>
public class UploadFileStreamCommand : IRequest<FileUploadResult>
{
    /// <summary>
    /// 文件流.
    /// </summary>
    public Stream FileStream { get; init; } = Stream.Null;

    /// <summary>
    /// 文件类型.
    /// </summary>
    public string ContentType { get; set; } = default!;

    /// <summary>
    /// 文件大小.
    /// </summary>
    public int FileSize { get; set; } = default!;

    /// <summary>
    /// 文件 MD5.
    /// </summary>
    public string MD5 { get; set; } = default!;

    /// <summary>
    /// 文件路径，即 ObjectKey.
    /// </summary>
    public string ObjectKey { get; set; } = default!;
}
