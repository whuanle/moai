using Maomi;
using MoAI.Workflow.Enums;
using MoAI.Workflow.Models;

namespace MoAI.Workflow.Runtime;

/// <summary>
/// ForEach 节点运行时实现.
/// ForEach 节点负责迭代集合并为每个项目执行循环体，收集所有迭代的结果.
/// </summary>
[InjectOnTransient(ServiceKey = NodeType.ForEach)]
public class ForEachNodeRuntime : INodeRuntime
{
    /// <inheritdoc/>
    public NodeType SupportedNodeType => NodeType.ForEach;

    /// <summary>
    /// 执行 ForEach 节点逻辑.
    /// </summary>
    public Task<NodeExecutionResult> ExecuteAsync(
        Dictionary<string, object> inputs,
        INodePipeline pipeline,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!inputs.TryGetValue("collection", out var collectionObj))
            {
                return Task.FromResult(NodeExecutionResult.Failure("缺少必需的输入字段: collection"));
            }

            var collection = ConvertToEnumerable(collectionObj);
            if (collection == null)
            {
                return Task.FromResult(NodeExecutionResult.Failure("collection 字段必须是可枚举的集合类型"));
            }

            var results = new List<object>();
            var itemIndex = 0;

            foreach (var item in collection)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return Task.FromResult(NodeExecutionResult.Failure("ForEach 循环被取消"));
                }

                var iterationContext = new Dictionary<string, object>
                {
                    ["item"] = item ?? new object(),
                    ["index"] = itemIndex
                };

                results.Add(iterationContext);
                itemIndex++;
            }

            var output = new Dictionary<string, object>
            {
                ["results"] = results,
                ["count"] = itemIndex,
                ["collection"] = collectionObj
            };

            return Task.FromResult(NodeExecutionResult.Success(output));
        }
        catch (Exception ex)
        {
            return Task.FromResult(NodeExecutionResult.Failure(ex));
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
}
