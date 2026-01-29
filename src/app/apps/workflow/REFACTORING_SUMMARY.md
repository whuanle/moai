# QueryNodeDefineCommandHandler 重构总结

## 重构目标

将 `QueryNodeDefineCommandHandler` 按 `NodeType` 拆分，支持批量查询多个节点定义。

## 主要变更

### 1. 更新 QueryNodeDefineCommand (Shared 层)

**文件**: `MoAI.App.Workflow.Shared/Queries/QueryNodeDefineCommand.cs`

- 移除单个节点查询的属性 (`NodeType`, `PluginId`, `ModelId`, `WikiId`)
- 新增批量查询参数:
  ```csharp
  public IReadOnlyCollection<KeyValue<NodeType, IReadOnlyCollection<string>>> Nodes { get; init; }
  ```
- Key: 节点类型 (NodeType)
- Value: 实例标识列表 (插件 ID、知识库 ID、模型 ID 等，转换为字符串)

### 2. 更新 QueryNodeDefineCommandResponse (Shared 层)

**文件**: `MoAI.App.Workflow.Shared/Queries/Responses/QueryNodeDefineCommandResponse.cs`

- 将单个节点定义改为节点定义列表:
  ```csharp
  public IReadOnlyList<NodeDefineItem> Nodes { get; init; }
  ```
- 新增 `NodeDefineItem` 类，包含原有的所有节点定义属性

### 3. 新增专用查询命令 (Shared 层)

#### QueryPluginNodeDefineCommand
**文件**: `MoAI.App.Workflow.Shared/Queries/QueryPluginNodeDefineCommand.cs`
- 支持批量查询多个插件的节点定义
- 参数: `IReadOnlyCollection<int> PluginIds`

#### QueryWikiNodeDefineCommand
**文件**: `MoAI.App.Workflow.Shared/Queries/QueryWikiNodeDefineCommand.cs`
- 支持批量查询多个知识库的节点定义
- 参数: `IReadOnlyCollection<int> WikiIds`

#### QueryAiChatNodeDefineCommand
**文件**: `MoAI.App.Workflow.Shared/Queries/QueryAiChatNodeDefineCommand.cs`
- 支持批量查询多个 AI 模型的节点定义
- 参数: `IReadOnlyCollection<int> ModelIds`

### 4. 新增专用响应类型 (Shared 层)

**文件**: `MoAI.App.Workflow.Shared/Queries/Responses/`
- `QueryPluginNodeDefineCommandResponse.cs`
- `QueryWikiNodeDefineCommandResponse.cs`
- `QueryAiChatNodeDefineCommandResponse.cs`

每个响应都包含 `IReadOnlyList<NodeDefineItem> Nodes` 属性

### 5. 新增专用 Handler (Core 层)

#### QueryPluginNodeDefineCommandHandler
**文件**: `MoAI.App.Workflow.Core/Queries/QueryPluginNodeDefineCommandHandler.cs`
- 批量查询插件信息
- 支持 Native、Tool、Custom (MCP/OpenAPI) 插件类型
- 解析插件字段定义并转换为工作流字段类型

#### QueryWikiNodeDefineCommandHandler
**文件**: `MoAI.App.Workflow.Core/Queries/QueryWikiNodeDefineCommandHandler.cs`
- 批量查询知识库信息
- 返回知识库节点定义列表

#### QueryAiChatNodeDefineCommandHandler
**文件**: `MoAI.App.Workflow.Core/Queries/QueryAiChatNodeDefineCommandHandler.cs`
- 批量查询 AI 模型信息
- 返回 AI 对话节点定义列表

### 6. 重构主 Handler (Core 层)

**文件**: `MoAI.App.Workflow.Core/Queries/QueryNodeDefineCommandHandler.cs`

- 移除直接的数据库访问和插件工厂依赖
- 改为通过 `IMediator` 委托给专用 Handler
- 按节点类型分组处理请求
- 对于静态节点 (Start, End, Condition 等)，直接返回定义
- 对于动态节点 (Plugin, Wiki, AiChat)，委托给专用 Handler

## 架构优势

### 1. 职责分离
- 每个 Handler 只负责一种节点类型的查询
- 主 Handler 作为协调者，负责路由和聚合

### 2. 批量查询支持
- 一次请求可以查询多个节点定义
- 减少网络往返次数
- 提高性能

### 3. 可扩展性
- 新增节点类型时，只需添加新的 Query 和 Handler
- 不影响现有代码

### 4. 代码复用
- 插件字段解析逻辑集中在 `QueryPluginNodeDefineCommandHandler`
- 避免代码重复

## 使用示例

```csharp
// 批量查询多种节点类型
var command = new QueryNodeDefineCommand
{
    Nodes = new[]
    {
        // 查询开始节点
        new KeyValue<NodeType, IReadOnlyCollection<string>>(
            NodeType.Start, 
            Array.Empty<string>()
        ),
        // 查询多个插件
        new KeyValue<NodeType, IReadOnlyCollection<string>>(
            NodeType.Plugin, 
            new[] { "1", "2", "3" }
        ),
        // 查询多个知识库
        new KeyValue<NodeType, IReadOnlyCollection<string>>(
            NodeType.Wiki, 
            new[] { "10", "20" }
        ),
        // 查询多个 AI 模型
        new KeyValue<NodeType, IReadOnlyCollection<string>>(
            NodeType.AiChat, 
            new[] { "5", "6" }
        )
    }
};

var response = await mediator.Send(command);
// response.Nodes 包含所有查询的节点定义
```

## 兼容性说明

这是一个破坏性变更，需要更新所有调用 `QueryNodeDefineCommand` 的代码。

### 迁移指南

**旧代码**:
```csharp
var command = new QueryNodeDefineCommand
{
    NodeType = NodeType.Plugin,
    PluginId = 1
};
var response = await mediator.Send(command);
// response 是单个节点定义
```

**新代码**:
```csharp
var command = new QueryNodeDefineCommand
{
    Nodes = new[]
    {
        new KeyValue<NodeType, IReadOnlyCollection<string>>(
            NodeType.Plugin,
            new[] { "1" }
        )
    }
};
var response = await mediator.Send(command);
// response.Nodes[0] 是节点定义
```

## 文件清单

### 新增文件 (Shared 层)
- `Queries/QueryPluginNodeDefineCommand.cs`
- `Queries/QueryWikiNodeDefineCommand.cs`
- `Queries/QueryAiChatNodeDefineCommand.cs`
- `Queries/Responses/QueryPluginNodeDefineCommandResponse.cs`
- `Queries/Responses/QueryWikiNodeDefineCommandResponse.cs`
- `Queries/Responses/QueryAiChatNodeDefineCommandResponse.cs`

### 新增文件 (Core 层)
- `Queries/QueryPluginNodeDefineCommandHandler.cs`
- `Queries/QueryWikiNodeDefineCommandHandler.cs`
- `Queries/QueryAiChatNodeDefineCommandHandler.cs`

### 修改文件
- `Queries/QueryNodeDefineCommand.cs` (Shared)
- `Queries/Responses/QueryNodeDefineCommandResponse.cs` (Shared)
- `Queries/QueryNodeDefineCommandHandler.cs` (Core)
