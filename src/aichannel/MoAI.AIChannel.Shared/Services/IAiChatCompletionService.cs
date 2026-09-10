using System.Threading;
using System.Threading.Tasks;
using MoAI.Database.Entities;

namespace MoAI.AIChannel.Services;

/// <summary>
/// 一次性对话服务：按渠道协议处理厂商差异化请求选项.
/// </summary>
public interface IAiChatCompletionService
{
    /// <summary>
    /// 发送一次性文本问题并返回模型文本响应.
    /// </summary>
    /// <param name="model">模型.</param>
    /// <param name="channel">渠道.</param>
    /// <param name="prompt">提示词.</param>
    /// <param name="options">请求选项.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>模型文本响应.</returns>
    Task<string> CompleteTextAsync(AiModelEntity model, AiChannelEntity channel, string prompt, AiChatCompletionOptions options, CancellationToken cancellationToken = default);
}

/// <summary>
/// 一次性对话请求选项.
/// </summary>
public sealed class AiChatCompletionOptions
{
    /// <summary>
    /// 采样温度.
    /// </summary>
    public float? Temperature { get; init; }

    /// <summary>
    /// 最大输出 token 数.
    /// </summary>
    public int? MaxOutputTokens { get; init; }

    /// <summary>
    /// 是否要求模型关闭深度思考/思维链.
    /// </summary>
    public bool DisableThinking { get; init; }

    /// <summary>
    /// 是否偏好 JSON 文本响应.
    /// </summary>
    public bool PreferJsonResponse { get; init; }
}