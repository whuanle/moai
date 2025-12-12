using Microsoft.Extensions.DependencyInjection;
using Microsoft.KernelMemory.AI;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using MoAI.AI.Abstract;
using MoAI.AI.Models;
using MoAI.AiModel.Services;
using MoAI.Infra.Exceptions;

namespace MoAI.AI.ParagraphPreprocess;

/// <summary>
/// 实现 chat 和 查询向量的客户端.
/// </summary>
internal class ParagraphPreprocessAiClient : IParagraphPreprocessAiClient
{
    private readonly IServiceProvider _serviceProvider;

    private readonly AiEndpoint _chat;
    private readonly AiEndpoint _embedding;

    /// <summary>
    /// Initializes a new instance of the <see cref="ParagraphPreprocessAiClient"/> class.
    /// </summary>
    /// <param name="chat"></param>
    /// <param name="embedding"></param>
    /// <param name="serviceProvider"></param>
    public ParagraphPreprocessAiClient(AiEndpoint chat, AiEndpoint embedding, IServiceProvider serviceProvider)
    {
        _chat = chat;
        _embedding = embedding;
        _serviceProvider = serviceProvider;
    }

    public async Task<string> GenerateTextAsync(string prompt)
    {
        var kernelBuilder = Kernel.CreateBuilder();
        var chatCompletionConfigurator = _serviceProvider.GetKeyedService<IChatCompletionConfigurator>(_chat.Provider);
        if (chatCompletionConfigurator == null)
        {
            throw new BusinessException("暂不支持该模型");
        }

        var kernel = chatCompletionConfigurator.Configure(kernelBuilder, _chat).Build();

        var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();

        var executionSettings = new PromptExecutionSettings()
        {
            ModelId = _chat.Name,

            // 手动执行函数
            FunctionChoiceBehavior = FunctionChoiceBehavior.None()
        };

        var response = await chatCompletionService.GetChatMessageContentAsync(prompt, executionSettings);

        var chatCompletion = response.InnerContent as OpenAI.Chat.ChatCompletion;

        if (chatCompletion == null)
        {
            throw new BusinessException("对话异常");
        }

        // todo: 做使用量统计
        return response.Content!;
    }

    public async Task<float> CalculateSimilarityAsync(string text1, string text2)
    {
        // 方式1：调用 Embedding API 计算余弦相似度
        var embedding1 = await GetEmbeddingAsync(text1);
        var embedding2 = await GetEmbeddingAsync(text2);
        return CalculateCosineSimilarity(embedding1, embedding2);
    }

    /// <summary>
    /// 获取文本 Embedding
    /// </summary>
    private async Task<float[]> GetEmbeddingAsync(string text)
    {
        var textEmbeddingGeneration = _serviceProvider.GetRequiredKeyedService<ITextEmbeddingGeneration>(_embedding.Provider);
        var textTokenizer = MoAI.AI.Helpers.TokenizerFactory.GetTokenizerForModel(_embedding.Name);
        ITextEmbeddingGenerator embeddingGenerator = textEmbeddingGeneration.GetTextEmbeddingGenerator(_embedding);

        var embedding = await embeddingGenerator.GenerateEmbeddingAsync(text);

        var vector = embedding.Data;
        return vector.ToArray();
    }

    /// <summary>
    /// 计算余弦相似度
    /// </summary>
    private float CalculateCosineSimilarity(float[] vec1, float[] vec2)
    {
        if (vec1.Length != vec2.Length)
        {
            throw new ArgumentException("向量维度不一致");
        }

        float dotProduct = 0, magnitude1 = 0, magnitude2 = 0;
        for (int i = 0; i < vec1.Length; i++)
        {
            dotProduct += vec1[i] * vec2[i];
            magnitude1 += vec1[i] * vec1[i];
            magnitude2 += vec2[i] * vec2[i];
        }

        return dotProduct / (float)(Math.Sqrt(magnitude1) * Math.Sqrt(magnitude2));
    }
}
