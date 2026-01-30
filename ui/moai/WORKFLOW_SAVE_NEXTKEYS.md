# 工作流保存 nextNodeKeys 实现确认

## 问题描述
需要确保在保存流程定义时，节点的 `nextNodeKeys` 字段被正确填充。

## 当前实现分析

### 1. 数据转换流程 (`utils.ts`)

在 `toApiFormat` 函数中，已经正确实现了 `nextNodeKeys` 的构建：

```typescript
export function toApiFormat(workflow: WorkflowData): {
  functionDesign: ApiNodeDesign[];
  uiDesign: string;
} {
  // 构建节点的下游连接映射
  const nextNodeKeysMap = new Map<string, string[]>();
  workflow.edges.forEach(edge => {
    const nextKeys = nextNodeKeysMap.get(edge.source) || [];
    nextKeys.push(edge.target);
    nextNodeKeysMap.set(edge.source, nextKeys);
  });
  
  // 转换节点
  const functionDesign: ApiNodeDesign[] = workflow.nodes.map(node => ({
    nodeKey: node.id,
    nodeType: node.type,
    name: node.name,
    description: node.description,
    inputFields: node.config.inputFields,
    outputFields: node.config.outputFields,
    fieldDesigns: node.config.settings,
    nextNodeKeys: nextNodeKeysMap.get(node.id) || [], // ✅ 正确填充
  }));
  
  // ...
}
```

### 2. 保存流程 (`api.ts`)

```typescript
async save(appId: string, teamId: number, workflow: WorkflowData): Promise<void> {
  const client = GetApiClient();
  
  // 转换内部格式到 API 格式
  const { functionDesign, uiDesign } = toApiFormat(workflow);
  
  await client.api.team.workflowapp.update.post({
    appId,
    teamId,
    name: workflow.name,
    description: workflow.description,
    functionDesign: JSON.stringify(functionDesign), // ✅ 包含 nextNodeKeys
    uiDesign,
  });
}
```

### 3. 数据流示例

假设有以下工作流：
- 节点 A (start) → 节点 B (aiChat)
- 节点 B (aiChat) → 节点 C (end)

**内部格式 (WorkflowData):**
```json
{
  "nodes": [
    { "id": "start_1", "type": "start", ... },
    { "id": "aiChat_1", "type": "aiChat", ... },
    { "id": "end_1", "type": "end", ... }
  ],
  "edges": [
    { "id": "edge_start_1_aiChat_1", "source": "start_1", "target": "aiChat_1" },
    { "id": "edge_aiChat_1_end_1", "source": "aiChat_1", "target": "end_1" }
  ]
}
```

**转换后的 API 格式 (functionDesign):**
```json
[
  {
    "nodeKey": "start_1",
    "nodeType": "start",
    "nextNodeKeys": ["aiChat_1"]  // ✅ 从 edges 提取
  },
  {
    "nodeKey": "aiChat_1",
    "nodeType": "aiChat",
    "nextNodeKeys": ["end_1"]  // ✅ 从 edges 提取
  },
  {
    "nodeKey": "end_1",
    "nodeType": "end",
    "nextNodeKeys": []  // ✅ 无下游节点
  }
]
```

## 结论

✅ **实现已完成且正确**

当前代码已经正确实现了 `nextNodeKeys` 的填充：

1. **数据来源**: 从 `workflow.edges` 数组中提取连接关系
2. **映射构建**: 使用 `Map` 结构高效构建每个节点的下游节点列表
3. **数据填充**: 在转换为 API 格式时，为每个节点正确填充 `nextNodeKeys`
4. **空值处理**: 对于没有下游连接的节点，返回空数组 `[]`

## 验证建议

如果需要验证实现是否正常工作，可以：

1. **添加日志**: 在 `toApiFormat` 函数中添加 console.log 查看生成的数据
2. **网络监控**: 使用浏览器开发者工具查看保存请求的 payload
3. **单元测试**: 编写测试用例验证不同连接场景下的 nextNodeKeys 生成

```typescript
// 测试示例
const testWorkflow: WorkflowData = {
  nodes: [
    { id: 'A', type: NodeType.Start, ... },
    { id: 'B', type: NodeType.AiChat, ... },
    { id: 'C', type: NodeType.End, ... }
  ],
  edges: [
    { id: 'e1', source: 'A', target: 'B' },
    { id: 'e2', source: 'B', target: 'C' }
  ]
};

const result = toApiFormat(testWorkflow);
console.log(result.functionDesign);
// 预期输出:
// [
//   { nodeKey: 'A', nextNodeKeys: ['B'] },
//   { nodeKey: 'B', nextNodeKeys: ['C'] },
//   { nodeKey: 'C', nextNodeKeys: [] }
// ]
```
