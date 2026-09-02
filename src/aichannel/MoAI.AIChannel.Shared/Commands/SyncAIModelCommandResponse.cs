namespace MoAI.AIChannel.Commands;

/// <summary>
/// SyncAIModelCommandResponse.
/// </summary>
public class SyncAIModelCommandResponse
{
    /// <summary>
    /// 供应商返回的模型总数.
    /// </summary>
    public int Total { get; init; }

    /// <summary>
    /// 本次新增数量.
    /// </summary>
    public int Added { get; init; }

    /// <summary>
    /// 已存在跳过数量.
    /// </summary>
    public int Skipped { get; init; }
}
