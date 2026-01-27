# AI 工作流前端集成指南

## 概述

本文档为前端团队提供完整的 AI 工作流系统对接指南，包括：
- 工作流应用的生命周期管理
- 工作流设计器的实现要点
- 节点定义查询和渲染
- 工作流执行和监控
- 完整的 API 接口说明

## 目录

1. [架构概览](#架构概览)
2. [工作流应用生命周期](#工作流应用生命周期)
3. [节点定义系统](#节点定义系统)
4. [工作流设计器实现](#工作流设计器实现)
5. [工作流执行](#工作流执行)
6. [API 接口详细说明](#api-接口详细说明)
7. [数据模型](#数据模型)
8. [前端组件建议](#前端组件建议)
9. [错误处理](#错误处理)
10. [最佳实践](#最佳实践)

---

## 架构概览

### 核心概念

**Workflow 是一种特殊的应用类型**：
- Workflow 不是独立的实体，而是 `AppEntity` 的一种类型（`AppType.Workflow = 2`）
- 应用基础信息（名称、描述、头像）存储在 `AppEntity` 中
- 工作流设计数据（节点、连接、UI 布局）存储在 `AppWorkflowDesignEntity` 中
- 所有外部接口都使用 `AppId`（应用 ID），不暴露内部的 WorkflowDesignId

### 数据分离原则

| 数据类型 | 存储位置 | 字段示例 |
|---------|---------|---------|
| 应用基础信息 | `AppEntity` | Name, Description, Avatar, TeamId |
| 工作流设计数据 | `AppWorkflowDesignEntity` | UiDesign, FunctionDesign (草稿和发布版本) |
| 工作流执行历史 | `AppWorkflowHistoryEntity` | State, Data, RunParameters |


---

## 工作流应用生命周期

### 1. 创建工作流应用

**步骤**：
1. 调用统一的应用创建接口
2. 指定 `appType = 2`（Workflow 类型）
3. 系统自动创建 `AppEntity` 和 `AppWorkflowDesignEntity`

**API 接口**：
```
POST /app/team/create
```

**请求示例**：
```json
{
  "name": "客户服务工作流",
  "description": "自动化客户咨询处理流程",
  "avatar": "https://example.com/avatar.png",
  "appType": 2,
  "teamId": 123,
  "classifyId": 456
}
```

**响应示例**：
```json
{
  "appId": "550e8400-e29b-41d4-a716-446655440000",
  "name": "客户服务工作流",
  "appType": 2,
  "createTime": "2024-01-15T10:30:00Z"
}
```

### 2. 编辑工作流（草稿模式）

**步骤**：
1. 用户在设计器中拖拽节点、配置参数、连接节点
2. 调用更新接口保存草稿
3. 草稿可以多次修改，不影响已发布版本

**API 接口**：
```
PUT /app/workflow/definition/{appId}
```

**请求示例**：
```json
{
  "appId": "550e8400-e29b-41d4-a716-446655440000",
  "name": "客户服务工作流",
  "description": "更新后的描述",
  "uiDesignDraft": {
    "nodes": [
      {
        "id": "node_1",
        "type": "Start",
        "position": { "x": 100, "y": 100 },
        "data": {}
      }
    ],
    "edges": []
  },
  "functionDesignDraft": {
    "nodes": [
      {
        "nodeKey": "node_1",
        "name": "开始",
        "nodeType": "Start",
        "fieldDesigns": []
      }
    ],
    "connections": []
  }
}
```


### 3. 发布工作流

**步骤**：
1. 验证草稿的完整性和有效性
2. 调用发布接口
3. 系统创建版本快照，将草稿复制到发布版本
4. 只有发布版本可以被执行

**API 接口**：
```
POST /app/workflow/definition/{appId}/publish
```

**请求示例**：
```json
{
  "appId": "550e8400-e29b-41d4-a716-446655440000"
}
```

**响应示例**：
```json
{
  "success": true,
  "message": "工作流已成功发布",
  "version": 2,
  "publishTime": "2024-01-15T11:00:00Z"
}
```

### 4. 查询工作流定义

**API 接口**：
```
GET /app/workflow/definition/{appId}
```

**响应示例**：
```json
{
  "id": "660e8400-e29b-41d4-a716-446655440001",
  "appId": "550e8400-e29b-41d4-a716-446655440000",
  "name": "客户服务工作流",
  "description": "自动化客户咨询处理流程",
  "avatar": "https://example.com/avatar.png",
  "isPublish": true,
  "uiDesign": { /* 发布版本的 UI 设计 */ },
  "uiDesignDraft": { /* 草稿版本的 UI 设计 */ },
  "functionDesign": { /* 发布版本的功能设计 */ },
  "functionDesignDraft": { /* 草稿版本的功能设计 */ }
}
```

### 5. 删除工作流应用

**API 接口**：
```
DELETE /app/team/delete
```

**请求示例**：
```json
{
  "appId": "550e8400-e29b-41d4-a716-446655440000"
}
```

**注意**：
- 删除应用会软删除 `AppEntity` 和 `AppWorkflowDesignEntity`
- 执行历史（`AppWorkflowHistoryEntity`）会被保留，用于审计


---

## 节点定义系统

### 节点类型概览

工作流支持 10 种节点类型：

| NodeType | 名称 | 用途 | 是否需要额外参数 |
|----------|------|------|-----------------|
| Start | 开始节点 | 工作流入口 | 否 |
| End | 结束节点 | 工作流终点 | 否 |
| AiChat | AI 对话节点 | 调用 AI 模型 | 可选 modelId |
| Wiki | 知识库节点 | 查询知识库 | 可选 wikiId |
| Plugin | 插件节点 | 执行插件 | 必需 pluginId |
| Condition | 条件节点 | 条件分支 | 否 |
| ForEach | 循环节点 | 迭代集合 | 否 |
| Fork | 分支节点 | 并行执行 | 否 |
| JavaScript | JS 代码节点 | 执行代码 | 否 |
| DataProcess | 数据处理节点 | 数据转换 | 否 |

### 查询节点定义

#### 单个节点查询

**API 接口**：
```
POST /app/workflow/node-define/query
```

**请求示例 1 - 查询固定节点**：
```json
{
  "nodeType": "Start"
}
```

**请求示例 2 - 查询插件节点**：
```json
{
  "nodeType": "Plugin",
  "pluginId": 123
}
```

**响应示例**：
```json
{
  "nodeType": "Plugin",
  "nodeTypeName": "插件节点",
  "description": "执行插件: 天气查询",
  "pluginId": 123,
  "pluginName": "天气查询",
  "inputFields": [
    {
      "fieldName": "city",
      "fieldType": "String",
      "isRequired": true,
      "description": "城市名称"
    },
    {
      "fieldName": "unit",
      "fieldType": "String",
      "isRequired": false,
      "description": "温度单位（celsius/fahrenheit）"
    }
  ],
  "outputFields": [
    {
      "fieldName": "result",
      "fieldType": "Dynamic",
      "isRequired": true,
      "description": "插件执行结果"
    }
  ],
  "supportsStreaming": false,
  "icon": "api",
  "color": "#1890ff"
}
```


#### 批量节点查询（推荐）

**API 接口**：
```
POST /app/workflow/node-define/batch-query
```

**请求示例**：
```json
{
  "requests": [
    {
      "requestId": "req_1",
      "nodeType": "Start"
    },
    {
      "requestId": "req_2",
      "nodeType": "End"
    },
    {
      "requestId": "req_3",
      "nodeType": "AiChat",
      "modelId": 456
    },
    {
      "requestId": "req_4",
      "nodeType": "Plugin",
      "pluginId": 123
    }
  ]
}
```

**响应示例**：
```json
{
  "nodeDefines": [
    {
      "requestId": "req_1",
      "nodeType": "Start",
      "nodeTypeName": "开始节点",
      "inputFields": [...],
      "outputFields": [...],
      "icon": "play-circle",
      "color": "#52c41a"
    },
    {
      "requestId": "req_2",
      "nodeType": "End",
      "nodeTypeName": "结束节点",
      "inputFields": [...],
      "outputFields": [],
      "icon": "stop-circle",
      "color": "#ff4d4f"
    }
  ],
  "errors": [
    {
      "requestId": "req_4",
      "errorCode": "PLUGIN_NOT_FOUND",
      "errorMessage": "插件不存在: 123"
    }
  ]
}
```

**优势**：
- 一次请求获取多个节点定义，减少网络开销
- 部分失败不影响其他请求
- 支持最多 50 个节点定义查询

### 字段类型说明

| FieldType | 说明 | 前端控件建议 |
|-----------|------|-------------|
| String | 字符串 | Input / TextArea |
| Number | 数字 | InputNumber |
| Boolean | 布尔值 | Switch / Checkbox |
| Object | JSON 对象 | JSON Editor |
| Array | 数组 | Dynamic List |
| Dynamic | 动态类型 | 根据实际值渲染 |


---

## 工作流设计器实现

### 设计器初始化流程

```typescript
// 1. 用户打开工作流设计器
async function initWorkflowDesigner(appId: string) {
  // 2. 查询工作流定义（如果是编辑模式）
  const workflowDef = await fetchWorkflowDefinition(appId);
  
  // 3. 批量查询所有常用节点定义
  const nodeDefines = await fetchBatchNodeDefines([
    { nodeType: 'Start' },
    { nodeType: 'End' },
    { nodeType: 'AiChat' },
    { nodeType: 'Wiki' },
    { nodeType: 'Condition' },
    { nodeType: 'ForEach' },
    { nodeType: 'Fork' },
    { nodeType: 'JavaScript' },
    { nodeType: 'DataProcess' }
  ]);
  
  // 4. 渲染节点面板
  renderNodePalette(nodeDefines);
  
  // 5. 渲染画布（如果有现有设计）
  if (workflowDef.uiDesignDraft) {
    renderCanvas(workflowDef.uiDesignDraft);
  }
}
```

### 数据结构设计

#### UiDesign（UI 设计数据）

用于存储节点的可视化布局信息：

```typescript
interface UiDesign {
  nodes: UiNode[];
  edges: UiEdge[];
  viewport?: {
    x: number;
    y: number;
    zoom: number;
  };
}

interface UiNode {
  id: string;              // 节点唯一标识（对应 FunctionDesign 中的 nodeKey）
  type: string;            // 节点类型（Start, End, Plugin 等）
  position: {
    x: number;
    y: number;
  };
  data: {
    label?: string;        // 节点显示名称
    icon?: string;         // 节点图标
    color?: string;        // 节点颜色
    [key: string]: any;    // 其他 UI 相关数据
  };
}

interface UiEdge {
  id: string;
  source: string;          // 源节点 ID
  target: string;          // 目标节点 ID
  sourceHandle?: string;   // 源节点连接点
  targetHandle?: string;   // 目标节点连接点
  label?: string;          // 连接线标签
  type?: string;           // 连接线类型（default, smoothstep 等）
}
```


#### FunctionDesign（功能设计数据）

用于存储节点的业务逻辑配置：

```typescript
interface FunctionDesign {
  nodes: NodeDesign[];
  connections: Connection[];
}

interface NodeDesign {
  nodeKey: string;         // 节点唯一标识（对应 UiDesign 中的 id）
  name: string;            // 节点名称
  description?: string;    // 节点描述
  nodeType: string;        // 节点类型（Start, End, Plugin 等）
  nextNodeKey?: string;    // 下一个节点的 key（可选）
  fieldDesigns: FieldDesign[];  // 字段配置
}

interface FieldDesign {
  fieldName: string;       // 字段名称
  expressionType: string;  // 表达式类型（Fixed, Variable, JsonPath, StringInterpolation）
  value: any;              // 字段值
}

interface Connection {
  sourceNodeKey: string;   // 源节点 key
  targetNodeKey: string;   // 目标节点 key
  label?: string;          // 连接标签（用于条件分支）
}
```

#### 表达式类型说明

| ExpressionType | 说明 | 示例 |
|----------------|------|------|
| Fixed | 固定值 | `"Hello World"`, `123`, `true` |
| Variable | 变量引用 | `"sys.userId"`, `"input.name"`, `"node_1.result"` |
| JsonPath | JSON 路径 | `"$.data.items[0].name"` |
| StringInterpolation | 字符串插值 | `"Hello {{input.name}}, your ID is {{sys.userId}}"` |

### 节点配置面板实现

```typescript
// 用户点击节点时，显示配置面板
function showNodeConfigPanel(nodeId: string) {
  const node = getNodeById(nodeId);
  
  // 1. 查询节点定义（获取输入字段）
  const nodeDefine = await fetchNodeDefine({
    nodeType: node.type,
    pluginId: node.data.pluginId,  // 如果是 Plugin 节点
    modelId: node.data.modelId,    // 如果是 AiChat 节点
    wikiId: node.data.wikiId       // 如果是 Wiki 节点
  });
  
  // 2. 根据 inputFields 渲染表单
  const form = renderConfigForm(nodeDefine.inputFields);
  
  // 3. 填充现有配置
  fillFormValues(form, node.functionDesign.fieldDesigns);
  
  // 4. 用户修改后保存
  form.onSubmit((values) => {
    updateNodeConfig(nodeId, values);
  });
}
```


### 变量引用选择器

为了方便用户配置变量引用，建议实现一个变量选择器：

```typescript
interface VariableOption {
  label: string;
  value: string;
  type: string;
  description?: string;
}

// 获取可用变量列表
function getAvailableVariables(currentNodeKey: string): VariableOption[] {
  const variables: VariableOption[] = [];
  
  // 1. 系统变量
  variables.push(
    { label: '用户 ID', value: 'sys.userId', type: 'Number' },
    { label: '团队 ID', value: 'sys.teamId', type: 'Number' },
    { label: '执行时间', value: 'sys.timestamp', type: 'Number' }
  );
  
  // 2. 输入参数
  variables.push(
    { label: '输入参数', value: 'input.*', type: 'Object' }
  );
  
  // 3. 前置节点输出
  const previousNodes = getPreviousNodes(currentNodeKey);
  previousNodes.forEach(node => {
    const nodeDefine = getNodeDefine(node.type);
    nodeDefine.outputFields.forEach(field => {
      variables.push({
        label: `${node.name} - ${field.fieldName}`,
        value: `${node.nodeKey}.${field.fieldName}`,
        type: field.fieldType,
        description: field.description
      });
    });
  });
  
  return variables;
}
```

### 保存工作流

```typescript
async function saveWorkflow(appId: string) {
  // 1. 收集 UI 设计数据
  const uiDesign = {
    nodes: canvasNodes.map(node => ({
      id: node.id,
      type: node.type,
      position: node.position,
      data: node.data
    })),
    edges: canvasEdges.map(edge => ({
      id: edge.id,
      source: edge.source,
      target: edge.target,
      label: edge.label
    })),
    viewport: getViewport()
  };
  
  // 2. 收集功能设计数据
  const functionDesign = {
    nodes: canvasNodes.map(node => ({
      nodeKey: node.id,
      name: node.data.label || node.type,
      nodeType: node.type,
      fieldDesigns: node.fieldDesigns || []
    })),
    connections: canvasEdges.map(edge => ({
      sourceNodeKey: edge.source,
      targetNodeKey: edge.target,
      label: edge.label
    }))
  };
  
  // 3. 调用更新接口
  await updateWorkflowDefinition({
    appId,
    uiDesignDraft: uiDesign,
    functionDesignDraft: functionDesign
  });
}
```


---

## 工作流执行

### 执行工作流

**API 接口**：
```
POST /app/workflow/execute
```

**请求示例**：
```json
{
  "appId": "550e8400-e29b-41d4-a716-446655440000",
  "parameters": {
    "customerName": "张三",
    "question": "如何重置密码？"
  }
}
```

**响应格式**：Server-Sent Events (SSE) 流式传输

```typescript
// 前端实现示例
async function executeWorkflow(appId: string, parameters: any) {
  const eventSource = new EventSource(
    `/app/workflow/execute?appId=${appId}&parameters=${JSON.stringify(parameters)}`
  );
  
  eventSource.onmessage = (event) => {
    const data = JSON.parse(event.data);
    
    switch (data.type) {
      case 'node_start':
        console.log(`节点开始: ${data.nodeKey}`);
        updateNodeStatus(data.nodeKey, 'running');
        break;
        
      case 'node_complete':
        console.log(`节点完成: ${data.nodeKey}`, data.output);
        updateNodeStatus(data.nodeKey, 'completed');
        updateNodeOutput(data.nodeKey, data.output);
        break;
        
      case 'node_error':
        console.error(`节点错误: ${data.nodeKey}`, data.error);
        updateNodeStatus(data.nodeKey, 'failed');
        showError(data.error);
        break;
        
      case 'workflow_complete':
        console.log('工作流完成', data.result);
        showResult(data.result);
        eventSource.close();
        break;
        
      case 'workflow_error':
        console.error('工作流错误', data.error);
        showError(data.error);
        eventSource.close();
        break;
    }
  };
  
  eventSource.onerror = (error) => {
    console.error('连接错误', error);
    eventSource.close();
  };
}
```

### 查询执行历史

**API 接口**：
```
GET /app/workflow/instance/{instanceId}
```

**响应示例**：
```json
{
  "id": "770e8400-e29b-41d4-a716-446655440002",
  "appId": "550e8400-e29b-41d4-a716-446655440000",
  "state": "Completed",
  "startTime": "2024-01-15T12:00:00Z",
  "endTime": "2024-01-15T12:00:15Z",
  "duration": 15000,
  "parameters": {
    "customerName": "张三",
    "question": "如何重置密码？"
  },
  "result": {
    "answer": "您可以通过以下步骤重置密码..."
  },
  "nodeExecutions": [
    {
      "nodeKey": "node_1",
      "nodeName": "开始",
      "nodeType": "Start",
      "state": "Completed",
      "startTime": "2024-01-15T12:00:00Z",
      "endTime": "2024-01-15T12:00:01Z",
      "input": {},
      "output": {
        "parameters": {
          "customerName": "张三",
          "question": "如何重置密码？"
        }
      }
    }
  ]
}
```


### 执行历史列表

**API 接口**：
```
GET /app/workflow/instance?appId={appId}&page={page}&pageSize={pageSize}
```

**响应示例**：
```json
{
  "total": 100,
  "page": 1,
  "pageSize": 20,
  "items": [
    {
      "id": "770e8400-e29b-41d4-a716-446655440002",
      "appId": "550e8400-e29b-41d4-a716-446655440000",
      "state": "Completed",
      "startTime": "2024-01-15T12:00:00Z",
      "endTime": "2024-01-15T12:00:15Z",
      "duration": 15000
    }
  ]
}
```

---

## API 接口详细说明

### 应用管理接口

#### 1. 创建应用（包括工作流）

```
POST /app/team/create
Content-Type: application/json
```

**请求参数**：
```typescript
interface CreateAppRequest {
  name: string;           // 应用名称（必需）
  description?: string;   // 应用描述
  avatar?: string;        // 应用头像 URL
  appType: number;        // 应用类型：0=Chat, 1=Agent, 2=Workflow
  teamId: number;         // 团队 ID（必需）
  classifyId?: number;    // 分类 ID
}
```

**响应**：
```typescript
interface CreateAppResponse {
  appId: string;          // 应用 ID（UUID）
  name: string;
  appType: number;
  createTime: string;     // ISO 8601 格式
}
```

#### 2. 删除应用

```
DELETE /app/team/delete
Content-Type: application/json
```

**请求参数**：
```typescript
interface DeleteAppRequest {
  appId: string;          // 应用 ID（必需）
}
```

#### 3. 启用/禁用应用

```
POST /app/team/set_disable
Content-Type: application/json
```

**请求参数**：
```typescript
interface SetDisableRequest {
  appId: string;          // 应用 ID（必需）
  isDisabled: boolean;    // true=禁用, false=启用
}
```


### 工作流定义接口

#### 1. 查询工作流定义

```
GET /app/workflow/definition/{appId}
```

**响应**：
```typescript
interface WorkflowDefinitionResponse {
  id: string;                    // 工作流设计 ID
  appId: string;                 // 应用 ID
  name: string;                  // 工作流名称（来自 AppEntity）
  description?: string;          // 工作流描述（来自 AppEntity）
  avatar?: string;               // 工作流头像（来自 AppEntity）
  isPublish: boolean;            // 是否已发布
  uiDesign?: UiDesign;           // 发布版本的 UI 设计
  uiDesignDraft?: UiDesign;      // 草稿版本的 UI 设计
  functionDesign?: FunctionDesign;      // 发布版本的功能设计
  functionDesignDraft?: FunctionDesign; // 草稿版本的功能设计
  createTime: string;
  updateTime: string;
}
```

#### 2. 更新工作流定义（草稿）

```
PUT /app/workflow/definition/{appId}
Content-Type: application/json
```

**请求参数**：
```typescript
interface UpdateWorkflowDefinitionRequest {
  appId: string;                        // 应用 ID（必需）
  name?: string;                        // 更新应用名称（可选）
  description?: string;                 // 更新应用描述（可选）
  avatar?: string;                      // 更新应用头像（可选）
  uiDesignDraft?: UiDesign;            // 更新 UI 设计草稿
  functionDesignDraft?: FunctionDesign; // 更新功能设计草稿
}
```

**注意**：
- `name`, `description`, `avatar` 会更新到 `AppEntity`
- `uiDesignDraft`, `functionDesignDraft` 会更新到 `AppWorkflowDesignEntity`
- 草稿可以多次修改，不影响已发布版本

#### 3. 发布工作流定义

```
POST /app/workflow/definition/{appId}/publish
Content-Type: application/json
```

**请求参数**：
```typescript
interface PublishWorkflowDefinitionRequest {
  appId: string;          // 应用 ID（必需）
}
```

**响应**：
```typescript
interface PublishWorkflowDefinitionResponse {
  success: boolean;
  message: string;
  version?: number;       // 版本号
  publishTime: string;    // 发布时间
}
```

**发布流程**：
1. 验证草稿的完整性（必须有 Start 和 End 节点）
2. 验证所有连接的有效性
3. 创建版本快照（保存当前发布版本）
4. 将草稿复制到发布字段
5. 设置 `isPublish = true`


### 节点定义接口

#### 1. 查询单个节点定义

```
POST /app/workflow/node-define/query
Content-Type: application/json
```

**请求参数**：
```typescript
interface QueryNodeDefineRequest {
  nodeType: string;       // 节点类型（必需）
  pluginId?: number;      // 插件 ID（Plugin 节点必需）
  modelId?: number;       // 模型 ID（AiChat 节点可选）
  wikiId?: number;        // 知识库 ID（Wiki 节点可选）
}
```

**响应**：
```typescript
interface NodeDefineResponse {
  nodeType: string;              // 节点类型
  nodeTypeName: string;          // 节点类型名称
  description: string;           // 节点描述
  pluginId?: number;             // 插件 ID（Plugin 节点）
  pluginName?: string;           // 插件名称（Plugin 节点）
  modelId?: number;              // 模型 ID（AiChat 节点）
  modelName?: string;            // 模型名称（AiChat 节点）
  wikiId?: number;               // 知识库 ID（Wiki 节点）
  wikiName?: string;             // 知识库名称（Wiki 节点）
  inputFields: FieldDefine[];    // 输入字段定义
  outputFields: FieldDefine[];   // 输出字段定义
  supportsStreaming: boolean;    // 是否支持流式输出
  icon: string;                  // 节点图标
  color: string;                 // 节点颜色（十六进制）
}

interface FieldDefine {
  fieldName: string;      // 字段名称
  fieldType: string;      // 字段类型（String, Number, Boolean, Object, Array, Dynamic）
  isRequired: boolean;    // 是否必需
  description: string;    // 字段描述
}
```

#### 2. 批量查询节点定义

```
POST /app/workflow/node-define/batch-query
Content-Type: application/json
```

**请求参数**：
```typescript
interface BatchQueryNodeDefineRequest {
  requests: NodeDefineRequest[];  // 最多 50 个请求
}

interface NodeDefineRequest {
  requestId?: string;     // 请求 ID（可选，用于关联响应）
  nodeType: string;       // 节点类型（必需）
  pluginId?: number;      // 插件 ID（Plugin 节点必需）
  modelId?: number;       // 模型 ID（AiChat 节点可选）
  wikiId?: number;        // 知识库 ID（Wiki 节点可选）
}
```

**响应**：
```typescript
interface BatchQueryNodeDefineResponse {
  nodeDefines: NodeDefineResponseItem[];  // 成功的节点定义
  errors: NodeDefineErrorItem[];          // 失败的请求
}

interface NodeDefineResponseItem extends NodeDefineResponse {
  requestId?: string;     // 请求 ID（如果提供）
}

interface NodeDefineErrorItem {
  requestId?: string;     // 请求 ID（如果提供）
  errorCode: string;      // 错误代码
  errorMessage: string;   // 错误消息
}
```


### 工作流执行接口

#### 1. 执行工作流

```
POST /app/workflow/execute
Content-Type: application/json
Accept: text/event-stream
```

**请求参数**：
```typescript
interface ExecuteWorkflowRequest {
  appId: string;          // 应用 ID（必需）
  parameters?: any;       // 输入参数（JSON 对象）
}
```

**响应格式**：Server-Sent Events (SSE)

**事件类型**：

1. **node_start** - 节点开始执行
```json
{
  "type": "node_start",
  "nodeKey": "node_1",
  "nodeName": "开始",
  "nodeType": "Start",
  "timestamp": "2024-01-15T12:00:00Z"
}
```

2. **node_complete** - 节点执行完成
```json
{
  "type": "node_complete",
  "nodeKey": "node_1",
  "nodeName": "开始",
  "nodeType": "Start",
  "output": {
    "parameters": { "name": "张三" }
  },
  "timestamp": "2024-01-15T12:00:01Z"
}
```

3. **node_error** - 节点执行失败
```json
{
  "type": "node_error",
  "nodeKey": "node_2",
  "nodeName": "AI 对话",
  "nodeType": "AiChat",
  "error": {
    "code": "MODEL_NOT_FOUND",
    "message": "AI 模型不存在"
  },
  "timestamp": "2024-01-15T12:00:05Z"
}
```

4. **workflow_complete** - 工作流执行完成
```json
{
  "type": "workflow_complete",
  "instanceId": "770e8400-e29b-41d4-a716-446655440002",
  "result": {
    "answer": "您可以通过以下步骤重置密码..."
  },
  "duration": 15000,
  "timestamp": "2024-01-15T12:00:15Z"
}
```

5. **workflow_error** - 工作流执行失败
```json
{
  "type": "workflow_error",
  "error": {
    "code": "VALIDATION_ERROR",
    "message": "工作流定义无效：缺少结束节点"
  },
  "timestamp": "2024-01-15T12:00:02Z"
}
```


#### 2. 查询执行实例详情

```
GET /app/workflow/instance/{instanceId}
```

**响应**：
```typescript
interface WorkflowInstanceResponse {
  id: string;                    // 实例 ID
  appId: string;                 // 应用 ID
  workflowDesignId: string;      // 工作流设计 ID
  state: string;                 // 状态：Pending, Running, Completed, Failed
  startTime: string;             // 开始时间
  endTime?: string;              // 结束时间
  duration?: number;             // 执行时长（毫秒）
  parameters?: any;              // 输入参数
  result?: any;                  // 执行结果
  error?: {                      // 错误信息（如果失败）
    code: string;
    message: string;
  };
  nodeExecutions: NodeExecution[];  // 节点执行记录
}

interface NodeExecution {
  nodeKey: string;               // 节点 key
  nodeName: string;              // 节点名称
  nodeType: string;              // 节点类型
  state: string;                 // 状态：Pending, Running, Completed, Failed
  startTime: string;             // 开始时间
  endTime?: string;              // 结束时间
  duration?: number;             // 执行时长（毫秒）
  input?: any;                   // 输入数据
  output?: any;                  // 输出数据
  error?: {                      // 错误信息（如果失败）
    code: string;
    message: string;
  };
}
```

#### 3. 查询执行历史列表

```
GET /app/workflow/instance?appId={appId}&page={page}&pageSize={pageSize}&state={state}
```

**查询参数**：
- `appId` (必需): 应用 ID
- `page` (可选): 页码，默认 1
- `pageSize` (可选): 每页数量，默认 20
- `state` (可选): 状态筛选（Pending, Running, Completed, Failed）

**响应**：
```typescript
interface WorkflowInstanceListResponse {
  total: number;                 // 总数
  page: number;                  // 当前页
  pageSize: number;              // 每页数量
  items: WorkflowInstanceSummary[];  // 实例列表
}

interface WorkflowInstanceSummary {
  id: string;                    // 实例 ID
  appId: string;                 // 应用 ID
  state: string;                 // 状态
  startTime: string;             // 开始时间
  endTime?: string;              // 结束时间
  duration?: number;             // 执行时长（毫秒）
}
```

