namespace MoAI.Storage.Commands;

/// <summary>
/// 删除文件的输入参数.
/// </summary>
public class DeleteFileCommand
{
    /// <summary>
    /// 文件 id 列表.
    /// </summary>
    public IReadOnlyCollection<int> FileIds { get; init; } = Array.Empty<int>();
}
