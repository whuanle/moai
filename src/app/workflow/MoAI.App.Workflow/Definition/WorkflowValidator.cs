namespace MoAI.App.Workflow.Definition;

/// <summary>
/// 工作流定义验证器 - 校验前端设计 JSON 的图结构合法性.
/// </summary>
public class WorkflowValidator
{
    /// <summary>
    /// 校验工作流定义，失败时抛出 <see cref="WorkflowValidationException"/>（包含全部错误）.
    /// </summary>
    public void Validate(WorkflowDefinition definition)
    {
        var errors = GetErrors(definition);
        if (errors.Count > 0)
        {
            throw new WorkflowValidationException(errors);
        }
    }

    /// <summary>
    /// 获取工作流定义的全部验证错误，空列表表示合法.
    /// </summary>
    public IReadOnlyList<string> GetErrors(WorkflowDefinition definition)
    {
        var errors = new List<string>();
        if (definition == null)
        {
            errors.Add("工作流定义不能为空");
            return errors;
        }

        var nodes = definition.Nodes ?? new List<NodeDefinition>();
        var connections = definition.Connections ?? new List<ConnectionDefinition>();
        if (nodes.Count == 0)
        {
            errors.Add("工作流必须包含至少一个节点");
            return errors;
        }

        // 节点基础信息：Key 非空且唯一、Type 非空
        var nodeMap = new Dictionary<string, NodeDefinition>();
        foreach (var node in nodes)
        {
            if (string.IsNullOrWhiteSpace(node.Key))
            {
                errors.Add("存在 Key 为空的节点");
                continue;
            }

            if (nodeMap.ContainsKey(node.Key))
            {
                errors.Add($"节点 Key 重复：{node.Key}");
                continue;
            }

            if (string.IsNullOrWhiteSpace(node.Type))
            {
                errors.Add($"节点 {node.Key} 缺少节点类型（type）");
            }

            nodeMap[node.Key] = node;
        }

        // Start / End 节点约束
        var startNodes = nodes.Where(n => n.Type == NodeTypes.Start).ToList();
        var endNodes = nodes.Where(n => n.Type == NodeTypes.End).ToList();
        if (startNodes.Count == 0)
        {
            errors.Add("工作流必须包含一个开始节点（start）");
        }
        else if (startNodes.Count > 1)
        {
            errors.Add($"工作流只能包含一个开始节点，发现 {startNodes.Count} 个：{string.Join(", ", startNodes.Select(n => n.Key))}");
        }

        if (endNodes.Count == 0)
        {
            errors.Add("工作流必须包含至少一个结束节点（end）");
        }

        // 连接约束（Key 重复的节点只保留首个，重复错误已在上方报告，此处不能因重复 Key 抛异常）
        var outgoing = nodes
            .Where(n => !string.IsNullOrWhiteSpace(n.Key))
            .DistinctBy(n => n.Key)
            .ToDictionary(n => n.Key, _ => new List<ConnectionDefinition>());
        var seenConnectionIds = new HashSet<string>();
        foreach (var conn in connections)
        {
            if (string.IsNullOrWhiteSpace(conn.Id))
            {
                errors.Add("存在 Id 为空的连接");
            }
            else if (!seenConnectionIds.Add(conn.Id))
            {
                errors.Add($"连接 Id 重复：{conn.Id}");
            }

            if (conn.Source == conn.Target)
            {
                errors.Add($"连接 {conn.Id} 不能连接节点自身：{conn.Source}");
            }

            if (!nodeMap.ContainsKey(conn.Source))
            {
                errors.Add($"连接 {conn.Id} 的源节点不存在：{conn.Source}");
                continue;
            }

            if (!nodeMap.ContainsKey(conn.Target))
            {
                errors.Add($"连接 {conn.Id} 的目标节点不存在：{conn.Target}");
                continue;
            }

            outgoing[conn.Source].Add(conn);

            var sourceType = nodeMap[conn.Source].Type;
            if (sourceType == NodeTypes.Condition)
            {
                if (string.IsNullOrWhiteSpace(conn.Condition))
                {
                    errors.Add($"条件节点 {conn.Source} 的出边 {conn.Id} 缺少 condition 标记（true/false）");
                }
                else if (conn.Condition != "true" && conn.Condition != "false")
                {
                    errors.Add($"条件节点 {conn.Source} 的出边 {conn.Id} condition 只能为 true/false，当前为 {conn.Condition}");
                }
            }
            else if (!string.IsNullOrWhiteSpace(conn.Condition))
            {
                errors.Add($"非条件节点 {conn.Source} 的出边 {conn.Id} 不能设置 condition");
            }
        }

        // 条件节点的出边不允许重复条件、且真/假分支都需要存在
        foreach (var conditionNode in nodes.Where(n => n.Type == NodeTypes.Condition))
        {
            var edges = outgoing[conditionNode.Key];
            var duplicated = edges.GroupBy(e => e.Condition).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            if (duplicated.Count > 0)
            {
                errors.Add($"条件节点 {conditionNode.Key} 存在重复的 condition 出边：{string.Join(", ", duplicated)}");
            }

            foreach (var branch in new[] { "true", "false" })
            {
                if (edges.All(e => e.Condition != branch))
                {
                    errors.Add($"条件节点 {conditionNode.Key} 缺少 condition={branch} 的出边");
                }
            }
        }

        // End 节点不能有出边；非 End 节点必须有出边
        foreach (var node in nodes)
        {
            if (node.Type == NodeTypes.End && outgoing[node.Key].Count > 0)
            {
                errors.Add($"结束节点 {node.Key} 不能有出边");
            }

            if (node.Type != NodeTypes.End && outgoing[node.Key].Count == 0)
            {
                errors.Add($"节点 {node.Key} 没有下游节点（孤立节点）");
            }
        }

        // 图连通性：从 start 出发可达所有节点
        if (startNodes.Count == 1 && nodeMap.Count == nodes.Count(n => !string.IsNullOrWhiteSpace(n.Key)))
        {
            var startKey = startNodes[0].Key;
            var visited = new HashSet<string>();
            var queue = new Queue<string>();
            queue.Enqueue(startKey);
            visited.Add(startKey);
            while (queue.Count > 0)
            {
                foreach (var edge in outgoing[queue.Dequeue()])
                {
                    if (visited.Add(edge.Target))
                    {
                        queue.Enqueue(edge.Target);
                    }
                }
            }

            foreach (var node in nodes)
            {
                if (!visited.Contains(node.Key))
                {
                    errors.Add($"节点 {node.Key} 无法从开始节点到达");
                }
            }

            // 环检测：DFS 三色标记
            var state = new Dictionary<string, int>();
            bool HasCycle(string key)
            {
                state[key] = 1;
                foreach (var edge in outgoing[key])
                {
                    var next = state.TryGetValue(edge.Target, out var s) ? s : 0;
                    if (next == 1 || (next == 0 && HasCycle(edge.Target)))
                    {
                        return true;
                    }
                }

                state[key] = 2;
                return false;
            }

            if (HasCycle(startKey))
            {
                errors.Add("工作流存在环，不允许循环连接");
            }
        }

        // 输入绑定校验：variable 引用的节点必须存在，且必须是当前节点的上游（避免引用未来数据）
        foreach (var node in nodes)
        {
            var ancestors = CollectAncestors(node.Key, connections);
            foreach (var (fieldName, binding) in node.Inputs)
            {
                if (binding == null)
                {
                    errors.Add($"节点 {node.Key} 的输入字段 {fieldName} 绑定为空");
                    continue;
                }

                if (binding.ExpressionType != ExpressionType.Variable)
                {
                    continue;
                }

                var prefix = binding.Value.Split('.').FirstOrDefault();
                if (string.IsNullOrEmpty(prefix))
                {
                    errors.Add($"节点 {node.Key} 的输入字段 {fieldName} 变量引用格式无效：{binding.Value}");
                    continue;
                }

                if (prefix == "sys" || prefix == "input")
                {
                    continue;
                }

                if (!nodeMap.ContainsKey(prefix))
                {
                    errors.Add($"节点 {node.Key} 的输入字段 {fieldName} 引用了不存在的节点：{binding.Value}");
                }
                else if (!ancestors.Contains(prefix))
                {
                    errors.Add($"节点 {node.Key} 的输入字段 {fieldName} 引用了非上游节点：{binding.Value}（只有上游节点的输出可以被引用）");
                }
            }
        }

        return errors;
    }

    /// <summary>
    /// 收集指定节点的全部祖先节点 Key.
    /// </summary>
    private static HashSet<string> CollectAncestors(string nodeKey, List<ConnectionDefinition> connections)
    {
        var incoming = connections
            .Where(c => c.Target == nodeKey)
            .Select(c => c.Source)
            .ToList();

        var ancestors = new HashSet<string>();
        var queue = new Queue<string>(incoming);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!ancestors.Add(current))
            {
                continue;
            }

            foreach (var parent in connections.Where(c => c.Target == current).Select(c => c.Source))
            {
                queue.Enqueue(parent);
            }
        }

        return ancestors;
    }
}
