namespace MoAI.AIChannel.Commands;

/// <summary>
/// SyncAIModelCatalogCommandResponse.
/// </summary>
public class SyncAIModelCatalogCommandResponse
{
    /// <summary>
    /// 目录中的渠道（provider）数量.
    /// </summary>
    public int ProviderCount { get; init; }

    /// <summary>
    /// 目录中的模型总数.
    /// </summary>
    public int ModelCount { get; init; }
}
