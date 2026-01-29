# 工作流编排核心架构设计

## 设计理念

工作流编排系统采用**数据分离**的架构设计，将画布 UI 数据和后端业务逻辑数据分开管理，通过统一的状态管理和约束规则确保数据一致性和业务正确性。

## 核心概念

### 1. 数据分离

#### 后端逻辑数据 (Backend Data)
存储工作流的业务逻辑和执行配置：
- 节点配置（输入/输出字段、执行参数）
- 连接逻辑（字段映射、条件表达式）
- 执行策略（超时、重试、错误处理）
- 全局变量和元数据

#### 画布 UI 数据 (Canvas Data)
存储工作流的可视化展示信息：
- 节点位置和尺寸
- UI 状态（展开/折叠、选中、高亮）
- 连接样式（线型、颜色）
- 视图状态（缩放、偏移）

### 2. 约束规则

系统通过 `NodeConstraints` 定义节点的约束规则：

```typescript
interface NodeConstraints {
  minCount: number;      // 最小数量
  maxCount: number;      // 最大数量（-1 表示无限制）
  deletable: boolean;    // 是否可删除
  copyable: boolean;     // 是否可复制
  requireConnection: boolean; // 是否必须连接
}
```

#### 特殊节点约束

**开始节点 (Start)**
- 必须存在且唯一（minCount: 1, maxCount: 1）
- 不可删除、不可复制
- 必须有输出连接

**结束节点 (End)**
- 必须存在且唯一（minCount: 1, maxCount: 1）
- 不可删除、不可复制
- 必须有输入连接

**其他节点**
- 数量不限（maxCount: -1）
- 可删除、可复制
- 必须有输入和输出连接

### 3. 验证机制

系统提供多层验证：

#### 结构验证
- ✅ 必须有且仅有一个开始节点
- ✅ 必须有且仅有一个结束节点
- ✅ 所有节点必须正确连接（除特殊节点外）
- ✅ 不允许形成环路
- ✅ 不允许重复连接

#### 配置验证
- ✅ 必填字段不能为空
- ✅ 字段类型必须匹配
- ✅ 连接的字段类型必须兼容

#### 数据一致性验证
- ✅ 后端数据和画布数据节点数量一致
- ✅ 节点 ID 在两个数据集中都存在
- ✅ 连接引用的节点必须存在

## 文件结构

```
workflow/
├── ARCHITECTURE.md           # 架构设计文档（本文件）
├── types.ts                  # 基础类型定义
├── workflowStore.ts          # 核心数据结构和接口定义
├── useWorkflowStore.ts       # Zustand 状态管理实现
├── workflowConverter.ts      # 数据格式转换工具
├── nodeTemplates.ts          # 节点模板定义
├── nodeRegistries.tsx        # 节点注册配置
├── WorkflowConfig.tsx        # 主组件
├── NodePanel.tsx             # 节点面板
└── useEditorProps.tsx        # 编辑器配置
```

## 核心 API

### WorkflowStore

#### 节点操作
```typescript
// 添加节点（自动验证约束）
addNode(type: NodeType, position: CanvasPosition): string | { error: string }

// 删除节点（自动验证约束）
deleteNode(nodeId: string): boolean | { error: string }

// 复制节点（自动验证约束）
copyNode(nodeId: string, offset: CanvasPosition): string | { error: string }

// 更新节点位置（仅更新画布数据）
updateNodePosition(nodeId: string, position: CanvasPosition): void

// 更新节点配置（仅更新后端数据）
updateNodeConfig(nodeId: string, config: Partial<BackendNodeData['config']>): void

// 更新节点 UI 状态（仅更新画布数据）
updateNodeUI(nodeId: string, ui: Partial<CanvasNodeData['ui']>): void
```

#### 连接操作
```typescript
// 添加连接（自动验证）
addEdge(sourceNodeId: string, targetNodeId: string): string | { error: string }

// 删除连接
deleteEdge(edgeId: string): void

// 更新连接配置
updateEdgeConfig(edgeId: string, config: Partial<BackendEdgeData>): void
```

#### 验证操作
```typescript
// 验证整个工作流
validate(): ValidationError[]

// 检查是否可以添加节点
canAddNode(type: NodeType): boolean | { error: string }

// 检查是否可以删除节点
canDeleteNode(nodeId: string): boolean | { error: string }

// 检查是否可以添加连接
canAddEdge(sourceNodeId: string, targetNodeId: string): boolean | { error: string }
```

#### 数据转换
```typescript
// 导出为后端格式
toWorkflowData(): BackendWorkflowData

// 从后端数据加载
loadFromBackend(data: BackendWorkflowData): void

// 导出为 JSON
exportToJSON(): string

// 从 JSON 导入
importFromJSON(json: string): void
```

### WorkflowConverter

```typescript
// 转换为编辑器格式
toEditorFormat(backend: BackendWorkflowData, canvas: CanvasWorkflowData): WorkflowJSON

// 从编辑器格式转换
fromEditorFormat(editorData: WorkflowJSON): { backend, canvas }

// 同步编辑器变更
syncEditorChanges(editorData, currentBackend, currentCanvas): { backend, canvas }

// 验证数据一致性
validateDataConsistency(backend, canvas): { valid, errors }
```

## 使用示例

### 1. 初始化工作流

```typescript
import { useWorkflowStore } from './useWorkflowStore';

function WorkflowEditor() {
  const store = useWorkflowStore();
  
  useEffect(() => {
    // 从后端加载工作流
    const workflowData = await fetchWorkflow(workflowId);
    store.loadFromBackend(workflowData);
  }, []);
}
```

### 2. 添加节点

```typescript
function handleAddNode(type: NodeType, position: CanvasPosition) {
  const result = store.addNode(type, position);
  
  if (typeof result === 'string') {
    // 成功，result 是新节点 ID
    message.success('节点已添加');
  } else {
    // 失败，result.error 是错误信息
    message.error(result.error);
  }
}
```

### 3. 删除节点

```typescript
function handleDeleteNode(nodeId: string) {
  const result = store.deleteNode(nodeId);
  
  if (result === true) {
    message.success('节点已删除');
  } else {
    message.error(result.error);
  }
}
```

### 4. 验证工作流

```typescript
function handleSave() {
  const errors = store.validate();
  
  if (errors.length > 0) {
    // 显示验证错误
    errors.forEach(error => {
      message.error(error.message);
    });
    return;
  }
  
  // 保存工作流
  await store.save();
}
```

### 5. 与画布编辑器集成

```typescript
import { toEditorFormat, syncEditorChanges } from './workflowConverter';

function WorkflowCanvas() {
  const store = useWorkflowStore();
  
  // 转换为编辑器格式
  const editorData = toEditorFormat(store.backend, store.canvas);
  
  // 监听编辑器变更
  const handleContentChange = (ctx, event) => {
    const currentDoc = ctx.document.toJSON();
    const { backend, canvas } = syncEditorChanges(
      currentDoc,
      store.backend,
      store.canvas
    );
    
    // 更新 store（需要添加批量更新方法）
    // store.updateBatch({ backend, canvas });
  };
  
  return (
    <FreeLayoutEditorProvider
      initialData={editorData}
      onContentChange={handleContentChange}
    />
  );
}
```

## 扩展指南

### 添加新节点类型

1. 在 `types.ts` 中添加节点类型枚举
2. 在 `nodeTemplates.ts` 中定义节点模板
3. 在 `workflowStore.ts` 中添加约束规则
4. 在 `nodeRegistries.tsx` 中配置节点注册

### 添加自定义验证规则

在 `useWorkflowStore.ts` 的 `validate` 方法中添加：

```typescript
validate: () => {
  const errors: ValidationError[] = [];
  
  // 现有验证...
  
  // 添加自定义验证
  if (customCondition) {
    errors.push({
      type: ValidationErrorType.Custom,
      message: '自定义错误信息',
      nodeId: 'xxx',
    });
  }
  
  return errors;
}
```

### 添加节点配置面板

创建节点配置组件，使用 `updateNodeConfig` 更新配置：

```typescript
function NodeConfigPanel({ nodeId }: { nodeId: string }) {
  const store = useWorkflowStore();
  const node = store.backend.nodes.find(n => n.id === nodeId);
  
  const handleSave = (config) => {
    store.updateNodeConfig(nodeId, { settings: config });
  };
  
  return <Form onFinish={handleSave}>...</Form>;
}
```

## 最佳实践

1. **始终通过 Store 操作数据**：不要直接修改 backend 或 canvas 数据
2. **操作前先验证**：使用 `canAddNode`、`canDeleteNode` 等方法检查
3. **及时验证工作流**：在关键操作后调用 `validate()` 方法
4. **保持数据一致性**：使用 `validateDataConsistency` 定期检查
5. **使用转换工具**：通过 `workflowConverter` 在不同格式间转换
6. **错误处理**：所有操作都应处理可能的错误返回值

## 性能优化

1. **批量更新**：多个操作可以合并为一次状态更新
2. **选择性渲染**：使用 Zustand 的选择器避免不必要的重渲染
3. **延迟验证**：在用户停止操作后再进行完整验证
4. **增量同步**：只同步变更的节点和连接

## 未来规划

- [ ] 支持撤销/重做
- [ ] 支持工作流版本管理
- [ ] 支持节点分组
- [ ] 支持子工作流
- [ ] 支持实时协作
- [ ] 支持工作流模板
- [ ] 支持自动布局算法
- [ ] 支持工作流调试和断点
