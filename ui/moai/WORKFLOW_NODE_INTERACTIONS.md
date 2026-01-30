# 工作流节点交互功能实现

## 实现的功能

### 1. 右键菜单（Context Menu）
- **触发方式**：右键点击节点
- **菜单项**：
  - **编辑节点**：打开节点配置抽屉面板
  - **复制节点**：复制当前节点（如果节点类型允许复制）
  - **删除节点**：删除当前节点（如果节点类型允许删除）

### 2. 圈选多节点右键菜单
- **触发方式**：框选多个节点后右键点击
- **菜单项**：
  - **删除选中节点**：批量删除选中的节点
- **智能过滤**：
  - 自动过滤不可删除的节点
  - 显示删除结果和被过滤的节点数量

### 3. 多选删除（快捷键）
- **触发方式**：框选多个节点后按 `Delete` 键
- **功能**：
  - 批量删除选中的节点
  - 自动过滤不可删除的节点
  - 显示删除结果提示

### 4. 双击编辑
- **触发方式**：双击节点
- **功能**：打开右侧配置抽屉面板，可编辑节点属性

## 技术实现

### 节点事件监听
使用 DOM 事件监听实现节点交互：

```typescript
// 双击事件
canvasElement.addEventListener('dblclick', handleDoubleClick);

// 右键菜单事件
canvasElement.addEventListener('contextmenu', handleContextMenu);

// Delete 键删除
window.addEventListener('keydown', handleKeyDown);
```

### 圈选菜单实现
通过检测选中节点数量来判断是否显示圈选菜单：

```typescript
const selectedNodes = (document as any).getSelectedNodes?.() || [];

if (selectedNodes.length > 1) {
  // 显示圈选批量操作菜单
  setSelectionMenuVisible(true);
}
```

### 节点识别
通过 `data-node-id` 属性识别节点：

```typescript
<div data-node-id={props.node.id}>
  <DefaultNodeRenderer {...props} />
</div>
```

### 权限控制
- 使用 `NODE_CONSTRAINTS` 配置控制节点的可删除性和可复制性
- 开始节点和结束节点不可删除
- 部分节点类型不可复制
- 批量删除时自动过滤不可删除的节点

### 状态同步
- 操作通过 Zustand store 管理状态
- 自动同步到 FlowGram 编辑器
- 保持数据一致性

## 用户体验

### 视觉反馈
- 节点 hover 效果：阴影加深，轻微上浮
- 选中状态：蓝色边框高亮
- 操作成功/失败提示消息
- 两种右键菜单：单节点菜单和圈选菜单

### 交互流程
1. **编辑节点**：右键 → 编辑节点 / 双击节点 → 配置面板 → 修改 → 保存
2. **复制节点**：右键 → 复制节点 → 自动创建副本
3. **删除单个节点**：右键 → 删除节点
4. **批量删除节点**：
   - 方式1：框选多个节点 → 右键 → 删除选中节点
   - 方式2：框选多个节点 → 按 Delete 键

## 配置面板

### 功能
- 编辑节点名称和描述
- 根据节点类型显示不同的配置项
- 表单验证
- 保存后自动更新画布

### 遵循规范
- `maskClosable={false}`：不允许点击蒙层关闭
- 必须通过取消/保存按钮关闭

## 文件修改

### 主要文件
- `src/components/team/apps/workflow/WorkflowEditor.tsx`
  - 添加节点交互事件监听
  - 实现单节点右键菜单
  - 实现圈选右键菜单
  - 实现多选删除（Delete 键）
  - 实现双击编辑

- `src/components/team/apps/workflow/WorkflowEditor.css`
  - 添加节点样式
  - 添加 hover 和选中效果

- `src/components/team/apps/workflow/ConfigPanel.tsx`
  - 确保 `maskClosable={false}`

### Store 方法使用
- `store.canDeleteNode(id)` - 检查是否可删除
- `store.deleteNode(id)` - 删除单个节点
- `store.deleteNodes(ids)` - 批量删除节点
- `store.copyNode(id)` - 复制节点
- `store.getNode(id)` - 获取节点信息

## 调试功能

代码中包含详细的 console.log 调试信息：
- `🔍 Canvas element:` - 画布元素查找
- `✅ 事件监听器已添加` - 事件监听器状态
- `🖱️ 双击事件触发` / `🖱️ 右键事件触发` - 事件触发
- `🔍 找到的节点元素:` - 节点元素查找
- `✅ 节点 ID:` - 节点识别
- `📋 显示圈选右键菜单` - 圈选菜单显示

## 注意事项

1. **类型安全**：使用类型断言访问 FlowGram 编辑器的未导出 API
2. **事件清理**：所有事件监听器都在组件卸载时清理
3. **错误处理**：所有操作都有错误提示
4. **权限检查**：操作前检查节点约束
5. **状态同步**：Store 和编辑器双向同步
6. **智能过滤**：批量删除时自动过滤不可删除的节点

## 后续优化建议

1. 添加节点配置的撤销/重做功能
2. 支持更多键盘快捷键（Ctrl+C 复制，Ctrl+V 粘贴）
3. 添加节点批量编辑功能
4. 优化右键菜单的位置计算（避免超出屏幕）
5. 添加节点拖拽复制功能
6. 圈选菜单添加更多批量操作（复制、移动等）

