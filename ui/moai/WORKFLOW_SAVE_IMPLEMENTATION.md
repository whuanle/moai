# 工作流保存功能实现

## 概述

实现了工作流配置的保存功能，调用 `/api/team/workflowapp/update` 接口将画布数据和节点配置保存到后端。

## API 接口

### PUT /api/team/workflowapp/update

**请求体：UpdateWorkflowDefinitionCommand**
```typescript
{
  appId: string;              // 应用 ID
  teamId: number;             // 团队 ID
  name: string;               // 工作流名称
  description: string;        // 工作流描述
  avatar?: string;            // 工作流头像 ObjectKey（可选）
  nodes: NodeDesign[];        // 节点设计列表
  uiDesignDraft: string;      // UI 设计草稿（JSON 字符串）
}
```

**NodeDesign 结构：**
```typescript
{
  nodeKey: string;            // 节点唯一标识
  nodeType: NodeType;         // 节点类型
  name: string;               // 节点名称
  description: string;        // 节点描述
  fieldDesigns: KeyValueOfStringAndFieldDesign[];  // 字段设计
  nextNodeKeys: string[];     // 下游节点列表
}
```

## 实现流程

### 1. 编辑器内容变更监听

在 `useEditorProps.tsx` 中添加 `onContentChange` 回调：

```typescript
export const useEditorProps = (
  initialData: WorkflowJSON,
  onSelectNode: (nodeId: string) => void,
  onSelectNodeType: (nodeType: NodeType) => void,
  onContentChange?: (data: WorkflowJSON) => void  // 新增参数
)
```

当编辑器内容变化时，触发回调：
```typescript
onContentChange(ctx, event) {
  if (onContentChange) {
    const currentData = ctx.document.toJSON();
    onContentChange(currentData);
  }
}
```

### 2. Store 同步方法

在 `useWorkflowStore.tsx` 中添加 `syncFromEditor` 方法：

```typescript
syncFromEditor: (editorData: any) => {
  const state = get();
  const { backend: newBackend, canvas: newCanvas } = fromEditorFormat(editorData);
  
  // 合并保留现有配置
  const mergedBackend = {
    ...state.backend,
    nodes: newBackend.nodes!.map(newNode => {
      const existingNode = state.backend.nodes.find(n => n.id === newNode.id);
      return existingNode ? {
        ...existingNode,
        name: newNode.name,
        description: newNode.description,
      } : newNode;
    }),
    edges: newBackend.edges || [],
  };
  
  set({
    backend: mergedBackend,
    canvas: newCanvas,
    isDirty: true,
  });
}
```

### 3. 保存 API 实现

在 `useWorkflowStore.tsx` 的 `saveToApi` 方法中：

```typescript
saveToApi: async () => {
  const state = get();
  set({ isSaving: true });
  
  try {
    const client = GetApiClient();
    
    // 转换为 API 格式
    const nodeDesigns = state.backend.nodes.map(convertBackendNodeToNodeDesign);
    const uiDesign = toUiDesign(state.canvas);
    
    // 构建更新命令
    const updateCommand = {
      appId: state.appId,
      teamId: state.teamId,
      name: state.backend.name,
      description: state.backend.description,
      avatar: undefined,
      nodes: nodeDesigns,
      uiDesignDraft: uiDesign,
    };
    
    // 调用保存 API
    await client.api.team.workflowapp.update.put(updateCommand);
    
    set({ isSaving: false, isDirty: false });
    return true;
  } catch (error) {
    console.error('保存工作流失败:', error);
    set({ isSaving: false });
    throw error;
  }
}
```

### 4. 保存按钮处理

在 `WorkflowConfig.tsx` 的 `WorkflowTools` 组件中：

```typescript
const handleSave = async () => {
  try {
    // 从编辑器获取最新数据并同步到 store
    const currentData = document.toJSON();
    store.syncFromEditor(currentData);
    
    // 保存到 API
    const success = await store.saveToApi();
    
    if (success) {
      messageApi.success("工作流已保存");
    }
  } catch (error) {
    console.error("保存失败:", error);
    proxyRequestError(error, messageApi, "保存工作流失败");
  }
};
```

## 数据转换

### BackendNodeData → NodeDesign

```typescript
function convertBackendNodeToNodeDesign(node: BackendNodeData): NodeDesign {
  const fieldDesigns: KeyValueOfStringAndFieldDesign[] = node.config.inputFields.map(field => ({
    key: field.fieldName,
    value: {
      fieldName: field.fieldName,
      expressionType: field.expressionType || FieldExpressionType.Constant,
      value: field.value?.toString() || '',
    } as FieldDesign,
  }));
  
  return {
    nodeKey: node.id,
    nodeType: node.type as any,
    name: node.name,
    description: node.description,
    fieldDesigns,
    nextNodeKeys: (node.config.settings.nextNodeKeys as string[]) || [],
  };
}
```

### CanvasWorkflowData → JSON String

```typescript
function toUiDesign(canvas: CanvasWorkflowData): string {
  return JSON.stringify({
    nodes: canvas.nodes.map(node => ({
      id: node.id,
      type: node.type,
      position: node.position,
      ui: node.ui,
      title: node.title,
      content: node.content
    })),
    edges: canvas.edges.map(edge => ({
      id: edge.id,
      sourceNodeId: edge.sourceNodeId,
      targetNodeId: edge.targetNodeId,
      ui: edge.ui
    })),
    viewport: canvas.viewport
  });
}
```

## UI 状态管理

### isDirty 标志

- **设置时机**：
  - 编辑器内容变更时
  - 节点配置更新时
  - 从编辑器同步数据时

- **清除时机**：
  - 保存成功后

### 保存按钮状态

```typescript
<Button 
  type="primary" 
  icon={<SaveOutlined />} 
  onClick={handleSave}
  loading={store.isSaving}      // 保存中显示加载状态
>
  保存
</Button>
```

**注意**：保存按钮始终可用，不受 `isDirty` 限制。这样用户可以随时保存工作流，即使没有做任何更改。

### 状态标签

```typescript
{store.isDraft && <Tag color="orange">草稿</Tag>}
{store.isDirty && <Tag color="red">未保存</Tag>}
```

## 错误处理

使用 `proxyRequestError` 统一处理错误：

```typescript
try {
  await store.saveToApi();
} catch (error) {
  console.error("保存失败:", error);
  proxyRequestError(error, messageApi, "保存工作流失败");
}
```

## 未保存更改警告

在 `WorkflowConfig.tsx` 中添加浏览器关闭警告：

```typescript
useEffect(() => {
  const handleBeforeUnload = (e: BeforeUnloadEvent) => {
    if (store.isDirty) {
      e.preventDefault();
      e.returnValue = '您有未保存的更改，确定要离开吗？';
    }
  };

  window.addEventListener('beforeunload', handleBeforeUnload);
  return () => window.removeEventListener('beforeunload', handleBeforeUnload);
}, [store.isDirty]);
```

## 保存流程图

```
用户编辑画布
  ↓
onContentChange 触发
  ↓
设置 isDirty = true
  ↓
用户点击保存按钮
  ↓
从编辑器获取最新数据 (document.toJSON())
  ↓
syncFromEditor 同步到 store
  ↓
转换数据格式
  ├─ BackendNodeData[] → NodeDesign[]
  └─ CanvasWorkflowData → JSON String
  ↓
构建 UpdateWorkflowDefinitionCommand
  ↓
调用 PUT /api/team/workflowapp/update
  ↓
保存成功
  ├─ 设置 isDirty = false
  ├─ 设置 isSaving = false
  └─ 显示成功消息
```

## 测试场景

### 场景 1: 添加节点后保存
1. 从节点面板拖拽节点到画布
2. 保存按钮变为可用（isDirty = true）
3. 点击保存
4. 验证节点数据正确保存到后端

### 场景 2: 修改节点配置后保存
1. 双击节点打开配置面板
2. 修改节点配置
3. 保存配置
4. 点击保存按钮
5. 验证配置正确保存

### 场景 3: 移动节点后保存
1. 拖动节点改变位置
2. 点击保存
3. 验证位置信息保存到 uiDesignDraft

### 场景 4: 保存失败处理
1. 模拟网络错误
2. 点击保存
3. 验证错误消息显示
4. 验证 isDirty 保持为 true

## 相关文件

- `src/components/team/apps/workflow/useWorkflowStore.tsx` - Store 实现
- `src/components/team/apps/workflow/WorkflowConfig.tsx` - 保存按钮和状态管理
- `src/components/team/apps/workflow/useEditorProps.tsx` - 编辑器配置和内容变更监听
- `src/components/team/apps/workflow/workflowConverter.ts` - 数据转换工具
- `src/apiClient/api/team/workflowapp/update/index.ts` - API 客户端

## 注意事项

1. **数据合并**：保存时合并编辑器数据和现有配置，避免丢失配置信息
2. **错误处理**：使用 `proxyRequestError` 统一处理 API 错误
3. **状态同步**：编辑器变更时及时更新 store 状态
4. **用户提示**：保存成功/失败都要给用户明确反馈
5. **未保存警告**：离开页面前提示用户保存更改
