using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Storage.Commands.Models;

public class DownloadFileCommandResponse
{
    /// <summary>
    /// 本地文件路径.
    /// </summary>
    public string LocalFilePath { get; set; } = string.Empty;

    /// <summary>
    /// 文件路径.
    /// </summary>
    public string ObjectKey { get; set; } = default!;

    /// <summary>
    /// 文件扩展名.
    /// </summary>
    public string FileExtension { get; set; } = default!;

    /// <summary>
    /// md5.
    /// </summary>
    public string FileMd5 { get; set; } = default!;

    /// <summary>
    /// 文件大小.
    /// </summary>
    public int FileSize { get; set; }

    /// <summary>
    /// 文件类型.
    /// </summary>
    public string ContentType { get; set; } = default!;
}