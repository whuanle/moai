#pragma warning disable CA1031 // 不捕获常规异常类型

using Maomi.MQ;
using MediatR;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using MoAI.AI.ChatCompletion;
using MoAI.AI.Commands;
using MoAI.AI.Models;
using MoAI.AiModel.Events;

namespace MoAI.AI.Handlers;

/// <summary>
/// <inheritdoc cref="OneSimpleChatCommand"/>
/// </summary>
public class OneSimpleChatCommandHandler : IRequestHandler<OneSimpleChatCommand, OneSimpleChatCommandResponse>
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IAiClientBuilder _aiClientBuilder;
    private readonly IMessagePublisher _messagePublisher;

    /// <summary>
    /// Initializes a new instance of the <see cref="OneSimpleChatCommandHandler"/> class.
    /// </summary>
    /// <param name="serviceProvider"></param>
    /// <param name="aiClientBuilder"></param>
    /// <param name="messagePublisher"></param>
    public OneSimpleChatCommandHandler(IServiceProvider serviceProvider, IAiClientBuilder aiClientBuilder, IMessagePublisher messagePublisher)
    {
        _serviceProvider = serviceProvider;
        _aiClientBuilder = aiClientBuilder;
        _messagePublisher = messagePublisher;
    }

    /// <inheritdoc/>
    public async Task<OneSimpleChatCommandResponse> Handle(OneSimpleChatCommand request, CancellationToken cancellationToken)
    {
        var kernelBuilder = Kernel.CreateBuilder();

        var kernel = _aiClientBuilder.Configure(kernelBuilder, request.Endpoint)
            .Build();

        var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();

        var executionSettings = new PromptExecutionSettings()
        {
            ModelId = request.Endpoint.Name,
            FunctionChoiceBehavior = FunctionChoiceBehavior.None()
        };

        ChatHistory history = [];
        history.AddSystemMessage(request.Prompt);
        history.AddUserMessage(request.Question);

        var response = await chatCompletionService.GetChatMessageContentAsync(history, kernel: kernel);

        history.AddAssistantMessage(response.Content ?? string.Empty);

        OpenAIChatCompletionsUsage chatCompletionsUsage = new();
        if (response.Metadata?.TryGetValue("Usage", out object? usage) == true)
        {
            try
            {
                // usage 转 JsonElement
                var usageJson = System.Text.Json.JsonSerializer.SerializeToElement(usage);
                chatCompletionsUsage = new OpenAIChatCompletionsUsage
                {
                    PromptTokens = usageJson.GetProperty("InputTokenCount").GetInt32(),
                    CompletionTokens = usageJson.GetProperty("OutputTokenCount").GetInt32(),
                    TotalTokens = usageJson.GetProperty("TotalTokenCount").GetInt32()
                };
            }
            catch
            {
            }
        }

        await _messagePublisher.AutoPublishAsync(
            new AiModelUseageMessage
            {
                AiModelId = request.AiModelId,
                Channel = request.Channel,
                ContextUserId = request.ContextUserId,
                TokenUsage = chatCompletionsUsage
            });

        return new OneSimpleChatCommandResponse
        {
            Content = response.Content ?? string.Empty,
            Useage = new Models.TextTokenUsage
            {
                InputTokenCount = chatCompletionsUsage.PromptTokens,
                OutputTokenCount = chatCompletionsUsage.CompletionTokens,
                TotalTokenCount = chatCompletionsUsage.TotalTokens
            }
        };
    }
}
