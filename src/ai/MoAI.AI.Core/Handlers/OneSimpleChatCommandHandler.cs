using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using MoAI.AI.ChatCompletion;
using MoAI.AI.Commands;
using MoAI.Infra.Exceptions;

namespace MoAI.AI.Handlers;

/// <summary>
/// <inheritdoc cref="OneSimpleChatCommand"/>
/// </summary>
public class OneSimpleChatCommandHandler : IRequestHandler<OneSimpleChatCommand, OneSimpleChatCommandResponse>
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IAiClientBuilder _aiClientBuilder;

    /// <summary>
    /// Initializes a new instance of the <see cref="OneSimpleChatCommandHandler"/> class.
    /// </summary>
    /// <param name="serviceProvider"></param>
    public OneSimpleChatCommandHandler(IServiceProvider serviceProvider, IAiClientBuilder aiClientBuilder)
    {
        _serviceProvider = serviceProvider;
        _aiClientBuilder = aiClientBuilder;
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

            // 手动执行函数
            FunctionChoiceBehavior = FunctionChoiceBehavior.None()
        };

        ChatHistory history = [];
        history.AddSystemMessage(request.Prompt);
        history.AddUserMessage(request.Question);

        var response = await chatCompletionService.GetChatMessageContentAsync(history, kernel: kernel);

        history.AddAssistantMessage(response.Content ?? string.Empty);

        var chatCompletion = response.InnerContent as OpenAI.Chat.ChatCompletion;

        if (chatCompletion == null)
        {
            throw new BusinessException("对话异常");
        }

        return new OneSimpleChatCommandResponse
        {
            Content = response.Content ?? string.Empty,
            Useage = new Models.TextTokenUsage
            {
                InputTokenCount = chatCompletion.Usage.InputTokenCount,
                OutputTokenCount = chatCompletion.Usage.OutputTokenCount,
                TotalTokenCount = chatCompletion.Usage.TotalTokenCount
            }
        };
    }
}
