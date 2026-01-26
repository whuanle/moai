# CreateWorkflowDefinitionCommand 调整实现总结

## 完成日期
2025-01-25

## 实现内容

### 1. 创建新模型

#### Connection.cs
- 位置：`src/workflow/MoAI.Workflow.Shared/Models/Connection.cs`
- 功能：表示工作流节点之间的连接关系
- 属性：
  - `SourceNodeKey`: 源节点标识符
  - `TargetNodeKey`: 目标节点标识符
  - `Label`: 可选的连接标签（用于条件分支）

#### WorkflowDefinition.cs
- 位置：`src/workflow/MoAI.Workflow.Shared/Models/WorkflowDefinition.cs`
- 功能：完整的工作流定义模型
- 属性：
  - `Id`: 工作流定义 ID
  - `Name`: 工作流名称
  - `Description`: 工作流描述
  - `Nodes`: 节点列表（`IReadOnlyCollection<NodeDesign>`）
  - `Connections`: 连接列表（`IReadOnlyCollection<Connection>`）

### 2. 更新 Command 模型

#### CreateWorkflowDefinitionCommand.cs
**变更前：**
```csharp
public string FunctionDesign { get; set; } = string.Empty;
```

**变更后：**
```csharp
[Required(ErrorMessage = "节点列表不能为空")]
[MinLength(1, ErrorMessage = "至少需要一个节点")]
public IReadOnlyCollection<NodeDesign> Nodes { get; set; } = Array.Empty<NodeDesign>();

[Required(ErrorMessage = "连接列表不能为空")]
public IReadOnlyCollection<Connection> Connections { get; set; } = Array.Empty<Connection>();
```

**改进点：**
- ✅ 使用强类型集合替代 JSON 字符串
- ✅ 使用 `IReadOnlyCollection` 确保不可变性
- ✅ 添加数据注解进行 API 层验证
- ✅ 添加字符串长度限制

#### UpdateWorkflowDefinitionCommand.cs
- 同样的变更模式
- 添加了 `[Required]` 验证特性

### 3. 更新 WorkflowDefinitionService

#### 方法签名变更
**变更前：**
```csharp
public void ValidateWorkflowDefinition(string functionDesignJson)
```

**变更后：**
```csharp
public void ValidateWorkflowDefinition(WorkflowDefinition definition)
```

#### 新增方法
```csharp
private void ValidateConnections(
    List<NodeDesign> nodes, 
    List<Connection> connections)
{
    // 1. 验证连接的源节点和目标节点存在
    // 2. 验证 Start 节点有出边
    // 3. 验证 End 节点没有出边
    // 4. 验证所有非 End 节点都有至少一个出边
}
```

#### 更新的验证逻辑
- 使用 `Connection` 对象构建邻接表
- 基于连接列表进行图遍历
- 更准确的连通性和循环检测

### 4. 更新 Handler

#### CreateWorkflowDefinitionCommandHandler.cs
**主要变更：**
1. 构建 `WorkflowDefinition` 对象
2. 调用验证服务
3. 序列化为 JSON 存储到数据库

```csharp
// 1. 构建工作流定义对象
var definition = new WorkflowDefinition
{
    Id = Guid.NewGuid().ToString(),
    Name = request.Name,
    Description = request.Description,
    Nodes = request.Nodes,
    Connections = request.Connections
};

// 2. 验证工作流定义
_workflowDefinitionService.ValidateWorkflowDefinition(definition);

// 3. 序列化并存储
var functionDesignJson = JsonSerializer.Serialize(definition, new JsonSerializerOptions
{
    WriteIndented = false,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
});
```

#### UpdateWorkflowDefinitionCommandHandler.cs
- 同样的模式
- 在更新前创建版本快照

### 5. 更新 WorkflowRuntime

#### ExecuteAsync 方法签名变更
**变更前：**
```csharp
public async IAsyncEnumerable<WorkflowProcessingItem> ExecuteAsync(
    string functionDesignJson,
    ...)
```

**变更后：**
```csharp
public async IAsyncEnumerable<WorkflowProcessingItem> ExecuteAsync(
    WorkflowDefinition definition,
    ...)
```

**改进：**
- 直接接收 `WorkflowDefinition` 对象
- 不需要内部解析 JSON
- 类型安全

### 6. 更新 WorkflowExecutionService

在调用 `WorkflowRuntime.ExecuteAsync` 之前反序列化工作流定义：

```csharp
// 反序列化工作流定义
var definition = JsonSerializer.Deserialize<WorkflowDefinition>(
    workflowDesign.FunctionDesgin,
    new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    });

if (definition == null)
{
    throw new BusinessException(400, "工作流定义反序列化失败");
}

await foreach (var item in _workflowRuntime.ExecuteAsync(
    definition,
    startupParameters,
    instanceId.ToString(),
    workflowDesignId.ToString(),
    systemVariables,
    cancellationToken))
{
    // ...
}
```

## 验证层次

### API 层验证（自动）
ASP.NET Core 模型绑定会自动验证：
- `[Required]` - 字段不能为空
- `[StringLength]` - 字符串长度限制
- `[MinLength]` - 集合最小长度
- JSON 格式正确性
- 属性类型匹配

### Handler 层验证（业务逻辑）
WorkflowDefinitionService 验证：
- 节点类型有效性
- Start/End 节点数量
- 节点键唯一性
- 连接的源节点和目标节点存在
- 图结构有效性（连通性、循环检测）
- 节点配置完整性

## 数据流

```
客户端 JSON 请求
    ↓
API 层（模型绑定）
    ├─ 反序列化为 CreateWorkflowDefinitionCommand
    ├─ 验证 [Required], [StringLength] 等
    └─ 如果失败 → 400 Bad Request
    ↓
Handler 层
    ├─ 构建 WorkflowDefinition 对象
    ├─ 调用 WorkflowDefinitionService.ValidateWorkflowDefinition()
    │   ├─ 验证节点类型
    │   ├─ 验证 Start/End 节点
    │   ├─ 验证连接
    │   └─ 验证图结构
    └─ 如果失败 → 抛出 BusinessException → 400 Bad Request
    ↓
序列化为 JSON
    ↓
存储到 WorkflowDesignEntity.FunctionDesgin
```

## 执行流程

```
从数据库读取 WorkflowDesignEntity
    ↓
反序列化 FunctionDesgin 为 WorkflowDefinition
    ↓
传递给 WorkflowRuntime.ExecuteAsync()
    ↓
验证工作流定义
    ↓
执行节点
```

## 优势

### 1. 类型安全
- 编译时类型检查
- IDE 智能提示
- 减少运行时错误

### 2. 不可变性
- 使用 `IReadOnlyCollection` 防止意外修改
- 符合 CQRS 命令的不可变性原则

### 3. 分层验证
- API 层：格式验证
- Handler 层：业务逻辑验证
- 清晰的关注点分离

### 4. 更好的错误信息
- 详细的验证错误
- 指明具体的问题节点和连接
- 帮助用户快速定位问题

### 5. 可维护性
- 代码更清晰
- 易于扩展
- 易于测试

## 编译状态

✅ **所有项目编译成功，无错误**

- MoAI.Workflow.Shared: ✅ 成功（8个警告，主要是代码分析建议）
- MoAI.Workflow.Core: ✅ 成功（159个警告，主要是代码分析建议）
- MoAI.Workflow.Api: ✅ 成功（1个警告）

## 下一步建议

1. **更新 API 文档**
   - 更新 OpenAPI/Swagger 文档
   - 添加请求示例

2. **更新前端代码**
   - 修改 API 客户端调用
   - 使用新的请求格式

3. **添加单元测试**
   - 测试 Connection 模型
   - 测试 WorkflowDefinition 模型
   - 测试验证逻辑

4. **添加集成测试**
   - 测试完整的创建流程
   - 测试验证错误场景

5. **性能优化**
   - 考虑缓存 JsonSerializerOptions
   - 优化大型工作流的验证性能

## 相关文件

### 新增文件
- `src/workflow/MoAI.Workflow.Shared/Models/Connection.cs`
- `src/workflow/MoAI.Workflow.Shared/Models/WorkflowDefinition.cs`

### 修改文件
- `src/workflow/MoAI.Workflow.Shared/Commands/CreateWorkflowDefinitionCommand.cs`
- `src/workflow/MoAI.Workflow.Shared/Commands/UpdateWorkflowDefinitionCommand.cs`
- `src/workflow/MoAI.Workflow.Core/Services/WorkflowDefinitionService.cs`
- `src/workflow/MoAI.Workflow.Core/Handlers/CreateWorkflowDefinitionCommandHandler.cs`
- `src/workflow/MoAI.Workflow.Core/Handlers/UpdateWorkflowDefinitionCommandHandler.cs`
- `src/workflow/MoAI.Workflow.Core/Runtime/WorkflowRuntime.cs`
- `src/workflow/MoAI.Workflow.Core/Services/WorkflowExecutionService.cs`

## 总结

成功将 `CreateWorkflowDefinitionCommand` 从接收 JSON 字符串改为接收强类型的节点列表和连接列表。这个改进提高了类型安全性、可维护性和用户体验，同时保持了向后兼容性（数据库存储格式未变）。所有代码编译成功，准备进行测试和部署。
