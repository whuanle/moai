using Jint;
using Maomi;
using MoAI.Infra.Exceptions;
using MoAI.Workflow.Enums;
using MoAI.Workflow.Models;

namespace MoAI.Workflow.Runtime;

/// <summary>
/// DataProcess 节点运行时实现.
/// DataProcess 节点负责对输入数据执行数据转换操作（map、filter、aggregate）.
/// </summary>
[InjectOnTransient(ServiceKey = NodeType.DataProcess)]
public class DataProcessNodeRuntime : INodeRuntime
{
    /// <inheritdoc/>
    public NodeType SupportedNodeType => NodeType.DataProcess;

    /// <summary>
    /// 执行 DataProcess 节点逻辑.
    /// </summary>
    public Task<NodeExecutionResult> ExecuteAsync(
        Dictionary<string, object> inputs,
        INodePipeline pipeline,
        CancellationToken cancellationToken)
    {
        try
        {
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

            var dataCollection = ConvertToEnumerable(dataObj);
            if (dataCollection == null)
            {
                return Task.FromResult(NodeExecutionResult.Failure("data 字段必须是可枚举的集合类型"));
            }

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
            
            if (ConvertToBoolean(filterResult))
            {
                results.Add(item);
            }

            index++;
        }

        return results;
    }

    private object ExecuteAggregate(
        IEnumerable<object> data,
        string expression,
        Dictionary<string, object> inputs,
        CancellationToken cancellationToken)
    {
        object? accumulator = inputs.TryGetValue("initialValue", out var initialValueObj) 
            ? initialValueObj 
            : null;

        var dataList = data.ToList();
        var startIndex = 0;

        if (accumulator == null)
        {
            if (dataList.Count == 0)
            {
                throw new BusinessException("无法对空集合执行 aggregate 操作且未提供初始值") { StatusCode = 400 };
            }

            accumulator = dataList[0];
            startIndex = 1;
        }

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

            engine.SetValue("item", item);
            engine.SetValue("index", index);

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

            engine.SetValue("accumulator", accumulator);
            engine.SetValue("acc", accumulator);
            engine.SetValue("item", currentItem);
            engine.SetValue("current", currentItem);
            engine.SetValue("index", index);

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

    private IEnumerable<object>? ConvertToEnumerable(object obj)
    {
        if (obj == null)
        {
            return null;
        }

        if (obj is IEnumerable<object> enumerable)
        {
            return enumerable;
        }

        if (obj is string str)
        {
            return new[] { str };
        }

        if (obj is System.Collections.IEnumerable collection)
        {
            var list = new List<object>();
            foreach (var item in collection)
            {
                list.Add(item);
            }
            return list;
        }

        return new[] { obj };
    }

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

        if (value is System.Collections.ICollection collection)
        {
            return collection.Count > 0;
        }

        if (value is System.Collections.IEnumerable enumerable)
        {
            return enumerable.Cast<object>().Any();
        }

        return true;
    }
}
