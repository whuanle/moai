namespace MoAI.Workflow.Models.NodeDefinitions;

/// <summary>
/// 使用大语言模型问答，该类型不能使用插件和知识库，只能用于直接问答.
/// </summary>
public class WorkflowAiQuestionNodeDefinition : WorkflowNodefinition
{
    /// <summary>
    /// AI模型id.
    /// </summary>
    public int AiModelId { get; init; } = default!;

    /// <inheritdoc/>
    public override NodeType NodeType { get; protected init; } = NodeType.AiQuestion;

    /// <summary>
    /// 提示词模板.
    /// </summary>
    public string Prompt { get; init; } = default!;

    /// <summary>
    /// 用户问题，支持用户的问题插值.
    /// </summary>
    public string Question { get; init; } = default!;

    /// <summary>
    /// 流式输出，设置流式输出后，流程执行时该节点给用户呈现打字机效果.
    /// </summary>
    public bool IsStream { get; init; }

    /// <summary>
    /// 固定输出参数
    /// </summary>
    public IReadOnlyCollection<WorkflowFieldParmater> OutputParameters { get; protected init; } = new List<WorkflowFieldParmater>()
    {
        new WorkflowFieldParmater
        {
            Key = "text",
            Description = "生成内容",
            Type = WorkflowFieldType.String
        },
        new WorkflowFieldParmater
        {
            Key = "usage",
            Description = "模型使用量",
            Type = WorkflowFieldType.Object,
            Items = new List<WorkflowFieldParmater>()
            {
                new WorkflowFieldParmater
                {
                    Key = "completion_tokens",
                    Description = "生成的token数量",
                    Type = WorkflowFieldType.Integer
                },
                new WorkflowFieldParmater
                {
                    Key = "prompt_tokens",
                    Description = "提示词的token数量",
                    Type = WorkflowFieldType.Integer
                },
                new WorkflowFieldParmater
                {
                    Key = "total_tokens",
                    Description = "总的token数量",
                    Type = WorkflowFieldType.Integer
                }
            }
        }
    };
}
