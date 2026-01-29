# 工作流节点模板 - 设计文档

## 1. 架构设计

### 1.1 组件结构
```
WorkflowConfig (主容器)
├── WorkflowHeader (顶部工具栏)
├── NodePanel (左侧节点面板) ← 新增
│   ├── NodeCategory (节点分类)
│   └── NodeTemplate (节点模板卡片)
├── EditorRenderer (画布)
└── ZoomControls (缩放控制)
```

### 1.2 数据流
```
nodeTemplates.ts (节点模板定义)
    ↓
NodePanel (显示可用节点)
    ↓ (拖拽)
EditorRenderer (创建节点实例)
    ↓
useEditorProps (节点渲染配置)
```

## 2. 节点模板定义

### 2.1 模板数据结构

```typescript
interface NodeTemplate {
  type: NodeType;
  name: string;
  description: string;
  icon: string;
  color: string;
  category: NodeCategory;
  defaultData: {
    title: string;
    content?: string;
    inputFields?: FieldDefine[];
    outputFields?: FieldDefine[];
  };
}

enum NodeCategory {
  Control = 'control',      // 控制流
  AI = 'ai',               // AI 节点
  Data = 'data',           // 数据处理
  Integration = 'integration' // 集成
}
```

### 2.2 节点模板配置

创建 `src/components/team/apps/workflow/nodeTemplates.ts`：

```typescript
export const nodeTemplates: NodeTemplate[] = [
  // 控制流节点
  {
    type: 'start',
    name: '开始',
    description: '工作流的起始节点',
    icon: '▶️',
    color: '#52c41a',
    category: NodeCategory.Control,
    defaultData: {
      title: '开始',
      outputFields: [
        { fieldName: 'trigger', fieldType: 'object', isRequired: false }
      ]
    }
  },
  {
    type: 'end',
    name: '结束',
    description: '工作流的结束节点',
    icon: '⏹️',
    color: '#ff4d4f',
    category: NodeCategory.Control,
    defaultData: {
      title: '结束',
      inputFields: [
        { fieldName: 'result', fieldType: 'dynamic', isRequired: false }
      ]
    }
  },
  {
    type: 'condition',
    name: '条件判断',
    description: '根据条件分支执行',
    icon: '◆',
    color: '#faad14',
    category: NodeCategory.Control,
    defaultData: {
      title: '条件判断',
      inputFields: [
        { fieldName: 'condition', fieldType: 'boolean', isRequired: true }
      ],
      outputFields: [
        { fieldName: 'true', fieldType: 'dynamic', isRequired: false },
        { fieldName: 'false', fieldType: 'dynamic', isRequired: false }
      ]
    }
  },
  {
    type: 'fork',
    name: '并行分支',
    description: '同时执行多个分支',
    icon: '⑂',
    color: '#722ed1',
    category: NodeCategory.Control,
    defaultData: {
      title: '并行分支',
      inputFields: [
        { fieldName: 'input', fieldType: 'dynamic', isRequired: false }
      ],
      outputFields: [
        { fieldName: 'branches', fieldType: 'array', isRequired: false }
      ]
    }
  },
  {
    type: 'forEach',
    name: '循环遍历',
    description: '遍历数组中的每个元素',
    icon: '🔁',
    color: '#13c2c2',
    category: NodeCategory.Control,
    defaultData: {
      title: '循环遍历',
      inputFields: [
        { fieldName: 'array', fieldType: 'array', isRequired: true }
      ],
      outputFields: [
        { fieldName: 'item', fieldType: 'dynamic', isRequired: false },
        { fieldName: 'index', fieldType: 'number', isRequired: false }
      ]
    }
  },
  
  // AI 节点
  {
    type: 'aiChat',
    name: 'AI 对话',
    description: '调用 AI 模型进行对话',
    icon: '🤖',
    color: '#1677ff',
    category: NodeCategory.AI,
    defaultData: {
      title: 'AI 对话',
      inputFields: [
        { fieldName: 'prompt', fieldType: 'string', isRequired: true },
        { fieldName: 'context', fieldType: 'string', isRequired: false }
      ],
      outputFields: [
        { fieldName: 'response', fieldType: 'string', isRequired: false }
      ]
    }
  },
  
  // 数据处理节点
  {
    type: 'dataProcess',
    name: '数据处理',
    description: '处理和转换数据',
    icon: '⚙️',
    color: '#2f54eb',
    category: NodeCategory.Data,
    defaultData: {
      title: '数据处理',
      inputFields: [
        { fieldName: 'input', fieldType: 'dynamic', isRequired: true }
      ],
      outputFields: [
        { fieldName: 'output', fieldType: 'dynamic', isRequired: false }
      ]
    }
  },
  {
    type: 'javaScript',
    name: 'JavaScript',
    description: '执行 JavaScript 代码',
    icon: '📜',
    color: '#f5222d',
    category: NodeCategory.Data,
    defaultData: {
      title: 'JavaScript',
      content: '// 编写 JavaScript 代码\nreturn input;',
      inputFields: [
        { fieldName: 'input', fieldType: 'dynamic', isRequired: false }
      ],
      outputFields: [
        { fieldName: 'output', fieldType: 'dynamic', isRequired: false }
      ]
    }
  },
  
  // 集成节点
  {
    type: 'plugin',
    name: '插件调用',
    description: '调用已配置的插件',
    icon: '🔌',
    color: '#eb2f96',
    category: NodeCategory.Integration,
    defaultData: {
      title: '插件调用',
      inputFields: [
        { fieldName: 'params', fieldType: 'object', isRequired: false }
      ],
      outputFields: [
        { fieldName: 'result', fieldType: 'dynamic', isRequired: false }
      ]
    }
  },
  {
    type: 'wiki',
    name: '知识库查询',
    description: '从知识库中检索信息',
    icon: '📚',
    color: '#52c41a',
    category: NodeCategory.Integration,
    defaultData: {
      title: '知识库查询',
      inputFields: [
        { fieldName: 'query', fieldType: 'string', isRequired: true }
      ],
      outputFields: [
        { fieldName: 'documents', fieldType: 'array', isRequired: false }
      ]
    }
  }
];
```

## 3. 节点面板组件

### 3.1 NodePanel 组件

创建 `src/components/team/apps/workflow/NodePanel.tsx`：

```typescript
import { useState } from 'react';
import { Collapse, Input, Badge } from 'antd';
import { SearchOutlined } from '@ant-design/icons';
import { nodeTemplates, NodeCategory } from './nodeTemplates';
import './NodePanel.css';

const categoryNames = {
  [NodeCategory.Control]: '控制流',
  [NodeCategory.AI]: 'AI 节点',
  [NodeCategory.Data]: '数据处理',
  [NodeCategory.Integration]: '集成'
};

export function NodePanel() {
  const [searchText, setSearchText] = useState('');
  
  // 按分类分组节点
  const groupedNodes = nodeTemplates.reduce((acc, template) => {
    if (!acc[template.category]) {
      acc[template.category] = [];
    }
    acc[template.category].push(template);
    return acc;
  }, {} as Record<NodeCategory, typeof nodeTemplates>);
  
  // 过滤节点
  const filteredGroups = Object.entries(groupedNodes).map(([category, nodes]) => ({
    category: category as NodeCategory,
    nodes: nodes.filter(node => 
      node.name.toLowerCase().includes(searchText.toLowerCase()) ||
      node.description.toLowerCase().includes(searchText.toLowerCase())
    )
  })).filter(group => group.nodes.length > 0);
  
  const handleDragStart = (e: React.DragEvent, template: typeof nodeTemplates[0]) => {
    e.dataTransfer.setData('application/json', JSON.stringify(template));
    e.dataTransfer.effectAllowed = 'copy';
  };
  
  return (
    <div className="workflow-node-panel">
      <div className="node-panel-header">
        <h3>节点库</h3>
        <Input
          placeholder="搜索节点"
          prefix={<SearchOutlined />}
          value={searchText}
          onChange={e => setSearchText(e.target.value)}
          allowClear
        />
      </div>
      
      <div className="node-panel-content">
        <Collapse
          defaultActiveKey={Object.values(NodeCategory)}
          ghost
          items={filteredGroups.map(({ category, nodes }) => ({
            key: category,
            label: (
              <span>
                {categoryNames[category]}
                <Badge count={nodes.length} style={{ marginLeft: 8 }} />
              </span>
            ),
            children: (
              <div className="node-template-list">
                {nodes.map(template => (
                  <div
                    key={template.type}
                    className="node-template-card"
                    draggable
                    onDragStart={e => handleDragStart(e, template)}
                    style={{ borderLeftColor: template.color }}
                  >
                    <div className="node-template-icon">{template.icon}</div>
                    <div className="node-template-info">
                      <div className="node-template-name">{template.name}</div>
                      <div className="node-template-desc">{template.description}</div>
                    </div>
                  </div>
                ))}
              </div>
            )
          }))}
        />
      </div>
    </div>
  );
}
```

### 3.2 NodePanel 样式

创建 `src/components/team/apps/workflow/NodePanel.css`：

```css
.workflow-node-panel {
  width: 280px;
  height: 100%;
  background: var(--color-bg-container);
  border-right: 1px solid var(--color-border);
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.node-panel-header {
  padding: var(--spacing-md);
  border-bottom: 1px solid var(--color-border);
}

.node-panel-header h3 {
  margin: 0 0 var(--spacing-sm) 0;
  font-size: 16px;
  font-weight: 600;
}

.node-panel-content {
  flex: 1;
  overflow-y: auto;
  padding: var(--spacing-sm);
}

.node-template-list {
  display: flex;
  flex-direction: column;
  gap: var(--spacing-sm);
}

.node-template-card {
  display: flex;
  align-items: center;
  gap: var(--spacing-sm);
  padding: var(--spacing-sm);
  background: var(--color-bg-container);
  border: 1px solid var(--color-border);
  border-left-width: 3px;
  border-radius: var(--radius-sm);
  cursor: grab;
  transition: all var(--transition-normal);
}

.node-template-card:hover {
  background: var(--color-bg-layout);
  box-shadow: var(--shadow-sm);
  transform: translateY(-2px);
}

.node-template-card:active {
  cursor: grabbing;
}

.node-template-icon {
  font-size: 24px;
  flex-shrink: 0;
}

.node-template-info {
  flex: 1;
  min-width: 0;
}

.node-template-name {
  font-size: 14px;
  font-weight: 500;
  color: var(--color-text-primary);
  margin-bottom: 2px;
}

.node-template-desc {
  font-size: 12px;
  color: var(--color-text-secondary);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}
```

## 4. 画布集成

### 4.1 修改 WorkflowConfig 组件

在 `WorkflowConfig.tsx` 中添加节点面板：

```typescript
<div className="workflow-canvas-container">
  <NodePanel />
  <FreeLayoutEditorProvider {...editorProps}>
    <EditorRenderer 
      className="workflow-editor"
      onDrop={handleCanvasDrop}
      onDragOver={handleCanvasDragOver}
    />
    <ZoomControls />
  </FreeLayoutEditorProvider>
</div>
```

### 4.2 处理节点拖放

```typescript
const handleCanvasDragOver = (e: React.DragEvent) => {
  e.preventDefault();
  e.dataTransfer.dropEffect = 'copy';
};

const handleCanvasDrop = (e: React.DragEvent) => {
  e.preventDefault();
  
  const templateData = e.dataTransfer.getData('application/json');
  if (!templateData) return;
  
  const template = JSON.parse(templateData);
  const { playground } = useClientContext();
  
  // 将鼠标位置转换为画布坐标
  const canvasPos = playground.config.getPoseFromMouseEvent(e.nativeEvent);
  
  // 创建新节点
  const newNode = {
    id: `${template.type}_${Date.now()}`,
    type: template.type,
    meta: {
      position: canvasPos,
    },
    data: template.defaultData
  };
  
  // 添加到画布
  document.addNode(newNode);
};
```

## 5. 节点渲染配置

### 5.1 更新 nodeRegistries.ts

根据节点类型配置不同的渲染样式：

```typescript
import { nodeTemplates } from './nodeTemplates';

export const nodeRegistries = nodeTemplates.reduce((acc, template) => {
  acc[template.type] = {
    type: template.type,
    meta: {
      defaultExpanded: true,
      color: template.color,
      icon: template.icon,
    },
    formMeta: {
      render: () => (
        // 节点表单渲染逻辑
      )
    }
  };
  return acc;
}, {} as Record<string, any>);
```

## 6. 正确性属性

### 6.1 节点创建属性
**属性 1.1**: 拖拽节点到画布后，必须创建一个新的节点实例
- 节点 ID 唯一
- 节点位置正确
- 节点数据完整

**属性 1.2**: 节点类型必须与模板定义一致
- nodeType 字段正确
- 默认字段完整
- 颜色和图标正确

### 6.2 节点面板属性
**属性 2.1**: 搜索功能必须正确过滤节点
- 按名称搜索
- 按描述搜索
- 大小写不敏感

**属性 2.2**: 节点分类必须正确显示
- 所有节点都在正确的分类下
- 分类计数正确
- 空分类不显示

## 7. 实现计划

### 阶段 1: 节点模板定义
1. 创建 nodeTemplates.ts
2. 定义所有节点类型
3. 配置默认数据

### 阶段 2: 节点面板组件
1. 创建 NodePanel 组件
2. 实现搜索功能
3. 实现拖拽功能

### 阶段 3: 画布集成
1. 修改 WorkflowConfig 布局
2. 实现拖放处理
3. 更新节点渲染配置

### 阶段 4: 测试和优化
1. 测试所有节点类型
2. 优化拖拽体验
3. 添加错误处理
