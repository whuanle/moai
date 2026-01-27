# 工作流节点定义 API 使用指南

## 概述

工作流节点定义 API 用于帮助前端设计器获取不同节点类型的定义信息，包括：
- 节点的输入输出字段
- 字段类型和是否必需
- 节点描述和图标
- 特定节点的额外信息（如 Plugin 的参数）

## API 端点

### 1. 查询单个节点定义

**端点**: `POST /app/workflow/node-define/query`

**用途**: 获取单个节点类型的定义信息

#### 请求示例

##### 固定节点（Start、End、Condition 等）

```json
{
  "nodeType": "Start"
}
```

```json
{
  "nodeType": "Condition"
}
```

##### Plugin 节点（需要 PluginId）

```json
{
  "nodeType": "Plugin",
  "pluginId": 123
}
```

##### AiChat 节点（可选 ModelId）

```json
{
  "nodeType": "AiChat",
  "modelId": 456
}
```

##### Wiki 节点（可选 WikiId）

```json
{
  "nodeType": "Wiki",
  "wikiId": 789
}
```

#### 响应示例

```json
{
  "nodeType": "AiChat",
  "nodeTypeName": "AI 对话节点",
  "description": "调用 AI 模型进行对话生成",
  "inputFields": [
    {
      "fieldName": "prompt",
      "fieldType": "String",
      "isRequired": true,
      "description": "提示词模板"
    },
    {
      "fieldName": "modelId",
      "fieldType": "Number",
      "isRequired": true,
      "description": "AI 模型 ID"
    },
    {
      "fieldName": "temperature",
      "fieldType": "Number",
      "isRequired": false,
      "description": "温度参数（0-1）"
    }
  ],
  "outputFields": [
    {
      "fieldName": "response",
      "fieldType": "String",
      "isRequired": true,
      "description": "AI 生成的响应文本"
    },
    {
      "fieldName": "usage",
      "fieldType": "Object",
      "isRequired": true,
      "description": "Token 使用统计"
    }
  ],
  "modelId": 456,
  "modelName": "GPT-4",
  "supportsStreaming": true,
  "icon": "robot",
  "color": "#13c2c2"
}
```

### 2. 批量查询节点定义（聚合 API）

**端点**: `POST /app/workflow/node-define/batch-query`

**用途**: 一次性获取多个节点类型的定义信息，减少 API 调用次数

#### 请求示例

```json
{
  "requests": [
    {
      "nodeType": "Start",
      "requestId": "req-1"
    },
    {
      "nodeType": "AiChat",
      "modelId": 456,
      "requestId": "req-2"
    },
    {
      "nodeType": "Plugin",
      "pluginId": 123,
      "requestId": "req-3"
    },
    {
      "nodeType": "Wiki",
      "wikiId": 789,
      "requestId": "req-4"
    },
    {
      "nodeType": "End",
      "requestId": "req-5"
    }
  ]
}
```

#### 响应示例

```json
{
  "nodeDefines": [
    {
      "requestId": "req-1",
      "nodeType": "Start",
      "nodeTypeName": "开始节点",
      "description": "工作流的入口点，初始化工作流上下文并传递启动参数",
      "inputFields": [...],
      "outputFields": [...],
      "supportsStreaming": false,
      "icon": "play-circle",
      "color": "#52c41a"
    },
    {
      "requestId": "req-2",
      "nodeType": "AiChat",
      "nodeTypeName": "AI 对话节点",
      "modelId": 456,
      "modelName": "GPT-4",
      "inputFields": [...],
      "outputFields": [...],
      "supportsStreaming": true,
      "icon": "robot",
      "color": "#13c2c2"
    },
    {
      "requestId": "req-5",
      "nodeType": "End",
      "nodeTypeName": "结束节点",
      "inputFields": [...],
      "outputFields": [],
      "supportsStreaming": false,
      "icon": "stop-circle",
      "color": "#ff4d4f"
    }
  ],
  "errors": [
    {
      "requestId": "req-3",
      "nodeType": "Plugin",
      "errorMessage": "插件不存在: 123",
      "errorCode": "404"
    }
  ]
}
```

## 节点类型说明

### 固定节点（无需额外参数）

| NodeType | 名称 | 说明 |
|----------|------|------|
| Start | 开始节点 | 工作流入口，初始化上下文 |
| End | 结束节点 | 工作流终点，返回最终结果 |
| Condition | 条件节点 | 根据条件表达式路由分支 |
| ForEach | 循环节点 | 迭代集合执行循环体 |
| Fork | 分支节点 | 并行执行多个分支 |
| JavaScript | JavaScript 节点 | 执行 JavaScript 代码 |
| DataProcess | 数据处理节点 | 数据转换操作 |

### 动态节点（需要额外参数）

| NodeType | 必需参数 | 可选参数 | 说明 |
|----------|---------|---------|------|
| Plugin | pluginId | - | 根据插件 ID 获取插件特定的参数定义 |
| AiChat | - | modelId | 可指定模型 ID 获取模型特定配置 |
| Wiki | - | wikiId | 可指定知识库 ID 获取知识库特定配置 |

## 字段类型 (FieldType)

| 类型 | 说明 | 示例 |
|------|------|------|
| Empty | 空类型 | - |
| String | 字符串 | "Hello World" |
| Number | 数字 | 123, 45.67 |
| Boolean | 布尔值 | true, false |
| Object | JSON 对象 | {"key": "value"} |
| Array | 数组 | [1, 2, 3] |
| Dynamic | 动态类型 | 任意类型 |

## 使用场景

### 场景 1：工作流设计器初始化

当用户打开工作流设计器时，批量获取所有常用节点的定义：

```javascript
// 前端代码示例
const response = await fetch('/app/workflow/node-define/batch-query', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({
    requests: [
      { nodeType: 'Start', requestId: 'start' },
      { nodeType: 'End', requestId: 'end' },
      { nodeType: 'AiChat', requestId: 'aichat' },
      { nodeType: 'Wiki', requestId: 'wiki' },
      { nodeType: 'Condition', requestId: 'condition' },
      { nodeType: 'JavaScript', requestId: 'javascript' }
    ]
  })
});

const data = await response.json();
// 使用 data.nodeDefines 渲染节点面板
```

### 场景 2：用户选择插件后获取插件定义

当用户在设计器中选择特定插件时，获取该插件的参数定义：

```javascript
// 用户选择了插件 ID 123
const response = await fetch('/app/workflow/node-define/query', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({
    nodeType: 'Plugin',
    pluginId: 123
  })
});

const pluginDefine = await response.json();
// 使用 pluginDefine.inputFields 渲染插件参数表单
```

### 场景 3：动态加载多个插件定义

当用户需要同时了解多个插件的定义时：

```javascript
const selectedPlugins = [123, 456, 789];

const response = await fetch('/app/workflow/node-define/batch-query', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({
    requests: selectedPlugins.map((pluginId, index) => ({
      nodeType: 'Plugin',
      pluginId: pluginId,
      requestId: `plugin-${index}`
    }))
  })
});

const data = await response.json();
// data.nodeDefines 包含所有成功的插件定义
// data.errors 包含失败的插件（如不存在的插件）
```

## 错误处理

### 单个查询错误

如果查询失败，会抛出异常：

```json
{
  "statusCode": 404,
  "message": "插件不存在: 123"
}
```

### 批量查询错误

批量查询时，部分失败不会影响其他请求，失败的请求会在 `errors` 数组中返回：

```json
{
  "nodeDefines": [...],  // 成功的结果
  "errors": [
    {
      "requestId": "req-3",
      "nodeType": "Plugin",
      "errorMessage": "插件不存在: 123",
      "errorCode": "404"
    }
  ]
}
```

## 最佳实践

1. **使用批量查询减少请求次数**
   - 设计器初始化时，一次性获取所有常用节点定义
   - 避免为每个节点类型单独发起请求

2. **使用 requestId 关联请求和响应**
   - 在批量查询时，为每个请求设置唯一的 requestId
   - 响应中会包含相同的 requestId，方便前端匹配

3. **缓存节点定义**
   - 固定节点（Start、End 等）的定义不会变化，可以缓存
   - Plugin、AiChat、Wiki 节点的定义可能会变化，建议设置较短的缓存时间

4. **处理错误**
   - 批量查询时，检查 `errors` 数组
   - 对于失败的请求，可以显示默认定义或提示用户

5. **限制批量查询数量**
   - 单次批量查询最多支持 50 个节点定义
   - 如需查询更多，请分批请求

## 节点定义的 UI 展示建议

### 节点面板

使用 `icon` 和 `color` 字段渲染节点图标和颜色：

```jsx
<NodeCard 
  icon={nodeDefine.icon} 
  color={nodeDefine.color}
  title={nodeDefine.nodeTypeName}
  description={nodeDefine.description}
/>
```

### 参数表单

根据 `inputFields` 动态生成表单：

```jsx
{nodeDefine.inputFields.map(field => (
  <FormField
    key={field.fieldName}
    name={field.fieldName}
    type={field.fieldType}
    required={field.isRequired}
    label={field.description}
  />
))}
```

### 输出预览

根据 `outputFields` 显示节点的输出结构：

```jsx
<OutputPreview>
  {nodeDefine.outputFields.map(field => (
    <OutputField
      key={field.fieldName}
      name={field.fieldName}
      type={field.fieldType}
      description={field.description}
    />
  ))}
</OutputPreview>
```
