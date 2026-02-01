using Maomi;
using MoAI.Infra.Exceptions;
using MoAI.Workflow.Enums;
using MoAI.Workflow.Models;

namespace MoAI.Workflow.Services;

/// <summary>
/// 工作流定义服务，负责工作流定义的验证和管理.
/// 提供工作流图结构验证、节点类型验证、连接验证和节点设计验证功能.
/// </summary>
[InjectOnScoped]
public class WorkflowDefinitionService
{
    /// <summary>
    /// 验证工作流定义的有效性.
    /// 确保工作流定义满足以下条件：
    /// 1. 恰好有一个 Start 节点
    /// 2. 至少有一个 End 节点
    /// 3. 所有节点类型有效
    /// 4. 所有节点的下游节点都存在
    /// 5. 所有节点形成有效的有向图（无孤立节点）
    /// </summary>
    /// <param name="definition">工作流定义对象，包含节点信息.</param>
    /// <exception cref="BusinessException">当工作流定义无效时抛出，包含详细的验证错误信息.</exception>
    public void ValidateWorkflowDefinition(WorkflowDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (definition.Nodes == null || definition.Nodes.Count == 0)
        {
            throw new BusinessException(400, "工作流定义必须包含至少一个节点");
        }

        var nodeDesigns = definition.Nodes.ToList();

        // 验证 1: 恰好有一个 Start 节点
        var startNodes = nodeDesigns.Where(n => n.NodeType == NodeType.Start).ToList();
        if (startNodes.Count == 0)
        {
            throw new BusinessException(400, "工作流定义必须包含一个 Start 节点");
        }

        if (startNodes.Count > 1)
        {
            var startNodeKeys = string.Join(", ", startNodes.Select(n => n.NodeKey));
            throw new BusinessException(400, $"工作流定义只能包含一个 Start 节点，但发现 {startNodes.Count} 个: {startNodeKeys}");
        }

        // 验证 2: 至少有一个 End 节点
        var endNodes = nodeDesigns.Where(n => n.NodeType == NodeType.End).ToList();
        if (endNodes.Count == 0)
        {
            throw new BusinessException(400, "工作流定义必须包含至少一个 End 节点");
        }

        // 验证 3: 所有节点类型有效（枚举验证）
        var validNodeTypes = Enum.GetValues<NodeType>().ToHashSet();
        var invalidNodes = nodeDesigns.Where(n => !validNodeTypes.Contains(n.NodeType)).ToList();
        if (invalidNodes.Count != 0)
        {
            var invalidNodeInfo = string.Join(", ", invalidNodes.Select(n => $"{n.NodeKey}({n.NodeType})"));
            throw new BusinessException(400, $"工作流定义包含无效的节点类型: {invalidNodeInfo}");
        }

        // 验证 4: 检查节点 Key 唯一性
        var duplicateKeys = nodeDesigns
            .GroupBy(n => n.NodeKey)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicateKeys.Count != 0)
        {
            var duplicateKeyList = string.Join(", ", duplicateKeys);
            throw new BusinessException(400, $"工作流定义包含重复的节点 Key: {duplicateKeyList}");
        }

        // 验证 5: 验证节点连接
        ValidateNodeConnections(nodeDesigns);

        // 验证 6: 检查图的连通性（从 Start 节点可达所有非 End 节点）
        ValidateGraphConnectivity(nodeDesigns, startNodes[0]);

        // 验证 7: 检查是否存在循环（可选，但建议检查以避免无限循环）
        DetectCycles(nodeDesigns, startNodes[0]);
    }

    /// <summary>
    /// 验证节点连接的有效性.
    /// 确保节点连接满足以下条件：
    /// 1. 所有下游节点都存在
    /// 2. Start 节点有下游节点
    /// 3. End 节点没有下游节点
    /// 4. 所有非 End 节点都有至少一个下游节点
    /// </summary>
    /// <param name="nodes">节点列表.</param>
    /// <exception cref="BusinessException">当节点连接无效时抛出.</exception>
    private static void ValidateNodeConnections(List<NodeDesign> nodes)
    {
        var nodeKeys = nodes.Select(n => n.NodeKey).ToHashSet();

        // 验证 1: 所有下游节点都存在
        var invalidConnections = new List<string>();
        foreach (var node in nodes)
        {
            if (node.NextNodeKeys == null)
            {
                continue;
            }

            foreach (var nextKey in node.NextNodeKeys)
            {
                if (!nodeKeys.Contains(nextKey))
                {
                    invalidConnections.Add($"{node.NodeKey} -> {nextKey}");
                }
            }
        }

        if (invalidConnections.Count != 0)
        {
            var invalidList = string.Join(", ", invalidConnections);
            throw new BusinessException(400, $"以下节点的下游节点不存在: {invalidList}");
        }

        // 验证 2: Start 节点有下游节点
        var startNode = nodes.FirstOrDefault(n => n.NodeType == NodeType.Start);
        if (startNode != null)
        {
            if (startNode.NextNodeKeys == null || startNode.NextNodeKeys.Count == 0)
            {
                throw new BusinessException(400, "Start 节点必须有至少一个下游节点");
            }
        }

        // 验证 3: End 节点没有下游节点
        var endNodes = nodes.Where(n => n.NodeType == NodeType.End).ToList();
        foreach (var endNode in endNodes)
        {
            if (endNode.NextNodeKeys != null && endNode.NextNodeKeys.Count > 0)
            {
                throw new BusinessException(400, $"End 节点 '{endNode.NodeKey}' 不能有下游节点");
            }
        }

        // 验证 4: 所有非 End 节点都有至少一个下游节点（避免孤立节点）
        var nonEndNodes = nodes.Where(n => n.NodeType != NodeType.End).ToList();
        var orphanedNodes = nonEndNodes
            .Where(n => n.NextNodeKeys == null || n.NextNodeKeys.Count == 0)
            .Select(n => n.NodeKey)
            .ToList();

        if (orphanedNodes.Count != 0)
        {
            var orphanedList = string.Join(", ", orphanedNodes);
            throw new BusinessException(400, $"以下节点没有下游节点（孤立节点）: {orphanedList}");
        }
    }

    /// <summary>
    /// 验证工作流图的连通性.
    /// 确保从 Start 节点可以到达所有非 End 节点.
    /// </summary>
    private static void ValidateGraphConnectivity(List<NodeDesign> nodeDesigns, NodeDesign startNode)
    {
        var nodeMap = nodeDesigns.ToDictionary(n => n.NodeKey);
        var visited = new HashSet<string>();
        var queue = new Queue<string>();

        queue.Enqueue(startNode.NodeKey);
        visited.Add(startNode.NodeKey);

        while (queue.Count > 0)
        {
            var currentKey = queue.Dequeue();
            var currentNode = nodeMap[currentKey];

            // 遍历所有下游节点
            if (currentNode.NextNodeKeys != null)
            {
                foreach (var nextKey in currentNode.NextNodeKeys)
                {
                    if (!visited.Contains(nextKey))
                    {
                        visited.Add(nextKey);
                        queue.Enqueue(nextKey);
                    }
                }
            }
        }

        // 检查是否有孤立的节点（除了 End 节点）
        var unreachableNodes = nodeDesigns
            .Where(n => n.NodeType != NodeType.End && !visited.Contains(n.NodeKey))
            .Select(n => n.NodeKey)
            .ToList();

        if (unreachableNodes.Count != 0)
        {
            var unreachableNodeList = string.Join(", ", unreachableNodes);
            throw new BusinessException(
                400,
                $"工作流定义包含无法从 Start 节点到达的节点: {unreachableNodeList}");
        }
    }

    /// <summary>
    /// 检测工作流图中的循环.
    /// 使用深度优先搜索检测是否存在循环依赖.
    /// </summary>
    private static void DetectCycles(List<NodeDesign> nodeDesigns, NodeDesign startNode)
    {
        var nodeMap = nodeDesigns.ToDictionary(n => n.NodeKey);
        var visited = new HashSet<string>();
        var recursionStack = new HashSet<string>();

        bool HasCycle(string nodeKey, List<string> path)
        {
            if (recursionStack.Contains(nodeKey))
            {
                // 找到循环，构建循环路径
                var cycleStart = path.IndexOf(nodeKey);
                var cyclePath = string.Join(" -> ", path.Skip(cycleStart).Append(nodeKey));
                throw new BusinessException(400, $"工作流定义包含循环依赖: {cyclePath}");
            }

            if (visited.Contains(nodeKey))
            {
                return false;
            }

            visited.Add(nodeKey);
            recursionStack.Add(nodeKey);
            path.Add(nodeKey);

            // 检查所有下游节点
            var currentNode = nodeMap[nodeKey];
            if (currentNode.NextNodeKeys != null)
            {
                foreach (var nextKey in currentNode.NextNodeKeys)
                {
                    if (HasCycle(nextKey, path))
                    {
                        return true;
                    }
                }
            }

            path.RemoveAt(path.Count - 1);
            recursionStack.Remove(nodeKey);

            return false;
        }

        HasCycle(startNode.NodeKey, new List<string>());
    }
}
