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
    /// 候选模型列表（只探测不落库，由用户挑选后通过 import 接口导入）.
    /// </summary>
    public List<SyncAIModelCandidateItem> Items { get; init; } = new();
}
