using MoAI.Workflow.Enums;
using MoAI.Workflow.Runtime;

namespace MoAI.Workflow.Services;

/// <summary>
/// 节点运行时工厂，负责创建和管理节点运行时实例.
/// 提供节点类型到运行时实现的映射和注册功能.
/// </summary>
public class NodeRuntimeFactory
{
    private readonly Dictionary<NodeType, INodeRuntime> _runtimes;

    /// <summary>
    /// 初始化节点运行时工厂.
    /// </summary>
    public NodeRuntimeFactory()
    {
        _runtimes = new Dictionary<NodeType, INodeRuntime>();
    }

    /// <summary>
    /// 初始化节点运行时工厂，并注册提供的运行时实例.
    /// </summary>
    /// <param name="runtimes">要注册的运行时实例集合.</param>
    public NodeRuntimeFactory(IEnumerable<INodeRuntime> runtimes)
    {
        _runtimes = new Dictionary<NodeType, INodeRuntime>();
        
        foreach (var runtime in runtimes)
        {
            RegisterRuntime(runtime.SupportedNodeType, runtime);
        }
    }

    /// <summary>
    /// 获取指定节点类型的运行时实例.
    /// </summary>
    /// <param name="nodeType">节点类型.</param>
    /// <returns>对应的节点运行时实例.</returns>
    /// <exception cref="InvalidOperationException">当指定的节点类型没有注册运行时时抛出.</exception>
    public INodeRuntime GetRuntime(NodeType nodeType)
    {
        if (!_runtimes.TryGetValue(nodeType, out var runtime))
        {
            throw new InvalidOperationException($"No runtime registered for node type: {nodeType}");
        }
        
        return runtime;
    }

    /// <summary>
    /// 注册节点运行时实例.
    /// 如果指定的节点类型已经注册，将覆盖现有的运行时实例.
    /// </summary>
    /// <param name="nodeType">节点类型.</param>
    /// <param name="runtime">节点运行时实例.</param>
    public void RegisterRuntime(NodeType nodeType, INodeRuntime runtime)
    {
        _runtimes[nodeType] = runtime;
    }

    /// <summary>
    /// 检查指定的节点类型是否已注册运行时.
    /// </summary>
    /// <param name="nodeType">节点类型.</param>
    /// <returns>如果已注册返回 true，否则返回 false.</returns>
    public bool IsRegistered(NodeType nodeType)
    {
        return _runtimes.ContainsKey(nodeType);
    }

    /// <summary>
    /// 获取所有已注册的节点类型.
    /// </summary>
    /// <returns>已注册的节点类型集合.</returns>
    public IEnumerable<NodeType> GetRegisteredNodeTypes()
    {
        return _runtimes.Keys;
    }

    /// <summary>
    /// 取消注册指定节点类型的运行时.
    /// </summary>
    /// <param name="nodeType">要取消注册的节点类型.</param>
    /// <returns>如果成功取消注册返回 true，如果节点类型未注册返回 false.</returns>
    public bool UnregisterRuntime(NodeType nodeType)
    {
        return _runtimes.Remove(nodeType);
    }
}
