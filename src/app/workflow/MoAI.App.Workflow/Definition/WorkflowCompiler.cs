namespace MoAI.App.Workflow.Definition;

/// <summary>
/// 编译后的工作流图 - 运行时使用的邻接表结构.
/// </summary>
public class CompiledWorkflow
{
    /// <summary>
    /// 原始定义.
    /// </summary>
    public WorkflowDefinition Definition { get; init; } = new();

    /// <summary>
    /// 节点映射，键为节点 Key.
    /// </summary>
    public IReadOnlyDictionary<string, NodeDefinition> NodeMap { get; init; } = new Dictionary<string, NodeDefinition>();

    /// <summary>
    /// 出边映射，键为源节点 Key.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<ConnectionDefinition>> OutgoingEdges { get; init; } =
        new Dictionary<string, IReadOnlyList<ConnectionDefinition>>();

    /// <summary>
    /// 入边映射，键为目标节点 Key.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<ConnectionDefinition>> IncomingEdges { get; init; } =
        new Dictionary<string, IReadOnlyList<ConnectionDefinition>>();

    /// <summary>
    /// 开始节点 Key.
    /// </summary>
    public string StartNodeKey { get; init; } = string.Empty;
}

/// <summary>
/// 工作流编译器 - 把前端设计 JSON（流程定义）编译成运行时可执行的 DAG 图.
/// </summary>
public class WorkflowCompiler
{
    private readonly WorkflowValidator _validator;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkflowCompiler"/> class.
    /// </summary>
    public WorkflowCompiler(WorkflowValidator validator)
    {
        _validator = validator;
    }

    /// <summary>
    /// 编译工作流定义，验证失败时抛出 <see cref="WorkflowValidationException"/>.
    /// </summary>
    public CompiledWorkflow Compile(WorkflowDefinition definition)
    {
        _validator.Validate(definition);

        var nodeMap = definition.Nodes.ToDictionary(n => n.Key);
        var outgoing = definition.Connections
            .GroupBy(c => c.Source)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<ConnectionDefinition>)g.ToList());
        var incoming = definition.Connections
            .GroupBy(c => c.Target)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<ConnectionDefinition>)g.ToList());

        return new CompiledWorkflow
        {
            Definition = definition,
            NodeMap = nodeMap,
            OutgoingEdges = outgoing,
            IncomingEdges = incoming,
            StartNodeKey = definition.Nodes.First(n => n.Type == NodeTypes.Start).Key,
        };
    }
}
