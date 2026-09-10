using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;
using MoAI.Database.Entities;

namespace MoAI.AIChannel.Services;

/// <summary>
/// 向量化生成器提供者：根据渠道与模型，构建不同协议下的 <see cref="IEmbeddingGenerator{TInput,TEmbedding}"/>.
/// </summary>
public interface IEmbeddingGeneratorProvider
{
    /// <summary>
    /// 构建向量生成器.
    /// </summary>
    /// <param name="model">模型.</param>
    /// <param name="channel">渠道.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>返回向量生成器.</returns>
    Task<IEmbeddingGenerator<string, Embedding<float>>> GetEmbeddingGeneratorAsync(AiModelEntity model, AiChannelEntity channel, CancellationToken cancellationToken = default);
}

/// <summary>
/// 对话客户端提供者：根据渠道与模型，构建不同协议下的 <see cref="IChatClient"/>.
/// </summary>
public interface IChatClientProvider
{
    /// <summary>
    /// 构建对话客户端.
    /// </summary>
    /// <param name="model">模型.</param>
    /// <param name="channel">渠道.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>返回对话客户端.</returns>
    Task<IChatClient> GetChatClientAsync(AiModelEntity model, AiChannelEntity channel, CancellationToken cancellationToken = default);
}
