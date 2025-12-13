using Microsoft.KernelMemory;
using Microsoft.KernelMemory.AI;
using MoAI.AI.Models;
using MoAI.AiModel.Models;

namespace MoAI.AiModel.Services;

// todo： 准备废弃

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

    /// <summary>
    /// 获取文本向量化生成器.
    /// </summary>
    /// <param name="endpoint"></param>
    /// <param name="wikiConfig"></param>
    /// <returns></returns>
    ITextEmbeddingGenerator GetTextEmbeddingGenerator(AiEndpoint endpoint);
}
