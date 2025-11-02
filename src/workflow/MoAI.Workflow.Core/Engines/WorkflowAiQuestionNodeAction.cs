using MediatR;
using Microsoft.Extensions.DependencyInjection;
using MoAI.AI.Commands;
using MoAI.AI.Models;
using MoAI.Workflow.Models.NodeDefinitions;

namespace MoAI.Workflow.Engines;

public class WorkflowAiQuestionNodeAction
{
    private readonly WorkflowAiQuestionNodeDefinition _definition;

    public WorkflowAiQuestionNodeAction(WorkflowAiQuestionNodeDefinition definition)
    {
        _definition = definition;
    }

    public static async Task<string> InvokeAsync(IServiceProvider serviceProvider)
    {
        var mediator = serviceProvider.GetRequiredService<IMediator>();
        await foreach (var item in mediator.CreateStream(new ChatCompletionsCommand { }))
        {
            if (item is IOpenAIChatCompletionsObject chatCompletion)
            {
                return chatCompletion.Choices.FirstOrDefault()?.Message.Content ?? string.Empty;
            }
        }
    }
}
