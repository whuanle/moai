namespace MoAI.App.Workflow.Nodes;

/// <summary>
/// 节点执行器注册表 - 节点类型到执行器的映射.
/// 支持运行时注册新节点类型（自定义插件节点等），是"按定义扩展各类节点"的扩展点.
/// </summary>
public interface INodeExecutorRegistry
{
    /// <summary>
    /// 注册节点执行器，同类型重复注册会抛出异常.
    /// </summary>
    void Register(INodeExecutor executor);

    /// <summary>
    /// 按节点类型获取执行器.
    /// </summary>
    INodeExecutor? Get(string nodeType);

    /// <summary>
    /// 获取全部已注册执行器.
    /// </summary>
    IReadOnlyCollection<INodeExecutor> GetAll();
}

/// <summary>
/// <see cref="INodeExecutorRegistry"/> 默认实现.
/// </summary>
public class NodeExecutorRegistry : INodeExecutorRegistry
{
    private readonly Dictionary<string, INodeExecutor> _executors = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc/>
    public void Register(INodeExecutor executor)
    {
        if (_executors.ContainsKey(executor.NodeType))
        {
            throw new WorkflowException($"节点类型 {executor.NodeType} 已注册执行器：{_executors[executor.NodeType].GetType().Name}");
        }

        _executors[executor.NodeType] = executor;
    }

    /// <inheritdoc/>
    public INodeExecutor? Get(string nodeType)
    {
        return _executors.TryGetValue(nodeType, out var executor) ? executor : null;
    }

    /// <inheritdoc/>
    public IReadOnlyCollection<INodeExecutor> GetAll()
    {
        return _executors.Values.ToList();
    }
}
