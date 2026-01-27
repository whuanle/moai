using MoAI.Infra.Exceptions;
using MoAI.Workflow.Enums;
using MoAI.Workflow.Models;
using MoAI.Workflow.Services;

namespace MoAI.Workflow.Runtime;

/// <summary>
/// Condition 节点运行时实现.
/// Condition 节点负责评估布尔表达式并根据结果路由到不同的下一个节点.
/// </summary>
public class ConditionNodeRuntime : INodeRuntime
{
    private readonly ExpressionEvaluationService _expressionEvaluationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConditionNodeRuntime"/> class.
    /// </summary>
    /// <param name="expressionEvaluationService">表达式评估服务.</param>
    public ConditionNodeRuntime(ExpressionEvaluationService expressionEvaluationService)
    {
        _expressionEvaluationService = expressionEvaluationService;
    }

    /// <inheritdoc/>
    public NodeType SupportedNodeType => NodeType.Condition;

    /// <summary>
    /// 执行 Condition 节点逻辑.
    /// 评估条件表达式并返回布尔结果，用于工作流路由决策.
    /// </summary>
    /// <param name="nodeDefine">节点定义.</param>
    /// <param name="inputs">节点输入数据，应包含 condition 字段.</param>
    /// <param name="context">工作流上下文.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>包含条件评估结果的执行结果.</returns>
    public Task<NodeExecutionResult> ExecuteAsync(
        INodeDefine nodeDefine,
        Dictionary<string, object> inputs,
        IWorkflowContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            // 1. 验证必需的输入字段
            if (!inputs.TryGetValue("condition", out var conditionObj))
            {
                return Task.FromResult(NodeExecutionResult.Failure("缺少必需的输入字段: condition"));
            }

            // 2. 评估条件表达式
            bool conditionResult;
            try
            {
                // 条件可以是直接的布尔值或需要评估的表达式
                conditionResult = EvaluateCondition(conditionObj, context);
            }
            catch (BusinessException bex)
            {
                return Task.FromResult(NodeExecutionResult.Failure($"条件表达式评估失败: {bex.Message}"));
            }
            catch (Exception ex)
            {
                return Task.FromResult(NodeExecutionResult.Failure($"条件表达式评估异常: {ex.Message}"));
            }

            // 3. 构建输出
            var output = new Dictionary<string, object>
            {
                ["result"] = conditionResult,
                ["condition"] = conditionObj?.ToString() ?? string.Empty
            };

            return Task.FromResult(NodeExecutionResult.Success(output));
        }
        catch (Exception ex)
        {
            return Task.FromResult(NodeExecutionResult.Failure(ex));
        }
    }

    /// <summary>
    /// 评估条件表达式并返回布尔结果.
    /// </summary>
    /// <param name="conditionObj">条件对象，可以是布尔值、字符串或其他类型.</param>
    /// <param name="context">工作流上下文.</param>
    /// <returns>条件评估结果.</returns>
    private bool EvaluateCondition(object conditionObj, IWorkflowContext context)
    {
        // 如果已经是布尔值，直接返回
        if (conditionObj is bool boolValue)
        {
            return boolValue;
        }

        // 如果是字符串，尝试解析为布尔值或评估为表达式
        if (conditionObj is string conditionStr)
        {
            // 尝试直接解析为布尔值
            if (bool.TryParse(conditionStr, out var parsedBool))
            {
                return parsedBool;
            }

            // 尝试作为变量引用解析
            // 支持格式如: "nodeKey.result", "input.isEnabled", "sys.isAdmin"
            try
            {
                var resolvedValue = _expressionEvaluationService.EvaluateExpression(
                    conditionStr,
                    FieldExpressionType.Variable,
                    context);

                return ConvertToBoolean(resolvedValue);
            }
            catch
            {
                // 如果变量解析失败，尝试其他评估方式
            }

            // 支持简单的比较表达式（如 "true", "false", "1", "0"）
            return EvaluateSimpleExpression(conditionStr);
        }

        // 对于其他类型，尝试转换为布尔值
        return ConvertToBoolean(conditionObj);
    }

    /// <summary>
    /// 将对象转换为布尔值.
    /// </summary>
    /// <param name="value">要转换的值.</param>
    /// <returns>布尔值.</returns>
    private bool ConvertToBoolean(object value)
    {
        if (value == null)
        {
            return false;
        }

        if (value is bool boolValue)
        {
            return boolValue;
        }

        if (value is string strValue)
        {
            // 处理常见的布尔字符串表示
            var lowerStr = strValue.ToLowerInvariant().Trim();
            if (lowerStr == "true" || lowerStr == "1" || lowerStr == "yes" || lowerStr == "on")
            {
                return true;
            }
            if (lowerStr == "false" || lowerStr == "0" || lowerStr == "no" || lowerStr == "off" || lowerStr == string.Empty)
            {
                return false;
            }

            // 非空字符串视为 true
            return !string.IsNullOrWhiteSpace(strValue);
        }

        if (value is int intValue)
        {
            return intValue != 0;
        }

        if (value is long longValue)
        {
            return longValue != 0;
        }

        if (value is double doubleValue)
        {
            return Math.Abs(doubleValue) > double.Epsilon;
        }

        if (value is decimal decimalValue)
        {
            return decimalValue != 0;
        }

        // 对于集合类型，非空集合视为 true
        if (value is System.Collections.ICollection collection)
        {
            return collection.Count > 0;
        }

        if (value is System.Collections.IEnumerable enumerable)
        {
            return enumerable.Cast<object>().Any();
        }

        // 其他非 null 对象视为 true
        return true;
    }

    /// <summary>
    /// 评估简单的表达式字符串.
    /// </summary>
    /// <param name="expression">表达式字符串.</param>
    /// <returns>布尔值.</returns>
    private bool EvaluateSimpleExpression(string expression)
    {
        var trimmed = expression.Trim().ToLowerInvariant();

        // 处理常见的布尔表示
        return trimmed switch
        {
            "true" => true,
            "false" => false,
            "1" => true,
            "0" => false,
            "yes" => true,
            "no" => false,
            "on" => true,
            "off" => false,
            "" => false,
            _ => throw new BusinessException($"无法评估条件表达式: {expression}") { StatusCode = 400 }
        };
    }
}
