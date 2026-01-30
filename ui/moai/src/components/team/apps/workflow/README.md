# 工作流配置模块

## 概述

工作流配置模块用于可视化编辑和配置工作流，基于 `@flowgram.ai/free-layout-editor` 实现。

## 数据结构

### 后端 API 响应

```typescript
interface QueryWorkflowDefinitionCommandResponse {
  appId: string;
  name: string;
  description: string;
  
  // 功能设计草稿 - NodeDesign[] 数组
  functionDesignDraft: NodeDesign[];
  
  // UI 设计草稿 - JSON 字符串
  uiDesignDraft: string;
  
  isPublish: boolean;
  // ... 其他字段
}

interface NodeDesign {
  nodeKey: string;           // 节点唯一标识
  nodeType: NodeType;        // 节点类型
  name: string;              // 节点名称
  description: string;       // 节点描述
  fieldDesigns: KeyValueOfStringAndFieldDesign[];  // 字段设计
  nextNodeKeys: string[];    // 下游节点列表
}

interface FieldDesign {
  fieldName: string;         // 字段名称
  expressionType: FieldExpressionType;  // 表达式类型
  value: string;             // 字段值或表达式
}
```

### 内部数据结构

#### BackendWorkflowData
存储工作流的业务逻辑数据：
- nodes: 节点配置（输入/输出字段、执行设置等）
- edges: 节点连接关系
- variables: 全局变量
- metadata: 元数据

#### CanvasWorkflowData
存储画布的 UI 展示数据：
- nodes: 节点位置、UI 状态
- edges: 连接的 UI 样式
- viewport: 画布视图状态（缩放、偏移）

## 核心文件

### useWorkflowStore.tsx
Zustand 状态管理，负责：
- 从 API 加载工作流数据
- 将 NodeDesign[] 转换为内部 BackendNodeData
- 将 uiDesignDraft JSON 解析为 CanvasWorkflowData
- 保存工作流到后端
- 节点配置更新

### workflowConverter.ts
数据转换工具：
- `toEditorFormat`: 内部格式 → FlowGram 编辑器格式
- `fromEditorFormat`: FlowGram 编辑器格式 → 内部格式
- `parseUiDesign`: JSON 字符串 → CanvasWorkflowData
- `toUiDesign`: CanvasWorkflowData → JSON 字符串

### WorkflowConfig.tsx
主配置页面组件：
- 加载工作流数据
- 渲染 FlowGram 编辑器
- 处理节点拖放
- 显示节点配置面板

### useEditorProps.tsx
FlowGram 编辑器配置：
- 节点渲染逻辑
- 右键菜单
- 插件配置（缩略图、对齐）

## 使用流程

1. **加载工作流**
   ```typescript
   await store.loadFromApi(appId, teamId);
   ```
   - 调用 `/api/team/workflowapp/config` POST 接口
   - 解析 `functionDesignDraft` (NodeDesign[])
   - 解析 `uiDesignDraft` (JSON 字符串)
   - 转换为内部数据结构
   
   **三种情况：**
   - 有 uiDesignDraft：直接使用
   - 无 uiDesignDraft 但有节点：生成默认布局
   - 都为空（新建工作流）：显示空画布

2. **渲染画布**
   ```typescript
   const initialDocument = toEditorFormat(store.backend, store.canvas);
   const editorProps = useEditorProps(initialDocument, ...);
   ```
   - 合并后端数据和画布数据
   - 生成 FlowGram 编辑器所需格式

3. **节点配置**
   - 双击节点打开配置面板
   - 修改节点的 inputFields、outputFields、settings
   - 调用 `store.updateNodeConfig()` 更新

4. **保存工作流**
   ```typescript
   await store.saveToApi();
   ```
   - 将内部数据转换为 API 格式
   - 调用保存接口（待实现）

## 节点类型

- **start**: 开始节点（唯一）
- **end**: 结束节点（唯一）
- **condition**: 条件判断
- **fork**: 并行分支
- **forEach**: 循环遍历
- **aiChat**: AI 对话
- **dataProcess**: 数据处理
- **javaScript**: JavaScript 脚本
- **plugin**: 插件调用
- **wiki**: 知识库查询

## 字段表达式类型

- **Constant**: 常量值
- **Variable**: 变量引用
- **Expression**: 表达式
- **NodeOutput**: 节点输出
- **Context**: 上下文

## 注意事项

1. **数据一致性**: backend 和 canvas 的节点 ID 必须一致
2. **节点约束**: 开始/结束节点只能有一个
3. **连接关系**: 从 nextNodeKeys 推导边的连接
4. **字段类型**: 需要根据实际业务推断字段类型（默认 String）
5. **子字段支持**: Map 类型字段可以包含 children 子字段数组
6. **空画布处理**: 新建工作流时，functionDesignDraft 和 uiDesignDraft 都为空，显示空画布和提示信息

## 待实现功能

- [ ] 保存工作流 API 调用
- [ ] 其他节点类型的配置组件
- [ ] 节点验证逻辑
- [ ] 工作流执行功能
- [ ] 版本管理
