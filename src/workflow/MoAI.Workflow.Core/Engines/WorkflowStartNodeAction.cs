using MoAI.Workflow.Models.NodeDefinitions;

namespace MoAI.Workflow.Engines;

/// <summary>
/// 开始节点执行引擎，只能对输入进行简单处理后输出.
/// </summary>
public class WorkflowStartNodeAction
{
    private readonly WorkflowStartNodeDefinition _nodeDefinition;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkflowStartNodeAction"/> class.
    /// </summary>
    /// <param name="nodeDefinition"></param>
    public WorkflowStartNodeAction(WorkflowStartNodeDefinition nodeDefinition)
    {
        _nodeDefinition = nodeDefinition;
    }

    /// <summary>
    /// 对输入参数进行处理并输出参数，检查输入参数是否缺少以及符合要求，触发成功失败事件.
    /// </summary>
    /// <param name="jsonParamter"></param>
    /// <param name="workflowContext"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public string Inovke(string jsonParamter, DefaultWorkflowContext workflowContext, CancellationToken cancellationToken)
    {
        return jsonParamter;
    }
}