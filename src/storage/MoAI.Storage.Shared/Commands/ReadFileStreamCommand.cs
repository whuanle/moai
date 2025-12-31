using MediatR;

namespace MoAI.Storage.Commands;

/// <summary>
/// 读取文件流.
/// </summary>
public class ReadFileStreamCommand : IRequest<ReadFileStreamCommandResponse>
{
    /// <summary>
    /// 文件 key.
    /// </summary>
    public string ObjectKey { get; init; }
}

public class ReadFileStreamCommandResponse
{
    public Stream FileStream { get; set; } = default!;
}
