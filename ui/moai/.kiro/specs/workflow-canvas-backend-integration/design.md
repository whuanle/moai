# Design Document: 工作流画布后端集成

## Overview

本设计文档描述了工作流画布编辑器与后端 API 的集成方案。该功能将现有的基于 mock 数据的工作流画布编辑器与后端 API 连接，实现工作流的加载、保存和动态节点定义查询。

### 核心目标

1. **数据加载**: 从后端 API 加载工作流定义并转换为画布格式
2. **数据保存**: 将画布数据转换为后端格式并保存
3. **动态定义**: 为实例节点（Plugin、Wiki、AiModel）动态查询字段定义
4. **缓存优化**: 缓存节点定义避免重复 API 调用
5. **错误处理**: 提供友好的错误提示和恢复机制

### 技术栈

- **API Client**: Microsoft Kiota 生成的 TypeScript 客户端
- **State Management**: Zustand (useWorkflowStore)
- **Canvas Editor**: @flowgram.ai/free-layout-editor
- **Error Handling**: proxyRequestError 工具函数
- **UI Framework**: Ant Design 5

## Architecture

### 数据流架构

```
┌─────────────────┐
│   Backend API   │
│  (4 Endpoints)  │
└────────┬────────┘
         │
         ├─ config (POST)      ─┐
         ├─ query_define (POST) │ API Layer
         ├─ update (PUT)        │
         └─ list (POST)        ─┘
         │
┌────────▼────────────────────────────────┐
│      API Integration Layer              │
│  - workflowApiService.ts                │
│  - Data conversion & caching            │
└────────┬────────────────────────────────┘
         │
┌────────▼────────────────────────────────┐
│      Zustand Store Layer                │
│  - useWorkflowStore.ts                  │
│  - Backend data + Canvas data           │
└────────┬────────────────────────────────┘
         │
┌────────▼────────────────────────────────┐
│      Converter Layer                    │
│  - workflowConverter.ts                 │
│  - toEditorFormat / fromEditorFormat    │
└────────┬────────────────────────────────┘
         │
┌────────▼────────────────────────────────┐
│      Canvas Editor                      │
│  - FreeLayoutEditor                     │
│  - WorkflowJSON format                  │
└─────────────────────────────────────────┘
```


### 数据分离架构

系统维护两套独立的数据结构：

**后端逻辑数据 (BackendWorkflowData)**:
- 节点配置（输入/输出字段、执行参数）
- 连接逻辑（字段映射、条件表达式）
- 执行策略（超时、重试、错误处理）
- 全局变量和元数据

**画布 UI 数据 (CanvasWorkflowData)**:
- 节点位置和尺寸
- UI 状态（展开/折叠、选中、高亮）
- 连接样式（线型、颜色）
- 视图状态（缩放、偏移）

### 后端 API 接口

#### 1. 获取工作流定义 (config)

```typescript
POST /api/team/workflowapp/config

Request: QueryWorkflowDefinitionCommand {
  appId: Guid;
  teamId: number;
}

Response: QueryWorkflowDefinitionCommandResponse {
  id: Guid;
  appId: Guid;
  name: string;
  description: string;
  avatar: string;
  teamId: number;
  uiDesign: string;              // JSON 字符串
  uiDesignDraft: string;         // JSON 字符串
  functionDesign: string;        // JSON 字符串
  functionDesignDraft: string;   // JSON 字符串
  isPublish: boolean;
  createTime: string;
  updateTime: string;
}
```

#### 2. 查询节点定义 (query_define)

```typescript
POST /api/team/workflowapp/query_define

Request: QueryNodeDefineCommand {
  teamId: number;
  nodes: Array<{
    key: NodeType;
    value: string[];  // 实例 ID 列表
  }>;
}

Response: QueryNodeDefineCommandResponse {
  nodes: NodeDefineItem[];
}

NodeDefineItem {
  nodeType: NodeType;
  nodeTypeName: string;
  description: string;
  icon: string;
  color: string;
  inputFields: FieldDefine[];
  outputFields: FieldDefine[];
  supportsStreaming: boolean;
  // 实例特定字段
  pluginId?: number;
  pluginName?: string;
  wikiId?: number;
  wikiName?: string;
  modelId?: number;
  modelName?: string;
}
```

#### 3. 更新工作流定义 (update)

```typescript
PUT /api/team/workflowapp/update

Request: UpdateWorkflowDefinitionCommand {
  appId: Guid;
  teamId: number;
  name: string;
  description: string;
  avatar: string;
  nodes: NodeDesign[];
  connections: Connection[];
  uiDesignDraft: string;  // JSON 字符串
}

Response: EmptyCommandResponse {}
```

#### 4. 获取工作流列表 (list)

```typescript
POST /api/team/workflowapp/list

Request: QueryWorkflowDefinitionListCommand {
  teamId: number;
  keyword: string;
  pageIndex: number;
  pageSize: number;
}

Response: QueryWorkflowDefinitionListCommandResponse {
  items: WorkflowListItem[];
  totalCount: number;
  pageIndex: number;
  pageSize: number;
}
```


## Components and Interfaces

### 1. API Service Layer (workflowApiService.ts)

封装所有工作流相关的 API 调用，提供统一的接口。

```typescript
/**
 * 工作流 API 服务
 */
export class WorkflowApiService {
  private apiClient: MoAIClient;
  private nodeDefineCache: Map<string, NodeDefineItem>;
  
  constructor() {
    this.apiClient = GetApiClient();
    this.nodeDefineCache = new Map();
  }
  
  /**
   * 加载工作流定义
   */
  async loadWorkflow(appId: string, teamId: number): Promise<WorkflowData> {
    const response = await this.apiClient.api.team.workflowapp.config.post({
      appId,
      teamId
    });
    
    return this.parseWorkflowResponse(response);
  }
  
  /**
   * 保存工作流定义
   */
  async saveWorkflow(
    appId: string,
    teamId: number,
    workflow: BackendWorkflowData,
    uiDesign: string
  ): Promise<void> {
    await this.apiClient.api.team.workflowapp.update.put({
      appId,
      teamId,
      name: workflow.name,
      description: workflow.description,
      nodes: this.convertToNodeDesigns(workflow.nodes),
      connections: this.convertToConnections(workflow.edges),
      uiDesignDraft: uiDesign
    });
  }
  
  /**
   * 批量查询节点定义
   */
  async queryNodeDefinitions(
    teamId: number,
    nodeQueries: Map<NodeType, string[]>
  ): Promise<NodeDefineItem[]> {
    // 检查缓存
    const uncachedQueries = this.filterUncached(nodeQueries);
    
    if (uncachedQueries.size === 0) {
      return this.getCachedDefinitions(nodeQueries);
    }
    
    // 调用 API
    const response = await this.apiClient.api.team.workflowapp.query_define.post({
      teamId,
      nodes: Array.from(uncachedQueries.entries()).map(([nodeType, ids]) => ({
        key: nodeType,
        value: ids
      }))
    });
    
    // 更新缓存
    response.nodes?.forEach(item => {
      const cacheKey = this.getCacheKey(item);
      this.nodeDefineCache.set(cacheKey, item);
    });
    
    return response.nodes || [];
  }
  
  /**
   * 查询单个节点定义
   */
  async queryNodeDefinition(
    teamId: number,
    nodeType: NodeType,
    instanceId?: string
  ): Promise<NodeDefineItem | null> {
    const cacheKey = this.getCacheKey({ nodeType, instanceId });
    
    // 检查缓存
    if (this.nodeDefineCache.has(cacheKey)) {
      return this.nodeDefineCache.get(cacheKey)!;
    }
    
    // 调用批量查询 API
    const queries = new Map([[nodeType, instanceId ? [instanceId] : []]]);
    const results = await this.queryNodeDefinitions(teamId, queries);
    
    return results.find(r => 
      r.nodeType === nodeType && 
      this.matchesInstance(r, instanceId)
    ) || null;
  }
  
  /**
   * 清除缓存
   */
  clearCache(): void {
    this.nodeDefineCache.clear();
  }
  
  /**
   * 清除特定节点的缓存
   */
  clearNodeCache(nodeType: NodeType, instanceId?: string): void {
    const cacheKey = this.getCacheKey({ nodeType, instanceId });
    this.nodeDefineCache.delete(cacheKey);
  }
  
  private getCacheKey(item: { nodeType?: NodeType; instanceId?: string }): string {
    const { nodeType, instanceId } = item;
    return instanceId ? `${nodeType}:${instanceId}` : nodeType || '';
  }
  
  private parseWorkflowResponse(
    response: QueryWorkflowDefinitionCommandResponse
  ): WorkflowData {
    // 优先使用草稿版本
    const uiDesign = response.uiDesignDraft || response.uiDesign || '{}';
    const functionDesign = response.functionDesignDraft || response.functionDesign || '{}';
    
    return {
      id: response.id!,
      appId: response.appId!,
      name: response.name || '未命名工作流',
      description: response.description || '',
      uiDesign: JSON.parse(uiDesign),
      functionDesign: JSON.parse(functionDesign),
      isPublish: response.isPublish || false,
      isDraft: !!response.uiDesignDraft
    };
  }
}
```


### 2. Store Integration (useWorkflowStore.ts)

扩展现有的 Zustand store，添加 API 集成功能。

```typescript
/**
 * 扩展 WorkflowActions 接口
 */
export interface WorkflowActions {
  // ... 现有方法 ...
  
  /**
   * 从后端加载工作流
   */
  loadFromApi: (appId: string, teamId: number) => Promise<void>;
  
  /**
   * 保存工作流到后端
   */
  saveToApi: () => Promise<boolean>;
  
  /**
   * 查询节点定义并更新节点
   */
  queryAndUpdateNodeDefinition: (
    nodeId: string,
    nodeType: NodeType,
    instanceId?: string
  ) => Promise<void>;
  
  /**
   * 批量查询节点定义
   */
  batchQueryNodeDefinitions: (
    nodeQueries: Array<{ nodeId: string; nodeType: NodeType; instanceId?: string }>
  ) => Promise<void>;
}

/**
 * 扩展 WorkflowState 接口
 */
export interface WorkflowState {
  // ... 现有字段 ...
  
  /**
   * 应用 ID
   */
  appId: string;
  
  /**
   * 团队 ID
   */
  teamId: number;
  
  /**
   * 是否正在加载
   */
  isLoading: boolean;
  
  /**
   * 加载错误
   */
  loadError: string | null;
  
  /**
   * 是否为草稿版本
   */
  isDraft: boolean;
  
  /**
   * API 服务实例
   */
  apiService: WorkflowApiService;
}
```

### 3. Data Converter Enhancement (workflowConverter.ts)

增强现有的数据转换器，支持后端格式转换。

```typescript
/**
 * 将后端 FunctionDesign 转换为 BackendWorkflowData
 */
export function parseFunctionDesign(
  functionDesign: string
): Partial<BackendWorkflowData> {
  const data = JSON.parse(functionDesign);
  
  return {
    nodes: data.nodes?.map((node: any) => ({
      id: node.nodeKey,
      type: node.nodeType,
      name: node.name,
      description: node.description,
      config: {
        inputFields: node.inputFields || [],
        outputFields: node.outputFields || [],
        settings: node.fieldDesigns || {}
      }
    })) || [],
    edges: data.connections?.map((conn: any, index: number) => ({
      id: `edge_${index}`,
      sourceNodeId: conn.sourceNodeKey,
      targetNodeId: conn.targetNodeKey,
      label: conn.label
    })) || []
  };
}

/**
 * 将 BackendWorkflowData 转换为后端 FunctionDesign
 */
export function toFunctionDesign(
  backend: BackendWorkflowData
): string {
  const data = {
    nodes: backend.nodes.map(node => ({
      nodeKey: node.id,
      nodeType: node.type,
      name: node.name,
      description: node.description,
      inputFields: node.config.inputFields,
      outputFields: node.config.outputFields,
      fieldDesigns: node.config.settings
    })),
    connections: backend.edges.map(edge => ({
      sourceNodeKey: edge.sourceNodeId,
      targetNodeKey: edge.targetNodeId,
      label: edge.label
    }))
  };
  
  return JSON.stringify(data);
}

/**
 * 将 CanvasWorkflowData 转换为后端 UiDesign
 */
export function toUiDesign(canvas: CanvasWorkflowData): string {
  return JSON.stringify({
    nodes: canvas.nodes.map(node => ({
      id: node.id,
      position: node.position,
      ui: node.ui
    })),
    viewport: canvas.viewport
  });
}

/**
 * 解析后端 UiDesign 为 CanvasWorkflowData
 */
export function parseUiDesign(uiDesign: string): Partial<CanvasWorkflowData> {
  const data = JSON.parse(uiDesign);
  
  return {
    nodes: data.nodes?.map((node: any) => ({
      id: node.id,
      type: node.type || NodeType.Start,
      position: node.position || { x: 0, y: 0 },
      ui: node.ui || {
        expanded: true,
        selected: false,
        highlighted: false
      },
      title: node.title || '',
      content: node.content || ''
    })) || [],
    edges: data.edges || [],
    viewport: data.viewport || {
      zoom: 1,
      offsetX: 0,
      offsetY: 0
    }
  };
}
```


### 4. React Component Integration (WorkflowConfig.tsx)

更新主组件以使用 API 集成。

```typescript
export function WorkflowConfig({ appId, teamId }: WorkflowConfigProps) {
  const store = useWorkflowStore();
  const [messageApi, contextHolder] = message.useMessage();
  const [isInitialized, setIsInitialized] = useState(false);
  
  // 组件挂载时加载工作流
  useEffect(() => {
    loadWorkflowData();
  }, [appId, teamId]);
  
  const loadWorkflowData = async () => {
    try {
      store.set({ isLoading: true, loadError: null });
      await store.loadFromApi(appId, teamId);
      setIsInitialized(true);
    } catch (error) {
      console.error('加载工作流失败:', error);
      proxyRequestError(error, messageApi, '加载工作流失败');
      store.set({ loadError: '加载工作流失败，请重试' });
    } finally {
      store.set({ isLoading: false });
    }
  };
  
  const handleSave = async () => {
    try {
      // 验证工作流
      const errors = store.validate();
      if (errors.length > 0) {
        errors.forEach(error => {
          messageApi.error(error.message);
        });
        return;
      }
      
      // 保存到后端
      const success = await store.saveToApi();
      if (success) {
        messageApi.success('保存成功');
      }
    } catch (error) {
      console.error('保存工作流失败:', error);
      proxyRequestError(error, messageApi, '保存工作流失败');
    }
  };
  
  const handleNodeInstanceChange = async (
    nodeId: string,
    nodeType: NodeType,
    instanceId: string
  ) => {
    try {
      await store.queryAndUpdateNodeDefinition(nodeId, nodeType, instanceId);
      messageApi.success('节点定义已更新');
    } catch (error) {
      console.error('查询节点定义失败:', error);
      proxyRequestError(error, messageApi, '查询节点定义失败');
    }
  };
  
  // 加载状态
  if (store.isLoading) {
    return (
      <div className="moai-loading">
        <Spin size="large" tip="加载工作流中..." />
      </div>
    );
  }
  
  // 错误状态
  if (store.loadError) {
    return (
      <div className="moai-empty">
        <Empty
          description={store.loadError}
          image={Empty.PRESENTED_IMAGE_SIMPLE}
        >
          <Button type="primary" onClick={loadWorkflowData}>
            重试
          </Button>
        </Empty>
      </div>
    );
  }
  
  // 正常渲染
  return (
    <div className="workflow-config-container">
      {contextHolder}
      <div className="workflow-toolbar">
        <Space>
          <Button
            type="primary"
            icon={<SaveOutlined />}
            onClick={handleSave}
            loading={store.isSaving}
            disabled={!store.isDirty}
          >
            保存
          </Button>
          {store.isDraft && (
            <Tag color="orange">草稿</Tag>
          )}
          {store.isDirty && (
            <Tag color="red">未保存</Tag>
          )}
        </Space>
      </div>
      
      <FreeLayoutEditorProvider
        initialData={toEditorFormat(store.backend, store.canvas)}
        onContentChange={handleContentChange}
      >
        <WorkflowCanvas />
      </FreeLayoutEditorProvider>
    </div>
  );
}
```


## Data Models

### Backend API Models

#### WorkflowData (内部使用)

```typescript
interface WorkflowData {
  id: Guid;
  appId: Guid;
  name: string;
  description: string;
  uiDesign: any;           // 解析后的 JSON 对象
  functionDesign: any;     // 解析后的 JSON 对象
  isPublish: boolean;
  isDraft: boolean;        // 是否使用草稿版本
}
```

#### NodeDesign (后端格式)

```typescript
interface NodeDesign {
  nodeKey: string;         // 节点唯一标识
  nodeType: NodeType;      // 节点类型
  name: string;            // 节点名称
  description?: string;    // 节点描述
  inputFields: FieldDefine[];
  outputFields: FieldDefine[];
  fieldDesigns?: Record<string, any>;  // 字段配置
  nextNodeKey?: string;    // 下一个节点（已废弃，使用 connections）
}
```

#### Connection (后端格式)

```typescript
interface Connection {
  sourceNodeKey: string;   // 源节点 ID
  targetNodeKey: string;   // 目标节点 ID
  label?: string;          // 连接标签（用于条件分支）
}
```

#### FieldDefine

```typescript
interface FieldDefine {
  fieldName: string;       // 字段名称
  fieldType: FieldType;    // 字段类型
  isRequired: boolean;     // 是否必填
  description?: string;    // 字段描述
}

enum FieldType {
  Empty = 'empty',
  String = 'string',
  Number = 'number',
  Boolean = 'boolean',
  Object = 'object',
  Array = 'array',
  Dynamic = 'dynamic'
}
```

### Instance Node Types

实例节点需要选择具体实例才能获取完整的字段定义：

```typescript
// Plugin 节点
interface PluginNodeInstance {
  nodeType: NodeType.Plugin;
  pluginId: number;
  pluginName: string;
  inputFields: FieldDefine[];   // 从 query_define 获取
  outputFields: FieldDefine[];  // 从 query_define 获取
}

// Wiki 节点
interface WikiNodeInstance {
  nodeType: NodeType.Wiki;
  wikiId: number;
  wikiName: string;
  inputFields: FieldDefine[];   // 从 query_define 获取
  outputFields: FieldDefine[];  // 从 query_define 获取
}

// AiModel 节点
interface AiModelNodeInstance {
  nodeType: NodeType.AiChat;
  modelId: number;
  modelName: string;
  supportsStreaming: boolean;
  inputFields: FieldDefine[];   // 从 query_define 获取
  outputFields: FieldDefine[];  // 从 query_define 获取
}
```

### Cache Structure

```typescript
interface NodeDefinitionCache {
  // 缓存键格式: "nodeType:instanceId" 或 "nodeType"
  cache: Map<string, NodeDefineItem>;
  
  // 缓存时间戳
  timestamps: Map<string, number>;
  
  // 缓存过期时间（毫秒）
  ttl: number;
}
```

### Error Types

```typescript
interface ApiError {
  type: 'network' | 'validation' | 'server' | 'unknown';
  message: string;
  details?: any;
}

interface LoadError extends ApiError {
  canRetry: boolean;
  retryAction?: () => Promise<void>;
}

interface SaveError extends ApiError {
  validationErrors?: ValidationError[];
}
```


## Correctness Properties

*属性（Property）是关于系统行为的形式化陈述，应该在所有有效执行中保持为真。属性是人类可读规范和机器可验证正确性保证之间的桥梁。*

### Property 1: 数据转换往返一致性

*对于任何*有效的工作流数据，将其转换为画布格式再转换回后端格式，应该产生等价的数据结构（忽略 UI 特定字段）。

**Validates: Requirements 1.2, 3.1, 5.1, 5.2**

### Property 2: 草稿版本优先加载

*对于任何*包含草稿版本的工作流响应，加载时应该使用 uiDesignDraft 和 functionDesignDraft 字段而非已发布版本。

**Validates: Requirements 1.5, 8.2**

### Property 3: 节点定义缓存避免重复请求

*对于任何*已查询过的节点定义（nodeType + instanceId 组合），再次查询相同节点时应该使用缓存数据而不触发 API 调用。

**Validates: Requirements 2.3, 7.1**

### Property 4: 节点定义更新字段

*对于任何*从 query_define API 返回的节点定义，应该正确更新对应节点的 inputFields 和 outputFields。

**Validates: Requirements 2.2**

### Property 5: 实例变更清除旧定义

*对于任何*实例节点，当用户更改实例选择时，应该清除旧的字段定义并查询新的定义。

**Validates: Requirements 2.5**

### Property 6: 节点添加应用默认配置

*对于任何*节点类型，添加新节点时应该应用该类型的默认布局配置（位置、尺寸、字段定义）。

**Validates: Requirements 4.1**

### Property 7: 节点位置冲突自动调整

*对于任何*新添加的节点，如果其位置与现有节点重叠，应该自动调整位置避免重叠。

**Validates: Requirements 4.4**

### Property 8: 节点复制保留配置生成新 ID

*对于任何*可复制的节点，复制操作应该保留原节点的所有配置但生成新的唯一节点 ID。

**Validates: Requirements 4.5**

### Property 9: 无效连接自动移除

*对于任何*工作流数据，如果连接引用的源节点或目标节点不存在，应该自动移除该无效连接。

**Validates: Requirements 5.5**

### Property 10: 多节点共享缓存定义

*对于任何*使用相同实例的多个节点（相同 nodeType 和 instanceId），应该共享同一份缓存的节点定义数据。

**Validates: Requirements 7.2**

### Property 11: 快速操作防抖保存

*对于任何*连续的保存请求序列，如果请求间隔小于防抖时间（如 500ms），应该只执行最后一次保存请求。

**Validates: Requirements 7.4**

### Property 12: 批量查询结果正确分配

*对于任何*批量节点定义查询，返回的每个节点定义应该正确分配给对应的节点（通过 nodeType 和 instanceId 匹配）。

**Validates: Requirements 9.2**

### Property 13: 大批量请求自动分批

*对于任何*包含超过阈值数量（如 20 个）节点的批量查询请求，应该自动分批发送多个请求。

**Validates: Requirements 9.4**

### Property 14: 修改操作设置脏标记

*对于任何*工作流修改操作（添加/删除/更新节点或连接），应该将 isDirty 标记设置为 true。

**Validates: Requirements 10.1**

### Property 15: 画布变更同步 Store

*对于任何*画布编辑器的数据变更，应该同步更新 Zustand store 中的对应数据。

**Validates: Requirements 10.3**

### Property 16: Store 变更触发渲染

*对于任何*Zustand store 的数据变更，应该触发画布编辑器的重新渲染以反映最新状态。

**Validates: Requirements 10.4**


## Error Handling

### Error Handling Strategy

所有 API 调用都应该使用 `proxyRequestError` 进行统一的错误处理：

```typescript
try {
  await apiCall();
} catch (error) {
  console.error('操作失败:', error);
  proxyRequestError(error, messageApi, '操作失败');
}
```

### Error Categories

#### 1. Network Errors (网络错误)

- **场景**: 网络连接失败、超时
- **处理**: 显示友好提示，提供重试按钮
- **用户体验**: "网络连接失败，请检查网络后重试"

#### 2. Validation Errors (验证错误)

- **场景**: 工作流结构不合法、必填字段缺失
- **处理**: 高亮显示有问题的节点，显示具体错误信息
- **用户体验**: "节点 'AI 对话' 缺少必填字段: prompt"

#### 3. Server Errors (服务器错误)

- **场景**: 后端服务异常、数据库错误
- **处理**: 显示通用错误提示，记录详细日志
- **用户体验**: "服务器错误，请稍后重试"

#### 4. Authorization Errors (授权错误)

- **场景**: Token 过期、权限不足
- **处理**: 自动跳转到登录页（由 API Client 处理）
- **用户体验**: 自动重定向，无需手动操作

### Error Recovery

#### 加载失败恢复

```typescript
// 显示错误状态和重试按钮
if (store.loadError) {
  return (
    <Empty
      description={store.loadError}
      image={Empty.PRESENTED_IMAGE_SIMPLE}
    >
      <Button type="primary" onClick={loadWorkflowData}>
        重试
      </Button>
    </Empty>
  );
}
```

#### 保存失败恢复

```typescript
// 保持编辑状态，允许用户修改后重新保存
try {
  await store.saveToApi();
} catch (error) {
  // 错误已通过 proxyRequestError 显示
  // 保持 isDirty = true，允许重新保存
}
```

#### 部分失败处理

```typescript
// 批量查询节点定义时，部分失败不影响成功的节点
const results = await Promise.allSettled(queries);
results.forEach((result, index) => {
  if (result.status === 'fulfilled') {
    updateNodeDefinition(queries[index].nodeId, result.value);
  } else {
    console.warn(`节点 ${queries[index].nodeId} 定义查询失败:`, result.reason);
    // 节点保持可编辑，使用默认字段定义
  }
});
```

### Loading States

#### 全局加载状态

```typescript
// 初始加载工作流
if (store.isLoading) {
  return <Spin size="large" tip="加载工作流中..." />;
}
```

#### 操作加载状态

```typescript
// 保存操作
<Button
  type="primary"
  onClick={handleSave}
  loading={store.isSaving}
  disabled={!store.isDirty}
>
  保存
</Button>
```

#### 节点定义查询状态

```typescript
// 节点配置面板显示加载状态
{isLoadingDefinition && (
  <Spin tip="加载节点定义中..." />
)}
```

### Unsaved Changes Warning

```typescript
// 路由守卫：离开前检查未保存的更改
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


## Testing Strategy

### Dual Testing Approach

本项目采用单元测试和属性测试相结合的策略：

- **单元测试**: 验证具体示例、边缘情况和错误条件
- **属性测试**: 验证通用属性在所有输入下的正确性

两者互补，共同确保全面的测试覆盖。

### Unit Testing Focus

单元测试应该专注于：

1. **具体示例**: 验证特定场景的正确行为
2. **边缘情况**: 空数据、单节点工作流、大型工作流
3. **错误条件**: API 失败、网络错误、验证失败
4. **集成点**: 组件间交互、API 调用、状态更新

**避免过多单元测试** - 属性测试已经覆盖了大量输入组合。

### Property-Based Testing

#### 测试库选择

使用 **fast-check** 进行属性测试（TypeScript/JavaScript 的 PBT 库）。

```bash
npm install --save-dev fast-check
```

#### 测试配置

每个属性测试至少运行 **100 次迭代**（由于随机化）：

```typescript
import fc from 'fast-check';

fc.assert(
  fc.property(
    // 生成器
    fc.record({
      nodes: fc.array(nodeArbitrary),
      edges: fc.array(edgeArbitrary)
    }),
    // 属性断言
    (workflow) => {
      // 测试逻辑
    }
  ),
  { numRuns: 100 }  // 最少 100 次迭代
);
```

#### 测试标记

每个属性测试必须使用注释标记引用设计文档中的属性：

```typescript
/**
 * Feature: workflow-canvas-backend-integration
 * Property 1: 数据转换往返一致性
 * 
 * 对于任何有效的工作流数据，将其转换为画布格式再转换回后端格式，
 * 应该产生等价的数据结构（忽略 UI 特定字段）。
 */
test('property: round-trip data conversion consistency', () => {
  fc.assert(
    fc.property(workflowDataArbitrary, (workflowData) => {
      const canvasFormat = toEditorFormat(workflowData.backend, workflowData.canvas);
      const { backend: convertedBackend } = fromEditorFormat(canvasFormat);
      
      expect(convertedBackend.nodes).toHaveLength(workflowData.backend.nodes.length);
      expect(convertedBackend.edges).toHaveLength(workflowData.backend.edges.length);
      
      // 验证节点 ID 匹配
      const originalIds = new Set(workflowData.backend.nodes.map(n => n.id));
      const convertedIds = new Set(convertedBackend.nodes.map(n => n.id));
      expect(convertedIds).toEqual(originalIds);
    }),
    { numRuns: 100 }
  );
});
```

### Test Data Generators

#### 节点生成器

```typescript
const nodeArbitrary = fc.record({
  id: fc.uuid(),
  type: fc.constantFrom(...Object.values(NodeType)),
  name: fc.string({ minLength: 1, maxLength: 50 }),
  description: fc.option(fc.string()),
  config: fc.record({
    inputFields: fc.array(fieldDefineArbitrary),
    outputFields: fc.array(fieldDefineArbitrary),
    settings: fc.dictionary(fc.string(), fc.anything())
  })
});
```

#### 字段定义生成器

```typescript
const fieldDefineArbitrary = fc.record({
  fieldName: fc.string({ minLength: 1, maxLength: 30 }),
  fieldType: fc.constantFrom(...Object.values(FieldType)),
  isRequired: fc.boolean(),
  description: fc.option(fc.string())
});
```

#### 工作流生成器

```typescript
const workflowDataArbitrary = fc.record({
  backend: fc.record({
    id: fc.uuid(),
    name: fc.string(),
    version: fc.constant('1.0.0'),
    nodes: fc.array(nodeArbitrary, { minLength: 2, maxLength: 20 }),
    edges: fc.array(edgeArbitrary)
  }),
  canvas: fc.record({
    nodes: fc.array(canvasNodeArbitrary),
    edges: fc.array(canvasEdgeArbitrary),
    viewport: fc.record({
      zoom: fc.double({ min: 0.1, max: 3 }),
      offsetX: fc.integer({ min: -1000, max: 1000 }),
      offsetY: fc.integer({ min: -1000, max: 1000 })
    })
  })
});
```

### Integration Testing

#### API Mock

使用 MSW (Mock Service Worker) 模拟后端 API：

```typescript
import { rest } from 'msw';
import { setupServer } from 'msw/node';

const server = setupServer(
  rest.post('/api/team/workflowapp/config', (req, res, ctx) => {
    return res(ctx.json(mockWorkflowResponse));
  }),
  
  rest.put('/api/team/workflowapp/update', (req, res, ctx) => {
    return res(ctx.json({ success: true }));
  }),
  
  rest.post('/api/team/workflowapp/query_define', (req, res, ctx) => {
    return res(ctx.json({ nodes: mockNodeDefinitions }));
  })
);

beforeAll(() => server.listen());
afterEach(() => server.resetHandlers());
afterAll(() => server.close());
```

#### Component Testing

使用 React Testing Library 测试组件集成：

```typescript
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

test('loads workflow on mount', async () => {
  render(<WorkflowConfig appId="test-id" teamId={1} />);
  
  await waitFor(() => {
    expect(screen.queryByText('加载工作流中...')).not.toBeInTheDocument();
  });
  
  expect(screen.getByText('保存')).toBeInTheDocument();
});
```

### Performance Testing

#### 缓存效率测试

```typescript
test('cache prevents duplicate API calls', async () => {
  const apiSpy = jest.spyOn(apiService, 'queryNodeDefinitions');
  
  // 第一次查询
  await store.queryAndUpdateNodeDefinition('node1', NodeType.Plugin, 'plugin-123');
  expect(apiSpy).toHaveBeenCalledTimes(1);
  
  // 第二次查询相同节点（应使用缓存）
  await store.queryAndUpdateNodeDefinition('node2', NodeType.Plugin, 'plugin-123');
  expect(apiSpy).toHaveBeenCalledTimes(1);  // 仍然是 1 次
});
```

#### 防抖测试

```typescript
test('debounces rapid save requests', async () => {
  const saveSpy = jest.spyOn(apiService, 'saveWorkflow');
  
  // 快速连续保存
  store.saveToApi();
  store.saveToApi();
  store.saveToApi();
  
  await waitFor(() => {
    expect(saveSpy).toHaveBeenCalledTimes(1);  // 只调用一次
  });
});
```

