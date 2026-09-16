using System.Text.Json.Nodes;
using MoAI.App.Workflow.Definition;

namespace MoAI.App.Workflow.DataTransfer;

/// <summary>
/// 节点输入解析器 - 节点执行前，把节点定义中的输入字段绑定解析为实际输入数据.
/// 这一步完成"上游节点输出 → 当前节点输入"的数据传输.
/// </summary>
public class InputResolver
{
    private readonly IExpressionEvaluator _expressionEvaluator;

    /// <summary>
    /// Initializes a new instance of the <see cref="InputResolver"/> class.
    /// </summary>
    public InputResolver(IExpressionEvaluator expressionEvaluator)
    {
        _expressionEvaluator = expressionEvaluator;
    }

    /// <summary>
    /// 解析节点输入.
    /// </summary>
    /// <param name="node">节点定义.</param>
    /// <param name="scope">变量作用域.</param>
    /// <returns>输入 JSON 对象，键为字段名称.</returns>
    public JsonObject Resolve(NodeDefinition node, IWorkflowVariableScope scope)
    {
        var inputs = new JsonObject();
        foreach (var (fieldName, binding) in node.Inputs)
        {
            try
            {
                inputs[fieldName] = _expressionEvaluator.Evaluate(binding, scope);
            }
            catch (WorkflowException) when (!binding.Required)
            {
                // 非必需字段（如分支汇合节点引用未执行分支）解析失败时置为 null
                inputs[fieldName] = null;
            }
            catch (Exception ex) when (ex is not WorkflowException)
            {
                throw new WorkflowException($"节点 {node.Key} 的输入字段 {fieldName} 求值失败：{ex.Message}", ex);
            }
        }

        return inputs;
    }
}
