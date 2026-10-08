using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using MoAI.App.Workflow.Definition;
using MoAI.App.Workflow.Instance;
using MoAI.App.Workflow.Nodes;
using MoAI.App.Workflow.Persistence;
using Moq;
using Xunit;

namespace MoAI.App.Workflow.Tests;

/// <summary>
/// 引擎节点数据净化器接线：节点完成后、检查点落库前调用 INodeDataSanitizer，
/// 落库的节点输出与结束节点最终输出均为净化后数据.
/// </summary>
public class WorkflowNodeSanitizerTests
{
    /// <summary>确定性净化器：把字符串值 maskMe 全部替换为 MASKED.</summary>
    private sealed class StubSanitizer : INodeDataSanitizer
    {
        public List<string> SanitizedNodeKeys { get; } = [];

        public Task SanitizeAsync(WorkflowInstance instance, NodeDefinition node, NodeExecutionState state, CancellationToken cancellationToken)
        {
            SanitizedNodeKeys.Add(node.Key);
            MaskNode(state.Output);
            return Task.CompletedTask;
        }

        private static void MaskNode(JsonNode? node)
        {
            switch (node)
            {
                case JsonObject obj:
                    foreach (var key in obj.Select(static kv => kv.Key).ToList())
                    {
                        if (obj[key] is JsonValue value && value.TryGetValue<string>(out var text) && text == "maskMe")
                        {
                            obj[key] = "MASKED";
                        }
                        else
                        {
                            MaskNode(obj[key]);
                        }
                    }

                    break;
                case JsonArray array:
                    for (var i = 0; i < array.Count; i++)
                    {
                        if (array[i] is JsonValue value && value.TryGetValue<string>(out var text) && text == "maskMe")
                        {
                            array[i] = "MASKED";
                        }
                        else
                        {
                            MaskNode(array[i]);
                        }
                    }

                    break;
            }
        }
    }

    private static WorkflowDefinition CreateDefinition()
    {
        return new WorkflowDefinition
        {
            Id = "sanitize-demo",
            Name = "净化器演示",
            Version = 1,
            Status = DefinitionStatus.Published,
            Nodes =
            [
                new NodeDefinition
                {
                    Key = "start",
                    Name = "开始",
                    Type = NodeTypes.Start,
                    Outputs = [new PortDefinition { Name = "query", FieldType = FieldType.String, IsRequired = true }],
                },
                new NodeDefinition
                {
                    Key = "search",
                    Name = "检索",
                    Type = NodeTypes.Plugin,
                    Config = System.Text.Json.JsonSerializer.SerializeToElement(new { pluginKey = "mock.knowledgeSearch" }),
                },
                new NodeDefinition
                {
                    Key = "end",
                    Name = "结束",
                    Type = NodeTypes.End,
                    Inputs = new Dictionary<string, FieldBinding>
                    {
                        ["result"] = new FieldBinding { ExpressionType = ExpressionType.Variable, Value = "search.result", Required = false },
                    },
                },
            ],
            Connections =
            [
                new ConnectionDefinition { Id = "c1", Source = "start", Target = "search" },
                new ConnectionDefinition { Id = "c2", Source = "search", Target = "end" },
            ],
        };
    }

    [Fact]
    public async Task StartAsync_WithSanitizer_MasksNodeOutputBeforeCheckpointAndFinalOutput()
    {
        var store = new InMemoryWorkflowStore();
        await store.SaveDefinitionAsync(CreateDefinition());

        var pluginInvoker = new Mock<IWorkflowPluginInvoker>();
        pluginInvoker
            .Setup(i => i.InvokeAsync("mock.knowledgeSearch", It.IsAny<string?>(), It.IsAny<JsonObject>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new JsonObject { ["result"] = "maskMe" });

        var sanitizer = new StubSanitizer();

        var services = new ServiceCollection();
        services.AddMoAIWorkflow();
        services.AddSingleton<IWorkflowDefinitionStore>(store);
        services.AddSingleton<IWorkflowInstanceStore>(store);
        services.AddSingleton<IWorkflowEventLogStore>(store);
        services.AddSingleton(pluginInvoker.Object);
        services.AddSingleton(new Moq.Mock<IAiChatClient>().Object);
        services.AddSingleton(new Moq.Mock<IWorkflowAgentAppClient>().Object);
        services.AddSingleton(new Moq.Mock<IWorkflowWikiSearchClient>().Object);
        services.AddSingleton(new Moq.Mock<IWorkflowGraphSearchClient>().Object);
        services.AddSingleton<INodeDataSanitizer>(sanitizer);
        using var provider = services.BuildServiceProvider();

        var engine = provider.GetRequiredService<WorkflowEngine>();
        var instance = await engine.StartAsync("sanitize-demo", new JsonObject { ["query"] = "q" });

        Assert.Equal(InstanceStatus.Completed, instance.Status);

        // 节点输出在检查点落库前被净化（search 与 end 均经过净化器）
        Assert.Contains("search", sanitizer.SanitizedNodeKeys);
        Assert.Contains("end", sanitizer.SanitizedNodeKeys);

        // 存储中的实例数据为净化后数据
        var saved = await store.FindInstanceByIdAsync(instance.Id);
        Assert.NotNull(saved);
        var searchOutput = saved.NodeStates["search"].Output?.ToJsonString();
        Assert.Contains("MASKED", searchOutput);
        Assert.DoesNotContain("maskMe", searchOutput);

        // 结束节点输出（= 最终输出）引用的是净化后的上游输出
        var finalOutput = saved.Output?.ToJsonString();
        Assert.DoesNotContain("maskMe", finalOutput);
    }
}
