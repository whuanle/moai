# 节点定义查询功能实现总结

## 实现概述

为工作流设计器提供了灵活的节点定义查询功能，支持：
1. **单个节点定义查询** - 获取特定节点类型的详细定义
2. **批量节点定义查询（聚合 API）** - 一次性获取多个节点定义，减少 API 调用

## 已创建的文件

### 1. Shared 层（Commands & Responses）

#### Commands
- `QueryNodeDefineCommand.cs` - 单个节点定义查询命令
  - 支持所有 NodeType
  - Plugin 节点需要 PluginId（必需）
  - AiChat 节点可选 ModelId
  - Wiki 节点可选 WikiId

- `QueryBatchNodeDefineCommand.cs` - 批量节点定义查询命令
  - 支持混合查询不同类型的节点
  - 每个请求可以有独立的 RequestId
  - 单次最多查询 50 个节点定义
  - 包含 `NodeDefineRequest` 和验证器

#### Responses
- `QueryNodeDefineCommandResponse.cs` - 单个节点定义响应
  - 节点类型和名称
  - 输入输出字段定义
  - 特定节点的额外信息（PluginId、ModelId、WikiId 等）
  - UI 相关信息（Icon、Color）
  - 是否支持流式输出

- `QueryBatchNodeDefineCommandResponse.cs` - 批量查询响应
  - `NodeDefineResponseItem` - 成功的节点定义（继承自单个响应）
  - `NodeDefineErrorItem` - 失败的请求错误信息
  - 部分失败不影响其他请求

### 2. Core 层（Handlers）

#### Handlers
- `QueryNodeDefineCommandHandler.cs` - 单个节点定义查询处理器
  - 为每种 NodeType 提供专门的定义生成方法
  - **固定节点**（Start、End、Condition、ForEach、Fork、JavaScript、DataProcess）
    - 返回预定义的输入输出字段
    - 包含节点描述、图标、颜色等 UI 信息
  
  - **动态节点**（Plugin、AiChat、Wiki）
    - Plugin：从数据库查询插件信息，解析插件元数据
    - AiChat：可选查询 AI 模型信息
    - Wiki：可选查询知识库信息

- `QueryBatchNodeDefineCommandHandler.cs` - 批量查询处理器
  - 并行处理所有请求（使用 Task.WhenAll）
  - 每个请求独立处理，互不影响
  - 捕获单个请求的错误，不中断其他请求
  - 分离成功和失败的结果

### 3. API 层（Controller）

- `WorkflowNodeDefineController.cs` - 节点定义 API 控制器
  - 路由：`/app/workflow/node-define`
  - 端点：
    - `POST /query` - 查询单个节点定义
    - `POST /batch-query` - 批量查询节点定义

### 4. 文档

- `NODE_DEFINE_API.md` - API 使用指南
  - 详细的 API 说明和示例
  - 节点类型说明表
  - 字段类型说明
  - 使用场景和最佳实践
  - 前端集成示例代码

## 节点定义详情

### 固定节点（无需额外参数）

| NodeType | 输入字段 | 输出字段 | 特点 |
|----------|---------|---------|------|
| Start | parameters (Object, 可选) | parameters (Object) | 工作流入口 |
| End | result (Dynamic, 可选) | 无 | 工作流终点 |
| Condition | condition (String) | result (Boolean) | 条件分支 |
| ForEach | collection (Array), itemVariable (String, 可选) | results (Array) | 循环迭代 |
| Fork | branches (Array) | results (Object) | 并行分支 |
| JavaScript | code (String), timeout (Number, 可选) | result (Dynamic) | 代码执行 |
| DataProcess | data (Dynamic), operation (String), expression (String) | result (Dynamic) | 数据转换 |

### 动态节点（需要额外参数）

| NodeType | 必需参数 | 可选参数 | 输入字段 | 输出字段 |
|----------|---------|---------|---------|---------|
| Plugin | pluginId | - | 根据插件元数据动态生成 | 根据插件元数据动态生成 |
| AiChat | - | modelId | prompt, modelId, temperature, maxTokens | response, usage |
| Wiki | - | wikiId | query, topK, wikiIds | documents, count |

## 关键设计特点

### 1. 灵活性
- 支持固定节点和动态节点
- Plugin 节点根据 PluginId 动态生成定义
- AiChat 和 Wiki 节点可选提供额外信息

### 2. 性能优化
- 批量查询减少 API 调用次数
- 并行处理提高响应速度
- 部分失败不影响整体结果

### 3. 错误处理
- 单个查询：直接抛出异常
- 批量查询：收集错误，不中断其他请求
- 提供详细的错误信息（RequestId、ErrorCode、ErrorMessage）

### 4. UI 友好
- 提供 Icon 和 Color 字段用于 UI 渲染
- 提供 SupportsStreaming 标识
- 详细的字段描述信息

### 5. 可扩展性
- 易于添加新的节点类型
- 易于扩展节点定义信息
- 支持插件元数据的动态解析

## 使用流程

### 前端工作流设计器初始化

```
1. 用户打开设计器
   ↓
2. 调用批量查询 API，获取所有常用节点定义
   POST /app/workflow/node-define/batch-query
   {
     "requests": [
       { "nodeType": "Start" },
       { "nodeType": "End" },
       { "nodeType": "AiChat" },
       { "nodeType": "Wiki" },
       { "nodeType": "Condition" },
       ...
     ]
   }
   ↓
3. 渲染节点面板，显示所有可用节点
   ↓
4. 用户拖拽节点到画布
   ↓
5. 根据节点定义的 inputFields 渲染参数表单
```

### 用户选择插件

```
1. 用户点击添加 Plugin 节点
   ↓
2. 显示插件选择器
   ↓
3. 用户选择插件（pluginId: 123）
   ↓
4. 调用单个查询 API
   POST /app/workflow/node-define/query
   {
     "nodeType": "Plugin",
     "pluginId": 123
   }
   ↓
5. 根据返回的 inputFields 渲染插件参数表单
```

### 批量加载多个插件

```
1. 用户需要了解多个插件的定义
   ↓
2. 调用批量查询 API
   POST /app/workflow/node-define/batch-query
   {
     "requests": [
       { "nodeType": "Plugin", "pluginId": 123, "requestId": "p1" },
       { "nodeType": "Plugin", "pluginId": 456, "requestId": "p2" },
       { "nodeType": "Plugin", "pluginId": 789, "requestId": "p3" }
     ]
   }
   ↓
3. 处理响应
   - nodeDefines: 成功的插件定义
   - errors: 失败的插件（如不存在）
```

## 待完善事项

### 1. Plugin 元数据解析 ✅
已完成插件元数据解析功能：

**实现内容**：
- 支持三种插件类型的解析：
  - **Native Plugin**（原生插件）：从 `PluginNativeEntity` 和 `NativePluginFactory` 获取参数定义
  - **Tool Plugin**（工具插件）：从 `PluginToolEntity` 和 `NativePluginFactory` 获取参数定义
  - **Custom Plugin**（自定义插件）：从 `PluginCustomEntity` 和 `PluginFunctionEntity` 获取函数列表

**字段类型映射**：
```csharp
PluginConfigFieldType → FieldType
- String/Code → String
- Number/Integer → Number
- Boolean → Boolean
- Object/Map → Object
- Array → Array
```

**输入字段**：
- Native/Tool Plugin：根据插件模板的 `ParamsFieldTemplates` 动态生成
- Custom Plugin：提供通用字段（functionName, parameters）

**输出字段**：
- 所有插件类型都返回 `result` 字段（Dynamic 类型）
- Custom Plugin 额外返回 `availableFunctions` 字段（可用函数列表）

**依赖注入**：
- 在 Handler 中注入 `INativePluginFactory` 以访问插件模板信息

### 2. 缓存优化 💡
建议添加缓存机制：
- 固定节点定义可以永久缓存
- Plugin、AiChat、Wiki 节点定义可以短期缓存
- 使用 IMemoryCache 或 Redis

### 3. 权限控制 💡
建议添加权限检查：
- 验证用户是否有权访问特定插件
- 验证用户是否有权访问特定 AI 模型
- 验证用户是否有权访问特定知识库

### 4. 国际化支持 💡
建议添加多语言支持：
- 节点名称和描述的多语言版本
- 字段描述的多语言版本

## 测试建议

### 单元测试
1. 测试每种 NodeType 的定义生成
2. 测试 Plugin 节点的数据库查询
3. 测试批量查询的并行处理
4. 测试错误处理逻辑

### 集成测试
1. 测试 API 端点的请求和响应
2. 测试批量查询的部分失败场景
3. 测试不存在的 PluginId、ModelId、WikiId

### 性能测试
1. 测试批量查询 50 个节点的响应时间
2. 测试并发请求的处理能力

## 总结

本次实现提供了一个灵活、高效的节点定义查询系统，主要特点：

✅ **灵活性** - 支持固定节点和动态节点，满足不同场景需求
✅ **性能** - 批量查询和并行处理，减少 API 调用和响应时间
✅ **容错性** - 部分失败不影响整体，提供详细的错误信息
✅ **UI 友好** - 提供丰富的 UI 相关信息（图标、颜色、描述）
✅ **可扩展** - 易于添加新节点类型和扩展定义信息
✅ **文档完善** - 提供详细的 API 使用指南和示例
✅ **插件元数据解析** - 完整支持 Native、Tool、Custom 三种插件类型的动态字段生成

这个系统为工作流设计器提供了强大的节点定义查询能力，帮助用户更好地理解和配置工作流节点。

## 相关文档

- [NODE_DEFINE_API.md](.kiro/specs/ai-workflow/NODE_DEFINE_API.md) - API 使用指南
- [PLUGIN_METADATA_PARSING.md](.kiro/specs/ai-workflow/PLUGIN_METADATA_PARSING.md) - 插件元数据解析实现详情
