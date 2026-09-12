using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;
using MoAI.AIChannel.Services;
using MoAI.Database.Enums;

namespace MoAI.AI.Services;

/// <summary>
/// 捕获模型用量并写入模型使用计数与会话热态.
/// </summary>
public sealed class UsageCapturingChatClient : DelegatingChatClient
{
    private readonly IAiModelUsageCounter _usageCounter;
    private readonly AppChatHotStore _hotStore;
    private readonly Guid _modelId;
    private readonly int _teamId;
    private readonly long _userId;
    private readonly Guid _appId;
    private readonly Guid _sessionId;

    /// <summary>
    /// Initializes a new instance of the <see cref="UsageCapturingChatClient"/> class.
    /// </summary>
    /// <param name="innerClient">内层对话客户端.</param>
    /// <param name="usageCounter">模型使用计数器.</param>
    /// <param name="hotStore">会话热态存储.</param>
    /// <param name="modelId">模型 id.</param>
    /// <param name="teamId">团队 id.</param>
    /// <param name="userId">用户 id.</param>
    /// <param name="appId">应用 id.</param>
    /// <param name="sessionId">会话 id.</param>
    public UsageCapturingChatClient(
        IChatClient innerClient,
        IAiModelUsageCounter usageCounter,
        AppChatHotStore hotStore,
        Guid modelId,
        int teamId,
        long userId,
        Guid appId,
        Guid sessionId)
        : base(innerClient)
    {
        _usageCounter = usageCounter;
        _hotStore = hotStore;
        _modelId = modelId;
        _teamId = teamId;
        _userId = userId;
        _appId = appId;
        _sessionId = sessionId;
    }

    /// <inheritdoc/>
    public override async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        var response = await base.GetResponseAsync(messages, options, cancellationToken).ConfigureAwait(false);
        var usage = response.Usage;
        await RecordAsync(
            (int)(usage?.InputTokenCount ?? 0),
            (int)(usage?.OutputTokenCount ?? 0),
            cancellationToken).ConfigureAwait(false);
        return response;
    }

    /// <inheritdoc/>
    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var prompt = 0;
        var completion = 0;

        await foreach (var update in base.GetStreamingResponseAsync(messages, options, cancellationToken).ConfigureAwait(false))
        {
            if (update.Contents is not null)
            {
                foreach (var usage in update.Contents.OfType<UsageContent>())
                {
                    if (usage.Details?.InputTokenCount is long input)
                    {
                        prompt = (int)input;
                    }

                    if (usage.Details?.OutputTokenCount is long output)
                    {
                        completion = (int)output;
                    }
                }
            }

            yield return update;
        }

        await RecordAsync(prompt, completion, cancellationToken).ConfigureAwait(false);
    }

    private async Task RecordAsync(int promptTokens, int completionTokens, CancellationToken cancellationToken)
    {
        if (promptTokens <= 0 && completionTokens <= 0)
        {
            return;
        }

        await _hotStore.AddUsageAsync(_sessionId, promptTokens, completionTokens, cancellationToken).ConfigureAwait(false);

        await _usageCounter.IncrementAsync(
            _modelId,
            _teamId,
            _userId,
            (int)AiModelUseType.App,
            _appId,
            promptTokens,
            completionTokens,
            cancellationToken).ConfigureAwait(false);
    }
}
