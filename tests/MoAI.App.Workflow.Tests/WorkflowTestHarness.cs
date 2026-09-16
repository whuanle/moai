using Microsoft.Extensions.DependencyInjection;
using MoAI.App.Workflow.Definition;
using MoAI.App.Workflow.Instance;
using MoAI.App.Workflow.Nodes;
using MoAI.App.Workflow.Persistence;
using Moq;

namespace MoAI.App.Workflow.Tests;

/// <summary>
/// 测试装配：引擎完整 DI 对象图 + 内存存储 + Mock 端口.
/// </summary>
public sealed class WorkflowTestHarness
{
    public InMemoryWorkflowStore Store { get; } = new();

    public Mock<IWorkflowPluginInvoker> PluginInvoker { get; } = new();

    public Mock<IAiChatClient> AiChat { get; } = new();

    public IServiceProvider Services { get; }

    public WorkflowTestHarness()
    {
        var services = new ServiceCollection();
        services.AddMoAIWorkflow();
        services.AddSingleton<IWorkflowDefinitionStore>(Store);
        services.AddSingleton<IWorkflowInstanceStore>(Store);
        services.AddSingleton<IWorkflowEventLogStore>(Store);
        services.AddSingleton(PluginInvoker.Object);
        services.AddSingleton(AiChat.Object);
        Services = services.BuildServiceProvider();
    }

    public WorkflowEngine Engine => Services.GetRequiredService<WorkflowEngine>();

    /// <summary>
    /// 构建与 DemoDefinition 同构的"文档问答"流程定义：
    /// start → search(插件) → digest(JS) → check(条件) → [true] answer(AI) / [false] fallback(插件) → end.
    /// </summary>
    public static WorkflowDefinition CreateDocQaDefinition()
    {
        return new WorkflowDefinition
        {
            Id = "doc-qa",
            Name = "文档智能问答",
            Version = 1,
            Status = DefinitionStatus.Published,
            Nodes =
            [
                new NodeDefinition
                {
                    Key = "start",
                    Name = "开始",
                    Type = NodeTypes.Start,
                    Outputs =
                    [
                        new PortDefinition { Name = "query", FieldType = FieldType.String, IsRequired = true },
                    ],
                },
                new NodeDefinition
                {
                    Key = "search",
                    Name = "知识库检索",
                    Type = NodeTypes.Plugin,
                    Config = System.Text.Json.JsonSerializer.SerializeToElement(new { pluginKey = "mock.knowledgeSearch" }),
                    Inputs = new Dictionary<string, FieldBinding>
                    {
                        ["query"] = new FieldBinding { ExpressionType = ExpressionType.Variable, Value = "start.query" },
                    },
                    Outputs =
                    [
                        new PortDefinition { Name = "documents", FieldType = FieldType.Array },
                        new PortDefinition { Name = "hasResult", FieldType = FieldType.Boolean },
                    ],
                },
                new NodeDefinition
                {
                    Key = "digest",
                    Name = "资料摘要",
                    Type = NodeTypes.JavaScript,
                    Config = System.Text.Json.JsonSerializer.SerializeToElement(new
                    {
                        code = "function run(inputs, sys, nodes) {\n  var docs = (nodes.search && nodes.search.documents) || [];\n  var titles = docs.map(function (d) { return d.title; }).join('、');\n  return { summary: titles || '无资料' };\n}",
                    }),
                    Outputs = [new PortDefinition { Name = "summary", FieldType = FieldType.String }],
                },
                new NodeDefinition
                {
                    Key = "check",
                    Name = "是否有检索结果",
                    Type = NodeTypes.Condition,
                    Inputs = new Dictionary<string, FieldBinding>
                    {
                        ["condition"] = new FieldBinding { ExpressionType = ExpressionType.Variable, Value = "search.hasResult" },
                    },
                },
                new NodeDefinition
                {
                    Key = "answer",
                    Name = "AI 回答",
                    Type = NodeTypes.AiChat,
                    Config = System.Text.Json.JsonSerializer.SerializeToElement(new { model = "mock-gpt-4o" }),
                    Inputs = new Dictionary<string, FieldBinding>
                    {
                        ["system"] = new FieldBinding { ExpressionType = ExpressionType.Fixed, Value = "你是问答助手。" },
                        ["prompt"] = new FieldBinding
                        {
                            ExpressionType = ExpressionType.Interpolation,
                            Value = "用户问题：{start.query}\n参考资料摘要：{digest.summary}",
                        },
                    },
                    Outputs = [new PortDefinition { Name = "answer", FieldType = FieldType.String }],
                },
                new NodeDefinition
                {
                    Key = "fallback",
                    Name = "兜底回答",
                    Type = NodeTypes.Plugin,
                    Config = System.Text.Json.JsonSerializer.SerializeToElement(new { pluginKey = "mock.fallback" }),
                    Inputs = new Dictionary<string, FieldBinding>
                    {
                        ["query"] = new FieldBinding { ExpressionType = ExpressionType.Variable, Value = "start.query" },
                    },
                    Outputs = [new PortDefinition { Name = "answer", FieldType = FieldType.String }],
                },
                new NodeDefinition
                {
                    Key = "end",
                    Name = "结束",
                    Type = NodeTypes.End,
                    Inputs = new Dictionary<string, FieldBinding>
                    {
                        ["answer"] = new FieldBinding { ExpressionType = ExpressionType.Variable, Value = "answer.answer", Required = false },
                        ["fallbackAnswer"] = new FieldBinding { ExpressionType = ExpressionType.Variable, Value = "fallback.answer", Required = false },
                    },
                    Outputs =
                    [
                        new PortDefinition { Name = "answer", FieldType = FieldType.String },
                        new PortDefinition { Name = "fallbackAnswer", FieldType = FieldType.String },
                    ],
                },
            ],
            Connections =
            [
                new ConnectionDefinition { Id = "c1", Source = "start", Target = "search" },
                new ConnectionDefinition { Id = "c2", Source = "search", Target = "digest" },
                new ConnectionDefinition { Id = "c3", Source = "digest", Target = "check" },
                new ConnectionDefinition { Id = "c4", Source = "check", Target = "answer", Condition = "true", Label = "命中" },
                new ConnectionDefinition { Id = "c5", Source = "check", Target = "fallback", Condition = "false", Label = "未命中" },
                new ConnectionDefinition { Id = "c6", Source = "answer", Target = "end" },
                new ConnectionDefinition { Id = "c7", Source = "fallback", Target = "end" },
            ],
        };
    }
}
