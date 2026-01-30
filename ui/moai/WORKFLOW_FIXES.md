# 工作流配置接口修复总结

## 修复内容

### 1. 创建 Zustand Store (`useWorkflowStore.tsx`)

**主要功能：**
- 从 API 加载工作流配置数据
- 处理新的后端数据结构（`functionDesignDraft` 现在是 `NodeDesign[]` 数组）
- 将 `NodeDesign` 转换为内部 `BackendNodeData` 格式
- 解析 `uiDesignDraft` JSON 字符串为画布数据
- 提供节点配置更新方法

**关键转换函数：**
```typescript
// API NodeDesign → 内部 BackendNodeData
function convertNodeDesignToBackendNode(nodeDesign: NodeDesign): BackendNodeData

// 内部 BackendNodeData → API NodeDesign
function convertBackendNodeToNodeDesign(node: BackendNodeData): NodeDesign
```

**API 调用：**
```typescript
const response = await client.api.team.workflowapp.config.post({
  appId,
  teamId,
  includeDraft: true,
});
```

### 2. 数据结构映射

#### 后端 API 响应结构
```typescript
QueryWorkflowDefinitionCommandResponse {
  functionDesignDraft: NodeDesign[]  // 节点设计数组（不再是 JSON 字符串）
  uiDesignDraft: string              // UI 设计 JSON 字符串
  name: string
  description: string
  // ...
}

NodeDesign {
  nodeKey: string                    // 节点 ID
  nodeType: NodeType                 // 节点类型
  name: string                       // 节点名称
  description: string                // 节点描述
  fieldDesigns: KeyValueOfStringAndFieldDesign[]  // 字段配置
  nextNodeKeys: string[]             // 下游节点列表
}
```

#### 内部数据结构
```typescript
BackendNodeData {
  id: string                         // 从 nodeKey 映射
  type: NodeType                     // 从 nodeType 映射
  name: string                       // 从 name 映射
  description: string                // 从 description 映射
  config: {
    inputFields: ExtendedFieldDefine[]   // 从 fieldDesigns 转换
    outputFields: ExtendedFieldDefine[]  // 根据节点类型生成
    settings: {
      nextNodeKeys: string[]         // 从 nextNodeKeys 映射
    }
  }
}
```

### 3. 修复 WorkflowConfig.tsx

**移除内联样式：**
- 将所有 `style={{...}}` 替换为 CSS 类名
- 添加 `.workflow-loading-container` 和 `.workflow-error-container` 类
- 添加 `.workflow-config-title` 类

**修复未使用变量：**
- 移除 `handleRun` 中未使用的 `currentDoc` 变量

### 4. 更新 CSS 样式

**WorkflowCanvas.css：**
```css
.workflow-loading-container,
.workflow-error-container {
  display: flex;
  justify-content: center;
  align-items: center;
  height: 100vh;
}
```

**WorkflowConfig.css：**
```css
.workflow-config-title {
  margin: 0 !important;
}
```

### 5. 连接关系处理

从 `nextNodeKeys` 推导边的连接关系：
```typescript
const backendEdges = backendNodes.flatMap((node, index) => {
  const nextNodeKeys = node.config.settings.nextNodeKeys as string[] || [];
  return nextNodeKeys.map((targetNodeKey, edgeIndex) => ({
    id: `edge_${node.id}_${targetNodeKey}_${edgeIndex}`,
    sourceNodeId: node.id,
    targetNodeId: targetNodeKey,
    // ...
  }));
});
```

### 6. 画布数据生成

画布数据生成逻辑根据不同情况处理：

**情况 1：有 uiDesignDraft 数据**
```typescript
// 直接解析 JSON 字符串
canvasData = parseUiDesign(response.uiDesignDraft);
```

**情况 2：没有 uiDesignDraft，但有 functionDesignDraft（节点数据）**
```typescript
// 自动生成默认布局
canvasData = {
  nodes: backendNodes.map((node, index) => ({
    id: node.id,
    type: node.type,
    position: { 
      x: 100 + (index % 3) * 300, 
      y: 100 + Math.floor(index / 3) * 200 
    },
    // ...
  })),
  // ...
};
```

**情况 3：functionDesignDraft 和 uiDesignDraft 都为空（新建工作流）**
```typescript
// 返回空画布
canvasData = createDefaultCanvasData(); // { nodes: [], edges: [], viewport: {...} }
```

这样确保新建的工作流应用第一次打开时显示空画布，用户可以从节点面板拖拽节点到画布上开始设计。

## 数据流程

```
1. 用户访问工作流配置页面
   ↓
2. WorkflowConfig 组件调用 store.loadFromApi(appId, teamId)
   ↓
3. API 返回 QueryWorkflowDefinitionCommandResponse
   ├─ functionDesignDraft: NodeDesign[]
   └─ uiDesignDraft: string (JSON)
   ↓
4. 转换数据
   ├─ NodeDesign[] → BackendNodeData[]
   ├─ 从 nextNodeKeys 生成 edges
   └─ 解析 uiDesignDraft → CanvasWorkflowData
   ↓
5. 合并数据生成 FlowGram 编辑器格式
   ├─ toEditorFormat(backend, canvas)
   └─ 返回 WorkflowJSON
   ↓
6. 渲染画布
   ├─ 显示节点（使用 functionDesignDraft 的属性）
   ├─ 显示连接
   └─ 应用 uiDesignDraft 的位置信息
```

## 节点渲染逻辑

节点内容来自 `functionDesignDraft`（BackendNodeData）：
- **标题**: `node.name`
- **描述**: `node.description`
- **输入字段**: `node.config.inputFields`
- **输出字段**: `node.config.outputFields`

节点位置来自 `uiDesignDraft`（CanvasNodeData）：
- **位置**: `node.position { x, y }`
- **UI 状态**: `node.ui { expanded, selected, highlighted }`

## 字段类型支持

支持嵌套字段（Map 类型）：
```typescript
ExtendedFieldDefine {
  fieldName: string
  fieldType: FieldType  // 包括 Map
  children?: ExtendedFieldDefine[]  // 子字段数组
  expressionType: FieldExpressionType
  value: any
}
```

在节点渲染时，Map 类型字段会递归显示子字段。

## 待实现功能

1. **保存工作流 API**
   - 需要实现 `saveToApi()` 中的实际 API 调用
   - 将内部数据转换回 API 格式

2. **其他节点配置组件**
   - 目前只实现了 StartNodeConfig
   - 需要为其他节点类型创建配置组件

3. **字段类型推断**
   - 当前默认使用 `FieldType.String`
   - 需要根据实际数据推断正确的字段类型

4. **输出字段生成**
   - 根据节点类型动态生成 outputFields

## 测试建议

1. **加载测试**
   ```typescript
   // 测试空工作流
   // 测试包含多个节点的工作流
   // 测试包含 Map 类型字段的节点
   ```

2. **数据转换测试**
   ```typescript
   // 验证 NodeDesign → BackendNodeData 转换
   // 验证 nextNodeKeys → edges 转换
   // 验证 uiDesignDraft 解析
   ```

3. **画布渲染测试**
   ```typescript
   // 验证节点正确显示
   // 验证连接正确显示
   // 验证子字段正确显示
   ```

## 错误处理

所有 API 调用都使用 `proxyRequestError` 处理错误：
```typescript
try {
  // API 调用
} catch (error) {
  console.error('加载工作流失败:', error);
  proxyRequestError(error, messageApi, '加载工作流失败');
}
```

## 文件清单

### 新建文件
- `src/components/team/apps/workflow/useWorkflowStore.tsx` - Zustand store
- `src/components/team/apps/workflow/README.md` - 模块文档
- `WORKFLOW_FIXES.md` - 本文档

### 修改文件
- `src/components/team/apps/workflow/WorkflowConfig.tsx` - 移除内联样式，修复变量
- `src/components/team/apps/workflow/WorkflowCanvas.css` - 添加容器样式
- `src/components/team/apps/workflow/WorkflowConfig.css` - 添加标题样式
- `src/components/team/apps/workflow/workflowConverter.ts` - 移除注释

## 使用示例

```typescript
// 在组件中使用
const store = useWorkflowStore();

// 加载工作流
useEffect(() => {
  if (appId && teamId) {
    store.loadFromApi(appId, teamId);
  }
}, [appId, teamId]);

// 更新节点配置
const handleSaveConfig = (config: any) => {
  store.updateNodeConfig(nodeId, config);
};

// 保存工作流
const handleSave = async () => {
  const success = await store.saveToApi();
  if (success) {
    message.success('保存成功');
  }
};
```
