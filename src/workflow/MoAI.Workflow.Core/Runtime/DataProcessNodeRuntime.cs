using Jint;
using MoAI.Infra.Exceptions;
using MoAI.Workflow.Enums;
using MoAI.Workflow.Models;

namespace MoAI.Workflow.Runtime;

/// <summary>
/// DataProcess 节点运行时实现.
/// DataProcess 节点负责对输入数据执行数据转换操作（map、filter、aggregate）.
/// </summary>
public class DataProcessNodeRuntime : INodeRuntime
{
    /// <inheritdoc/>
    public NodeType SupportedNodeType => NodeType.DataProcess;

    /// <summary>
    /// 执行 DataProcess 节点逻辑.
    /// 根据操作类型（map、filter、aggregate）对输入数据执行相应的转换操作.
    /// </summary>
    /// <param name="nodeDefine">节点定义.</param>
    /// <param name="inputs">节点输入数据，应包含 operation、data 和 expression 字段.</param>
    /// <param name="context">工作流上下文.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>包含数据处理结果的执行结果.</returns>
    public Task<NodeExecutionResult> ExecuteAsync(
        INodeDefine nodeDefine,
        Dictionary<string, object> inputs,
        IWorkflowContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            // 1. 验证必需的输入字段
            if (!inputs.TryGetValue("operation", out var operationObj))
            {
                return Task.FromResult(NodeExecutionResult.Failure("缺少必需的输入字段: operation"));
            }

            if (!inputs.TryGetValue("data", out var dataObj))
            {
                return Task.FromResult(NodeExecutionResult.Failure("缺少必需的输入字段: data"));
            }

            if (!inputs.TryGetValue("expression", out var expressionObj))
            {
                return Task.FromResult(NodeExecutionResult.Failure("缺少必需的输入字段: expression"));
            }

            string operation = operationObj?.ToString()?.ToLowerInvariant() ?? string.Empty;
            string expression = expressionObj?.ToString() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(operation))
            {
                return Task.FromResult(NodeExecutionResult.Failure("operation 字段不能为空"));
            }

            if (string.IsNullOrWhiteSpace(expression))
            {
                return Task.FromResult(NodeExecutionResult.Failure("expression 字段不能为空"));
            }

            // 2. 将数据转换为可枚举集合
            var dataCollection = ConvertToEnumerable(dataObj);
            if (dataCollection == null)
            {
                return Task.FromResult(NodeExecutionResult.Failure("data 字段必须是可枚举的集合类型"));
            }

            // 3. 根据操作类型执行相应的处理
            object? result;
            try
            {
                result = operation switch
                {
                    "map" => ExecuteMap(dataCollection, expression, cancellationToken),
                    "filter" => ExecuteFilter(dataCollection, expression, cancellationToken),
                    "aggregate" or "reduce" => ExecuteAggregate(dataCollection, expression, inputs, cancellationToken),
                    _ => throw new BusinessException($"不支持的操作类型: {operation}") { StatusCode = 400 }
                };
            }
            catch (BusinessException bex)
            {
                return Task.FromResult(NodeExecutionResult.Failure($"数据处理失败: {bex.Message}"));
            }
            catch (Exception ex)
            {
                return Task.FromResult(NodeExecutionResult.Failure($"数据处理异常: {ex.Message}"));
            }

            // 4. 构建输出
            var output = new Dictionary<string, object>
            {
                ["result"] = result ?? new List<object>(),
                ["operation"] = operation,
                ["count"] = result is IEnumerable<object> enumerable ? enumerable.Count() : 1
            };

            return Task.FromResult(NodeExecutionResult.Success(output));
        }
        catch (Exception ex)
        {
            return Task.FromResult(NodeExecutionResult.Failure(ex));
        }
    }

    /// <summary>
    /// 执行 map 操作，对集合中的每个元素应用转换函数.
    /// </summary>
    /// <param name="data">输入数据集合.</param>
    /// <param name="expression">JavaScript 转换表达式.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>转换后的集合.</returns>
    private List<object> ExecuteMap(
        IEnumerable<object> data,
        string expression,
        CancellationToken cancellationToken)
    {
        var results = new List<object>();
        var index = 0;

        foreach (var item in data)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw new BusinessException("Map 操作被取消") { StatusCode = 400 };
            }

            var transformedItem = ExecuteJavaScriptExpression(expression, item, index, cancellationToken);
            results.Add(transformedItem ?? new object());
            index++;
        }

        return results;
    }

    /// <summary>
    /// 执行 filter 操作，根据条件过滤集合中的元素.
    /// </summary>
    /// <param name="data">输入数据集合.</param>
    /// <param name="expression">JavaScript 过滤表达式.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>过滤后的集合.</returns>
    private List<object> ExecuteFilter(
        IEnumerable<object> data,
        string expression,
        CancellationToken cancellationToken)
    {
        var results = new List<object>();
        var index = 0;

        foreach (var item in data)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw new BusinessException("Filter 操作被取消") { StatusCode = 400 };
            }

            var filterResult = ExecuteJavaScriptExpression(expression, item, index, cancellationToken);
            
            // 将结果转换为布尔值
            if (ConvertToBoolean(filterResult))
            {
                results.Add(item);
            }

            index++;
        }

        return results;
    }

    /// <summary>
    /// 执行 aggregate 操作，将集合归约为单个值.
    /// </summary>
    /// <param name="data">输入数据集合.</param>
    /// <param name="expression">JavaScript 归约表达式.</param>
    /// <param name="inputs">输入参数，可能包含 initialValue.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>归约后的值.</returns>
    private object ExecuteAggregate(
        IEnumerable<object> data,
        string expression,
        Dictionary<string, object> inputs,
        CancellationToken cancellationToken)
    {
        // 获取初始值（如果提供）
        object? accumulator = inputs.TryGetValue("initialValue", out var initialValueObj) 
            ? initialValueObj 
            : null;

        var dataList = data.ToList();
        var startIndex = 0;

        // 如果没有提供初始值，使用第一个元素作为初始值
        if (accumulator == null)
        {
            if (dataList.Count == 0)
            {
                throw new BusinessException("无法对空集合执行 aggregate 操作且未提供初始值") { StatusCode = 400 };
            }

            accumulator = dataList[0];
            startIndex = 1;
        }

        // 对剩余元素执行归约
        for (int i = startIndex; i < dataList.Count; i++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw new BusinessException("Aggregate 操作被取消") { StatusCode = 400 };
            }

            accumulator = ExecuteJavaScriptAggregateExpression(
                expression, 
                accumulator, 
                dataList[i], 
                i, 
                cancellationToken);
        }

        return accumulator ?? new object();
    }

    /// <summary>
    /// 执行 JavaScript 表达式（用于 map 和 filter）.
    /// </summary>
    /// <param name="expression">JavaScript 表达式.</param>
    /// <param name="item">当前项.</param>
    /// <param name="index">当前索引.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>表达式执行结果.</returns>
    private object? ExecuteJavaScriptExpression(
        string expression,
        object item,
        int index,
        CancellationToken cancellationToken)
    {
        try
        {
            using var engine = new Engine(options =>
            {
                options.LimitMemory(4_000_000);
                options.LimitRecursion(100);
                options.TimeoutInterval(TimeSpan.FromSeconds(4));
                options.MaxStatements(1000);
                options.CancellationToken(cancellationToken);
            });

            // 注入变量
            engine.SetValue("item", item);
            engine.SetValue("index", index);

            // 执行表达式
            var completionValue = engine.Evaluate(expression);

            if (completionValue.IsNull() || completionValue.IsUndefined())
            {
                return null;
            }

            return completionValue.ToObject();
        }
        catch (Jint.Runtime.JavaScriptException jsEx)
        {
            throw new BusinessException($"JavaScript 运行时错误: {jsEx.Message}") { StatusCode = 400 };
        }
        catch (Exception ex)
        {
            throw new BusinessException($"JavaScript 执行失败: {ex.Message}") { StatusCode = 400 };
        }
    }

    /// <summary>
    /// 执行 JavaScript 归约表达式（用于 aggregate）.
    /// </summary>
    /// <param name="expression">JavaScript 表达式.</param>
    /// <param name="accumulator">累加器值.</param>
    /// <param name="currentItem">当前项.</param>
    /// <param name="index">当前索引.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>表达式执行结果.</returns>
    private object? ExecuteJavaScriptAggregateExpression(
        string expression,
        object accumulator,
        object currentItem,
        int index,
        CancellationToken cancellationToken)
    {
        try
        {
            using var engine = new Engine(options =>
            {
                options.LimitMemory(4_000_000);
                options.LimitRecursion(100);
                options.TimeoutInterval(TimeSpan.FromSeconds(4));
                options.MaxStatements(1000);
                options.CancellationToken(cancellationToken);
            });

            // 注入变量
            engine.SetValue("accumulator", accumulator);
            engine.SetValue("acc", accumulator); // 别名
            engine.SetValue("item", currentItem);
            engine.SetValue("current", currentItem); // 别名
            engine.SetValue("index", index);

            // 执行表达式
            var completionValue = engine.Evaluate(expression);

            if (completionValue.IsNull() || completionValue.IsUndefined())
            {
                return null;
            }

            return completionValue.ToObject();
        }
        catch (Jint.Runtime.JavaScriptException jsEx)
        {
            throw new BusinessException($"JavaScript 运行时错误: {jsEx.Message}") { StatusCode = 400 };
        }
        catch (Exception ex)
        {
            throw new BusinessException($"JavaScript 执行失败: {ex.Message}") { StatusCode = 400 };
        }
    }

    /// <summary>
    /// 将对象转换为可枚举集合.
    /// </summary>
    /// <param name="obj">要转换的对象.</param>
    /// <returns>可枚举集合，如果无法转换则返回 null.</returns>
    private IEnumerable<object>? ConvertToEnumerable(object obj)
    {
        if (obj == null)
        {
            return null;
        }

        // 如果已经是 IEnumerable<object>，直接返回
        if (obj is IEnumerable<object> enumerable)
        {
            return enumerable;
        }

        // 如果是字符串，不要将其视为字符集合，而是单个项目
        if (obj is string str)
        {
            return new[] { str };
        }

        // 如果是其他 IEnumerable 类型，转换为 object 集合
        if (obj is System.Collections.IEnumerable collection)
        {
            var list = new List<object>();
            foreach (var item in collection)
            {
                list.Add(item);
            }
            return list;
        }

        // 如果是单个对象，包装为单元素集合
        return new[] { obj };
    }

    /// <summary>
    /// 将对象转换为布尔值.
    /// </summary>
    /// <param name="value">要转换的值.</param>
    /// <returns>布尔值.</returns>
    private bool ConvertToBoolean(object? value)
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
            var lowerStr = strValue.ToLowerInvariant().Trim();
            if (lowerStr == "true" || lowerStr == "1" || lowerStr == "yes" || lowerStr == "on")
            {
                return true;
            }
            if (lowerStr == "false" || lowerStr == "0" || lowerStr == "no" || lowerStr == "off" || lowerStr == string.Empty)
            {
                return false;
            }

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
}
