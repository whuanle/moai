using Microsoft.KernelMemory.AI;
using Microsoft.KernelMemory.MemoryStorage;
using Microsoft.SemanticKernel;
using MoAI.AI.Models;

namespace MoAI.AI.ChatCompletion;

/// <summary>
/// Ai 客户端构建器.
/// </summary>
public interface IAiClientBuilder : IDisposable
{
    /// <summary>
    /// 配置 sk 客户端.
    /// </summary>
    /// <param name="kernelBuilder"></param>
    /// <param name="endpoint"></param>
    /// <returns></returns>
    IKernelBuilder Configure(IKernelBuilder kernelBuilder, AiEndpoint endpoint);

    /// <summary>
    /// 配置 km 客户端.
    /// </summary>
    /// <param name="textEmbeddingGenerator"></param>
    /// <param name="memoryDbType"></param>
    /// <returns></returns>
    IMemoryDb CreateMemoryDb(ITextEmbeddingGenerator textEmbeddingGenerator, MemoryDbType memoryDbType);

    /// <summary>
    /// 配置 km 客户端.
    /// </summary>
    /// <param name="endpoint"></param>
    /// <param name="memoryDbType"></param>
    /// <param name="embeddingDimensions"></param>
    /// <returns></returns>
    IMemoryDb CreateMemoryDb(AiEndpoint endpoint, MemoryDbType memoryDbType, int embeddingDimensions);

    /// <summary>
    /// 创建 Ai 模型向量化客户端.
    /// </summary>
    /// <param name="endpoint"></param>
    /// <param name="embeddingDimensions">知识库向量的维度</param>
    /// <returns></returns>
    ITextEmbeddingGenerator CreateTextEmbeddingGenerator(AiEndpoint endpoint, int embeddingDimensions);
}