using System.Text.Json.Nodes;

namespace MoAI.App.Workflow.DataTransfer;

/// <summary>
/// 工作流变量作用域 - 流程数据传输的统一数据源.
/// 四个变量命名空间：
/// sys.*      - 系统变量（实例 ID、启动时间等）；
/// system.*   - 流程全局变量（启动时可赋值）；
/// input.*    - 工作流启动参数；
/// {node}.*   - 已完成节点的输出（键为节点 Key）.
/// </summary>
public interface IWorkflowVariableScope
{
    /// <summary>
    /// 系统变量.
    /// </summary>
    JsonObject SystemVariables { get; }

    /// <summary>
    /// 流程全局变量（system.* 引用）.
    /// </summary>
    JsonObject WorkflowGlobals { get; }

    /// <summary>
    /// 工作流启动参数.
    /// </summary>
    JsonObject InputParameters { get; }

    /// <summary>
    /// 已完成节点的输出，键为节点 Key.
    /// </summary>
    IReadOnlyDictionary<string, JsonObject> NodeOutputs { get; }

    /// <summary>
    /// 尝试按变量引用取值，支持 "sys.x"、"input.x"、"nodeKey.field.path[0]"、通配 "nodeKey.list[*].name".
    /// </summary>
    /// <param name="reference">变量引用.</param>
    /// <param name="value">解析结果.</param>
    /// <returns>是否解析成功.</returns>
    bool TryResolve(string reference, out JsonNode? value);

    /// <summary>
    /// 构建供 JsonPath 查询的上下文对象：{ "sys": {...}, "input": {...}, "nodes": { nodeKey: {...} } }.
    /// </summary>
    JsonObject ToJsonPathContext();
}

/// <summary>
/// <see cref="IWorkflowVariableScope"/> 默认实现.
/// </summary>
public class WorkflowVariableScope : IWorkflowVariableScope
{
    private readonly Dictionary<string, JsonObject> _nodeOutputs;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkflowVariableScope"/> class.
    /// </summary>
    public WorkflowVariableScope(
        JsonObject? systemVariables = null,
        JsonObject? inputParameters = null,
        IReadOnlyDictionary<string, JsonObject>? nodeOutputs = null,
        JsonObject? workflowGlobals = null)
    {
        SystemVariables = systemVariables?.CloneObject() ?? new JsonObject();
        InputParameters = inputParameters?.CloneObject() ?? new JsonObject();
        WorkflowGlobals = workflowGlobals?.CloneObject() ?? new JsonObject();
        _nodeOutputs = nodeOutputs?.ToDictionary(kv => kv.Key, kv => kv.Value.CloneObject())
            ?? new Dictionary<string, JsonObject>();
    }

    /// <inheritdoc/>
    public JsonObject SystemVariables { get; }

    /// <inheritdoc/>
    public JsonObject InputParameters { get; }

    /// <inheritdoc/>
    public JsonObject WorkflowGlobals { get; }

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, JsonObject> NodeOutputs => _nodeOutputs;

    /// <summary>
    /// 记录一个节点的输出（节点完成后调用）.
    /// </summary>
    public void SetNodeOutput(string nodeKey, JsonObject output)
    {
        _nodeOutputs[nodeKey] = output.CloneObject();
    }

    /// <inheritdoc/>
    public bool TryResolve(string reference, out JsonNode? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(reference))
        {
            return false;
        }

        // 拆出数组访问段：path[0] / path[*]
        var (path, index, wildcard) = SplitArrayAccess(reference.Trim());

        JsonNode? current;
        if (path.StartsWith("sys.", StringComparison.OrdinalIgnoreCase))
        {
            current = Navigate(SystemVariables, path[4..]);
        }
        else if (path.StartsWith("system.", StringComparison.OrdinalIgnoreCase))
        {
            current = Navigate(WorkflowGlobals, path[7..]);
        }
        else if (path.StartsWith("input.", StringComparison.OrdinalIgnoreCase))
        {
            current = Navigate(InputParameters, path[6..]);
        }
        else
        {
            var dotIndex = path.IndexOf('.');
            var nodeKey = dotIndex < 0 ? path : path[..dotIndex];
            if (!_nodeOutputs.TryGetValue(nodeKey, out var output))
            {
                return false;
            }

            current = dotIndex < 0 ? output : Navigate(output, path[(dotIndex + 1)..]);
        }

        if (current == null)
        {
            return false;
        }

        if (wildcard)
        {
            if (current is JsonArray array)
            {
                value = new JsonArray(array.Select(item => item?.DeepClone()).ToArray());
                return true;
            }

            return false;
        }

        if (index >= 0)
        {
            if (current is not JsonArray indexArray || index >= indexArray.Count)
            {
                return false;
            }

            value = indexArray[index]?.DeepClone();
            return true;
        }

        value = current.DeepClone();
        return true;
    }

    /// <inheritdoc/>
    public JsonObject ToJsonPathContext()
    {
        var nodes = new JsonObject();
        foreach (var (key, output) in _nodeOutputs)
        {
            nodes[key] = output.DeepClone();
        }

        return new JsonObject
        {
            ["sys"] = SystemVariables.DeepClone(),
            ["system"] = WorkflowGlobals.DeepClone(),
            ["input"] = InputParameters.DeepClone(),
            ["nodes"] = nodes,
        };
    }

    /// <summary>
    /// 按点分路径逐层导航 JSON 对象，支持 "a.b.c" 与 "list[0].name" 混合写法.
    /// </summary>
    private static JsonNode? Navigate(JsonObject root, string path)
    {
        var segments = path.Split('.');
        JsonNode? current = root;
        foreach (var segment in segments)
        {
            if (current == null)
            {
                return null;
            }

            // 段内可能带数组下标，如 "documents[0]"
            var name = segment;
            int? index = null;
            var bracket = segment.IndexOf('[');
            if (bracket >= 0)
            {
                name = segment[..bracket];
                var indexText = segment[(bracket + 1)..].TrimEnd(']');
                if (int.TryParse(indexText, out var parsedIndex))
                {
                    index = parsedIndex;
                }
            }

            if (current is JsonObject obj)
            {
                if (!obj.TryGetPropertyValue(name, out current) || current == null)
                {
                    return null;
                }
            }
            else
            {
                return null;
            }

            if (index.HasValue)
            {
                if (current is not JsonArray array || index.Value >= array.Count)
                {
                    return null;
                }

                current = array[index.Value];
            }
        }

        return current;
    }

    /// <summary>
    /// 拆分末尾数组访问：返回 (路径, 下标或 -1, 是否通配).
    /// </summary>
    private static (string Path, int Index, bool Wildcard) SplitArrayAccess(string reference)
    {
        if (reference.EndsWith("[*]", StringComparison.Ordinal))
        {
            return (reference[..^3], -1, true);
        }

        var bracket = reference.LastIndexOf('[');
        if (bracket > 0 && reference.EndsWith(']') && int.TryParse(reference[(bracket + 1)..^1], out var index))
        {
            return (reference[..bracket], index, false);
        }

        return (reference, -1, false);
    }
}
