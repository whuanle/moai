using System.Text.Json.Nodes;
using Moq;
using Xunit;
using Xunit.Abstractions;
using MoAI.App.Workflow.Definition;
using MoAI.App.Workflow.Nodes;

namespace MoAI.App.Workflow.Tests;

public class DiagnosticResumeTests
{
    private readonly ITestOutputHelper _output;

    public DiagnosticResumeTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task Resume_Diagnostic()
    {
        var harness = new WorkflowTestHarness();
        await harness.Store.SaveDefinitionAsync(WorkflowTestHarness.CreateDocQaDefinition());
        harness.AiChat.Setup(c => c.CompleteAsync(
            It.IsAny<AiChatRequest>(), It.IsAny<Func<string, Task>?>(), It.IsAny<CancellationToken>())).ReturnsAsync("AI 回答");

        harness.PluginInvoker
            .Setup(i => i.InvokeAsync("mock.knowledgeSearch", It.IsAny<JsonObject>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new JsonObject { ["documents"] = new JsonArray(), ["hasResult"] = false });

        var call = 0;
        harness.PluginInvoker
            .Setup(i => i.InvokeAsync("mock.fallback", It.IsAny<JsonObject>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                if (call == 0)
                {
                    call++;
                    throw new InvalidOperationException("插件服务暂不可用");
                }

                return new JsonObject { ["answer"] = "兜底成功" };
            });

        var suspended = await harness.Engine.StartAsync("doc-qa", new JsonObject { ["query"] = "问题" });
        _output.WriteLine($"第一次: status={suspended.Status} err={suspended.ErrorMessage}");
        foreach (var (key, state) in suspended.NodeStates)
        {
            _output.WriteLine($"  {key}: {state.State} attempts={state.Attempts} input={state.Input?.ToJsonString()} output={state.Output?.ToJsonString()}");
        }

        var resumed = await harness.Engine.ResumeAsync(suspended.Id);
        _output.WriteLine($"恢复后: status={resumed.Status} err={resumed.ErrorMessage} output={resumed.Output?.ToJsonString()}");
        foreach (var (key, state) in resumed.NodeStates)
        {
            _output.WriteLine($"  {key}: {state.State} attempts={state.Attempts} output={state.Output?.ToJsonString()}");
        }
    }
}
