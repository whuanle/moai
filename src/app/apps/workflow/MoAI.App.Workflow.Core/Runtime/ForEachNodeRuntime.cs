using MoAI.Infra.Exceptions;
using MoAI.Workflow.Enums;
using MoAI.Workflow.Models;

namespace MoAI.Workflow.Runtime;

/// <summary>
/// ForEach 节点运行时实现.
/// ForEach 节点负责迭代集合并为每个项目执行循环体，收集所有迭代的结果.
/// </summary>
public class ForEachNodeRuntime : INodeRuntime
{
    /// <inheritdoc/>
    public NodeType SupportedNodeType => NodeType.ForEach;

    /// <summary>
    /// 执行 ForEach 节点逻辑.
    /// 迭代输入集合，为每个项目执行循环体，并收集所有结果.
    /// </summary>
    /// <param name="nodeDefine">节点定义.</param>
    /// <param name="inputs">节点输入数据，应包含 collection 字段.</param>
    /// <param name="context">工作流上下文.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>包含所有迭代结果的执行结果.</returns>
    public Task<NodeExecutionResult> ExecuteAsync(
        INodeDefine nodeDefine,
        Dictionary<string, object> inputs,
        IWorkflowContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            // 1. 验证必需的输入字段
            if (!inputs.TryGetValue("collection", out var collectionObj))
            {
                return Task.FromResult(NodeExecutionResult.Failure("缺少必需的输入字段: collection"));
            }

            // 2. 将输入转换为可枚举集合
            var collection = ConvertToEnumerable(collectionObj);
            if (collection == null)
            {
                return Task.FromResult(NodeExecutionResult.Failure("collection 字段必须是可枚举的集合类型"));
            }

            // 3. 迭代集合并收集结果
            var results = new List<object>();
            var itemIndex = 0;

            foreach (var item in collection)
            {
                // 检查取消令牌
                if (cancellationToken.IsCancellationRequested)
                {
                    return Task.FromResult(NodeExecutionResult.Failure("ForEach 循环被取消"));
                }

                // 为每个项目创建迭代上下文
                var iterationContext = new Dictionary<string, object>
                {
                    ["item"] = item ?? new object(),
                    ["index"] = itemIndex
                };

                // 收集当前项目的结果
                results.Add(iterationContext);
                itemIndex++;
            }

            // 4. 构建输出
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
}
