# 工作流编排器

## 功能说明

### 节点操作

#### 添加节点
1. 从左侧节点面板拖拽节点到画布
2. 节点会自动创建唯一 ID
3. 支持搜索节点类型

#### 右键菜单
在画布上的任意节点上右键点击，可以：

- **编辑节点**：编辑节点配置（开发中）
- **复制节点**：复制当前节点到偏移位置
- **删除节点**：删除当前节点

#### 节点类型

**控制流节点**
- 开始：工作流起始节点
- 结束：工作流结束节点
- 条件判断：根据条件分支执行
- 并行分支：同时执行多个分支
- 循环遍历：遍历数组元素

**AI 节点**
- AI 对话：调用 AI 模型进行对话

**数据处理节点**
- 数据处理：处理和转换数据
- JavaScript：执行 JavaScript 代码

**集成节点**
- 插件调用：调用已配置的插件
- Wiki 查询：从知识库检索信息

### 画布操作

- **拖拽画布**：按住鼠标左键拖动
- **缩放**：鼠标滚轮或使用右下角缩放控制器
- **适应视图**：点击缩放控制器的适应按钮

### 快捷键

- `Ctrl +`：放大
- `Ctrl -`：缩小
- `右键`：打开节点菜单

## 技术实现

### 核心文件

- `nodeTemplates.ts`：节点模板定义
- `NodePanel.tsx`：节点面板组件
- `WorkflowConfig.tsx`：主配置页面
- `useEditorProps.tsx`：编辑器配置和右键菜单
- `nodeRegistries.tsx`：节点注册表

### 右键菜单实现

使用 Ant Design 的 `Dropdown` 组件实现右键菜单：

```tsx
<Dropdown menu={{ items: menuItems }} trigger={['contextMenu']}>
  <WorkflowNodeRenderer node={props.node}>
    {form?.render()}
  </WorkflowNodeRenderer>
</Dropdown>
```

菜单项包括：
- 编辑节点
- 复制节点
- 删除节点（红色危险按钮）

### 删除节点

由于 `@flowgram.ai/free-layout-editor` 库的 API 可能有不同版本，删除功能尝试多种方法：

```tsx
if (typeof (document as any).deleteNode === 'function') {
  (document as any).deleteNode(props.node.id);
} else if (typeof (document as any).removeNode === 'function') {
  (document as any).removeNode(props.node.id);
} else if (typeof (props.node as any).remove === 'function') {
  (props.node as any).remove();
}
```

这确保了在不同版本的库中都能正常工作。
