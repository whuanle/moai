using Microsoft.KernelMemory;
using MoAI.AiModel.Models;

namespace MoAI.AiModel.Services;

/// <summary>
/// 向量化构建接口.
/// </summary>
public interface ITextEmbeddingGeneration
{
    /// <summary>
    /// 配置.
    /// </summary>
    /// <param name="kernelMemoryBuilder"></param>
    /// <param name="endpoint"></param>
    /// <param name="wikiConfig"></param>
    /// <returns></returns>
    IKernelMemoryBuilder Configure(IKernelMemoryBuilder kernelMemoryBuilder, AiEndpoint endpoint, EmbeddingConfig wikiConfig);
}
