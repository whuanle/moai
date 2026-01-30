# 工作流空画布处理

## 问题描述

创建流程应用后，第一次打开时 `functionDesignDraft` 和 `uiDesignDraft` 都为空，需要正确显示空画布。

## 解决方案

### 1. 三种画布状态处理

在 `useWorkflowStore.tsx` 的 `loadFromApi` 方法中，根据不同情况生成画布数据：

```typescript
// 情况 1: 有 uiDesignDraft 数据
if (response.uiDesignDraft) {
  // 直接解析 JSON 字符串
  const parsedCanvas = parseUiDesign(response.uiDesignDraft);
  canvasData = {
    nodes: parsedCanvas.nodes || [],
    edges: parsedCanvas.edges || [],
    viewport: parsedCanvas.viewport || { zoom: 1, offsetX: 0, offsetY: 0 },
  };
}
// 情况 2: 没有 uiDesignDraft，但有 functionDesignDraft（节点数据）
else if (backendNodes.length > 0) {
  // 自动生成默认布局
  canvasData = {
    nodes: backendNodes.map((node, index) => ({
      id: node.id,
      type: node.type,
      position: { 
        x: 100 + (index % 3) * 300, 
        y: 100 + Math.floor(index / 3) * 200 
      },
      ui: {
        expanded: true,
        selected: false,
        highlighted: false,
      },
      title: node.name,
      content: node.description || '',
    })),
    edges: backendEdges.map(edge => ({
      id: edge.id,
      sourceNodeId: edge.sourceNodeId,
      targetNodeId: edge.targetNodeId,
      ui: {
        selected: false,
        style: 'solid' as const,
      },
    })),
    viewport: { zoom: 1, offsetX: 0, offsetY: 0 },
  };
}
// 情况 3: functionDesignDraft 和 uiDesignDraft 都为空（新建工作流）
else {
  // 返回空画布
  canvasData = createDefaultCanvasData();
}
```

### 2. 空画布提示

在 `WorkflowConfig.tsx` 的 `WorkflowCanvas` 组件中添加空状态提示：

```typescript
function WorkflowCanvas() {
  const { playground, document } = useClientContext();
  const [messageApi] = message.useMessage();
  const store = useWorkflowStore();

  // ... 拖放处理逻辑 ...

  // 检查是否为空画布
  const isEmpty = store.backend.nodes.length === 0;

  return (
    <div 
      className="workflow-editor"
      onDrop={handleDrop}
      onDragOver={handleDragOver}
    >
      <EditorRenderer />
      {isEmpty && (
        <div className="workflow-empty-hint">
          <div className="empty-hint-content">
            <div className="empty-hint-icon">📋</div>
            <h3>开始设计你的工作流</h3>
            <p>从左侧节点面板拖拽节点到画布上</p>
          </div>
        </div>
      )}
      <Minimap />
      <Tools />
    </div>
  );
}
```

### 3. 空状态样式

在 `WorkflowCanvas.css` 中添加空状态提示样式：

```css
/* 空画布提示 */
.workflow-empty-hint {
  position: absolute;
  top: 50%;
  left: 50%;
  transform: translate(-50%, -50%);
  z-index: 1;
  pointer-events: none;
  text-align: center;
}

.empty-hint-content {
  background: var(--color-bg-container);
  border: 2px dashed var(--color-border);
  border-radius: var(--radius-lg);
  padding: var(--spacing-xl);
  box-shadow: var(--shadow-sm);
}

.empty-hint-icon {
  font-size: 48px;
  margin-bottom: var(--spacing-md);
  opacity: 0.5;
}

.empty-hint-content h3 {
  margin: 0 0 var(--spacing-sm) 0;
  font-size: 18px;
  font-weight: 600;
  color: var(--color-text-primary);
}

.empty-hint-content p {
  margin: 0;
  font-size: 14px;
  color: var(--color-text-secondary);
}
```

## 用户体验流程

### 新建工作流（空画布）

1. 用户创建新的流程应用
2. 第一次打开配置页面
3. API 返回空的 `functionDesignDraft` 和 `uiDesignDraft`
4. 显示空画布和提示信息："开始设计你的工作流，从左侧节点面板拖拽节点到画布上"
5. 用户从左侧节点面板拖拽节点到画布
6. 节点添加成功，空状态提示自动消失

### 已有节点但无布局信息

1. 后端有节点数据（`functionDesignDraft` 不为空）
2. 但没有布局信息（`uiDesignDraft` 为空）
3. 自动生成默认布局（网格排列）
4. 用户可以调整节点位置

### 完整的工作流

1. 后端有节点数据和布局信息
2. 直接使用 `uiDesignDraft` 的位置信息
3. 显示完整的工作流画布

## 技术细节

### 空画布判断

```typescript
const isEmpty = store.backend.nodes.length === 0;
```

只要 `backend.nodes` 为空数组，就显示空状态提示。

### 提示层级

```css
z-index: 1;
pointer-events: none;
```

- `z-index: 1` 确保提示在画布上方
- `pointer-events: none` 确保不阻挡拖放操作

### 动态显示/隐藏

```typescript
{isEmpty && (
  <div className="workflow-empty-hint">
    {/* 提示内容 */}
  </div>
)}
```

当用户添加第一个节点后，`isEmpty` 变为 `false`，提示自动消失。

## 测试场景

### 场景 1: 新建工作流
- **输入**: `functionDesignDraft: []`, `uiDesignDraft: null`
- **预期**: 显示空画布和提示信息
- **验证**: 可以拖拽节点到画布

### 场景 2: 有节点无布局
- **输入**: `functionDesignDraft: [node1, node2]`, `uiDesignDraft: null`
- **预期**: 显示节点（自动布局）
- **验证**: 节点按网格排列

### 场景 3: 完整工作流
- **输入**: `functionDesignDraft: [node1, node2]`, `uiDesignDraft: "{...}"`
- **预期**: 显示节点（使用保存的位置）
- **验证**: 节点位置与保存时一致

## 相关文件

- `src/components/team/apps/workflow/useWorkflowStore.tsx` - 画布数据生成逻辑
- `src/components/team/apps/workflow/WorkflowConfig.tsx` - 空状态提示组件
- `src/components/team/apps/workflow/WorkflowCanvas.css` - 空状态样式

## 注意事项

1. **空状态提示不阻挡交互**: 使用 `pointer-events: none` 确保用户可以拖拽节点到画布
2. **自动消失**: 添加第一个节点后，提示自动消失
3. **视觉引导**: 使用虚线边框和图标，清晰指示用户操作
4. **响应式**: 提示居中显示，适配不同屏幕尺寸
