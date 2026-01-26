# 设计文档：AI 工作流编排引擎

## 概述

AI 工作流编排引擎是一个分布式、可扩展的工作流管理系统，设计用于在 MoAI 平台上编排复杂的 AI 工作流。该系统采用模块化架构，将工作流定义、执行引擎和数据持久化分离，支持多种节点类型和灵活的数据流。

核心设计原则：
- **关注点分离**：定义、设计、执行和存储分离
- **可扩展性**：易于添加新的节点类型和表达式类型
- **流式处理**：实时向客户端流式传输执行结果
- **类型安全**：强类型的节点定义和字段类型
- **错误恢复**：完整的错误捕获和执行历史

## 架构

### 高层架构

```
┌─────────────────────────────────────────────────────────────┐
│                     API 层 (Controllers)                      │
│  WorkflowController, WorkflowInstanceController              │
└────────────────────────┬────────────────────────────────────┘
                         │
┌────────────────────────▼────────────────────────────────────┐
│                    CQRS 层 (Commands/Queries)                │
│  CreateWorkflowDefinitionCommand, ExecuteWorkflowCommand    │
│  QueryWorkflowDefinitionCommand, QueryWorkflowInstanceCommand│
└────────────────────────┬────────────────────────────────────┘
                         │
┌────────────────────────▼────────────────────────────────────┐
│                   业务逻辑层 (Handlers/Services)              │
│  WorkflowDefinitionService, WorkflowExecutionService        │
│  VariableResolutionService, ExpressionEvaluationService     │
└────────────────────────┬────────────────────────────────────┘
                         │
┌────────────────────────▼────────────────────────────────────┐
│                  执行引擎层 (Runtime)                         │
│  WorkflowRuntime, INodeRuntime implementations              │
│  NodeRuntimeFactory, WorkflowContextManager                 │
└────────────────────────┬────────────────────────────────────┘
                         │
┌────────────────────────▼────────────────────────────────────┐
│                   数据访问层 (Repository)                     │
│  WorkflowDefinitionRepository, WorkflowInstanceRepository   │
│  DatabaseContext (Entity Framework Core)                    │
└─────────────────────────────────────────────────────────────┘
```

### 模块结构

```
MoAI.Workflow/
├── MoAI.Workflow.Shared/
│   ├── Commands/
│   │   ├── CreateWorkflowDefinitionCommand.cs
│   │   ├── UpdateWorkflowDefinitionCommand.cs
│   │   ├── DeleteWorkflowDefinitionCommand.cs
│   │   ├── ExecuteWorkflowCommand.cs
│   │   └── ...
│   ├── Queries/
│   │   ├── QueryWorkflowDefinitionCommand.cs
│   │   ├── QueryWorkflowInstanceCommand.cs
│   │   ├── QueryWorkflowExecutionCommand.cs
│   │   └── Responses/
│   ├── Models/
│   │   ├── NodeDefine.cs
│   │   ├── NodeDesign.cs
│   │   ├── FieldDesign.cs
│   │   ├── FieldExpression.cs
│   │   ├── WorkflowProcessingItem.cs
│   │   └── ...
│   ├── Enums/
│   │   ├── NodeType.cs
│   │   ├── FieldType.cs
│   │   ├── FieldExpressionType.cs
│   │   ├── NodeState.cs
│   │   └── ...
│   └── WorkflowSharedModule.cs
├── MoAI.Workflow.Core/
│   ├── Handlers/
│   │   ├── CreateWorkflowDefinitionCommandHandler.cs
│   │   ├── ExecuteWorkflowCommandHandler.cs
│   │   └── ...
│   ├── Queries/
│   │   ├── QueryWorkflowDefinitionCommandHandler.cs
│   │   └── ...
│   ├── Services/
│   │   ├── WorkflowDefinitionService.cs
│   │   ├── WorkflowExecutionService.cs
│   │   ├── VariableResolutionService.cs
│   │   ├── ExpressionEvaluationService.cs
│   │   ├── NodeRuntimeFactory.cs
│   │   └── ...
│   ├── Runtime/
│   │   ├── WorkflowRuntime.cs
│   │   ├── WorkflowContextManager.cs
│   │   ├── NodeRuntimes/
│   │   │   ├── INodeRuntime.cs
│   │   │   ├── StartNodeRuntime.cs
│   │   │   ├── EndNodeRuntime.cs
│   │   │   ├── AiChatNodeRuntime.cs
│   │   │   ├── WikiNodeRuntime.cs
│   │   │   ├── PluginNodeRuntime.cs
│   │   │   ├── ConditionNodeRuntime.cs
│   │   │   ├── ForEachNodeRuntime.cs
│   │   │   ├── ForkNodeRuntime.cs
│   │   │   ├── JavaScriptNodeRuntime.cs
│   │   │   └── DataProcessNodeRuntime.cs
│   │   └── ...
│   └── WorkflowCoreModule.cs
└── MoAI.Workflow.Api/
    ├── Controllers/
    │   ├── WorkflowDefinitionController.cs
    │   ├── WorkflowInstanceController.cs
    │   └── ...
    └── WorkflowApiModule.cs
```

## 组件和接口

### 1. 节点定义接口 (INodeDefine)

```csharp
public interface INodeDefine
{
    string NodeKey { get; }
    NodeType NodeType { get; }
    IReadOnlyList<FieldDefine> InputFields { get; }
    IReadOnlyList<FieldDefine> OutputFields { get; }
}

public class FieldDefine
{
    public string FieldName { get; set; }
    public FieldType FieldType { get; set; }
    public bool IsRequired { get; set; }
    public string Description { get; set; }
}
```

### 2. 节点设计 (NodeDesign)

```csharp
public class NodeDesign
{
    public string NodeKey { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public NodeType NodeType { get; set; }
    public string NextNodeKey { get; set; } // 简单情况下的下一个节点
    public Dictionary<string, FieldDesign> FieldDesigns { get; set; }
}

public class FieldDesign
{
    public string FieldName { get; set; }
    public FieldExpressionType ExpressionType { get; set; }
    public string Value { get; set; } // Fixed value or expression
}

public class Connection
{
    public string SourceNodeKey { get; set; }
    public string TargetNodeKey { get; set; }
    public string Label { get; set; } // 可选，用于条件分支标签（如 "true", "false"）
}

public class WorkflowDefinition
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public IReadOnlyCollection<NodeDesign> Nodes { get; set; }
    public IReadOnlyCollection<Connection> Connections { get; set; }
}
```
```

### 3. 字段表达式类型 (FieldExpressionType)

```csharp
public enum FieldExpressionType
{
    Fixed,                  // 常数值
    Variable,              // 变量引用 (sys.*, input.*, nodeKey.*)
    JsonPath,              // JSON 路径表达式
    StringInterpolation    // 字符串插值 {variable}
}
```

### 4. 工作流上下文 (IWorkflowContext)

```csharp
public interface IWorkflowContext
{
    string InstanceId { get; }
    string DefinitionId { get; }
    Dictionary<string, object> RuntimeParameters { get; }
    HashSet<string> ExecutedNodeKeys { get; }
    Dictionary<string, INodePipeline> NodePipelines { get; }
    Dictionary<string, object> FlattenedVariables { get; }
}
```

### 5. 节点管道 (INodePipeline)

```csharp
public interface INodePipeline
{
    INodeDefine NodeDefine { get; }
    NodeState State { get; }
    JsonElement InputJsonElement { get; }
    Dictionary<string, object> InputJsonMap { get; }
    JsonElement OutputJsonElement { get; }
    Dictionary<string, object> OutputJsonMap { get; }
    string ErrorMessage { get; }
}
```

### 6. 节点运行时接口 (INodeRuntime)

```csharp
public interface INodeRuntime
{
    NodeType SupportedNodeType { get; }
    Task<NodeExecutionResult> ExecuteAsync(
        INodeDefine nodeDefine,
        Dictionary<string, object> inputs,
        IWorkflowContext context,
        CancellationToken cancellationToken);
}

public class NodeExecutionResult
{
    public NodeState State { get; set; }
    public Dictionary<string, object> Output { get; set; }
    public string ErrorMessage { get; set; }
}
```

### 7. 工作流处理项 (WorkflowProcessingItem)

```csharp
public class WorkflowProcessingItem
{
    public string NodeType { get; set; }
    public string NodeKey { get; set; }
    public Dictionary<string, object> Input { get; set; }
    public Dictionary<string, object> Output { get; set; }
    public NodeState State { get; set; }
    public string ErrorMessage { get; set; }
    public DateTimeOffset ExecutedTime { get; set; }
}
```

### 8. 工作流运行时 (WorkflowRuntime)

```csharp
public class WorkflowRuntime
{
    private readonly INodeRuntimeFactory _nodeRuntimeFactory;
    private readonly IVariableResolutionService _variableResolutionService;
    private readonly IExpressionEvaluationService _expressionEvaluationService;

    public async IAsyncEnumerable<WorkflowProcessingItem> ExecuteAsync(
        WorkflowDefinition definition,
        Dictionary<string, object> startupParameters,
        IWorkflowContext context,
        CancellationToken cancellationToken)
    {
        // 1. 验证工作流定义
        ValidateWorkflowDefinition(definition);

        // 2. 初始化上下文
        InitializeContext(context, startupParameters);

        // 3. 从 Start 节点开始执行
        string currentNodeKey = FindStartNodeKey(definition);

        while (currentNodeKey != null)
        {
            // 获取节点设计
            var nodeDesign = definition.NodeDesigns[currentNodeKey];
            var nodeDefine = GetNodeDefine(nodeDesign);

            // 解析输入
            var inputs = await ResolveNodeInputsAsync(nodeDesign, context);

            // 执行节点
            var nodeRuntime = _nodeRuntimeFactory.GetRuntime(nodeDefine.NodeType);
            var result = await nodeRuntime.ExecuteAsync(nodeDefine, inputs, context, cancellationToken);

            // 更新上下文
            UpdateContext(context, currentNodeKey, result);

            // 流式传输结果
            yield return new WorkflowProcessingItem
            {
                NodeType = nodeDefine.NodeType.ToString(),
                NodeKey = currentNodeKey,
                Input = inputs,
                Output = result.Output,
                State = result.State,
                ErrorMessage = result.ErrorMessage,
                ExecutedTime = DateTimeOffset.UtcNow
            };

            // 处理错误
            if (result.State == NodeState.Failed)
            {
                yield break;
            }

            // 确定下一个节点
            currentNodeKey = DetermineNextNode(nodeDesign, result, context);
        }
    }
}
```

## 数据模型

### 现有数据库实体

系统使用以下现有的 EFCore 实体（无需创建 Repository）：

- **WorkflowDesignEntity**：存储工作流设计定义
- **WorkflowDesignSnapshotEntity**：存储工作流设计快照（版本历史）
- **WorkflowHistoryEntity**：存储工作流执行历史

这些实体已在 DatabaseContext 中配置，包含自动审计字段（CreateUserId、CreateTime、UpdateUserId、UpdateTime、IsDeleted）。

### 数据模型映射

```csharp
// WorkflowDesignEntity 用于存储工作流定义
// - 包含完整的工作流设计 JSON（所有节点、连接、配置）
// - 支持版本管理

// WorkflowDesignSnapshotEntity 用于存储版本历史
// - 记录每次更新的快照
// - 支持回滚到之前的版本

// WorkflowHistoryEntity 用于存储执行历史
// - 记录每次工作流执行的完整历史
// - 包括所有节点的输入、输出、状态和错误信息
```

## 关键服务

### 1. 变量解析服务 (VariableResolutionService)

负责解析变量引用并从工作流上下文中检索值。

```csharp
public class VariableResolutionService
{
    public object ResolveVariable(string variableReference, IWorkflowContext context)
    {
        // 支持的格式：
        // - sys.userId
        // - input.field
        // - nodeKey.field
        // - nodeKey.array[0]
        // - nodeKey.array[*]
    }

    public Dictionary<string, object> FlattenJson(string prefix, JsonElement element)
    {
        // 将嵌套 JSON 扁平化为 prefix.field 格式
    }
}
```

### 2. 表达式评估服务 (ExpressionEvaluationService)

负责评估不同类型的表达式。

```csharp
public class ExpressionEvaluationService
{
    public object EvaluateExpression(
        string expression,
        FieldExpressionType expressionType,
        IWorkflowContext context)
    {
        return expressionType switch
        {
            FieldExpressionType.Fixed => expression,
            FieldExpressionType.Variable => _variableResolutionService.ResolveVariable(expression, context),
            FieldExpressionType.JsonPath => EvaluateJsonPath(expression, context),
            FieldExpressionType.StringInterpolation => EvaluateStringInterpolation(expression, context),
            _ => throw new InvalidOperationException()
        };
    }
}
```

### 3. 节点运行时工厂 (NodeRuntimeFactory)

负责创建和管理节点运行时实例。

```csharp
public class NodeRuntimeFactory
{
    private readonly Dictionary<NodeType, INodeRuntime> _runtimes;

    public INodeRuntime GetRuntime(NodeType nodeType)
    {
        if (!_runtimes.TryGetValue(nodeType, out var runtime))
        {
            throw new InvalidOperationException($"No runtime for node type: {nodeType}");
        }
        return runtime;
    }

    public void RegisterRuntime(NodeType nodeType, INodeRuntime runtime)
    {
        _runtimes[nodeType] = runtime;
    }
}
```

### 4. 工作流定义服务 (WorkflowDefinitionService)

负责工作流定义的创建、更新和验证。

```csharp
public class WorkflowDefinitionService
{
    public void ValidateWorkflowDefinition(WorkflowDefinition definition)
    {
        // 1. 验证恰好有一个 Start 节点
        // 2. 验证至少有一个 End 节点
        // 3. 验证所有连接形成有效的有向图
        // 4. 验证所有连接的源节点和目标节点都存在于节点列表中
        // 5. 验证所有节点类型有效（来自固定的 NodeType 枚举）
        // 6. 验证所有字段类型兼容
        // 7. 验证没有孤立节点（除非是 End 节点）
        // 8. 验证没有循环依赖（除非是 ForEach 或 Fork 节点内部）
    }

    public void ValidateNodeDesign(NodeDesign nodeDesign, INodeDefine nodeDefine)
    {
        // 1. 验证所有必需的输入字段都已配置
        // 2. 验证字段类型兼容
        // 3. 验证表达式有效
        // 4. 验证变量引用格式正确（但不验证运行时是否存在）
    }
    
    public void ValidateConnections(
        IReadOnlyCollection<NodeDesign> nodes, 
        IReadOnlyCollection<Connection> connections)
    {
        // 1. 验证每个连接的源节点和目标节点都存在
        // 2. 验证连接不会形成无效的循环
        // 3. 验证 Start 节点有出边
        // 4. 验证 End 节点没有出边
        // 5. 验证所有非 End 节点都有至少一个出边
    }
}
```

## 正确性属性

一个属性是一个特征或行为，应该在系统的所有有效执行中保持真实——本质上是关于系统应该做什么的正式陈述。属性充当人类可读规范和机器可验证正确性保证之间的桥梁。

### 属性 1：工作流定义往返一致性

*对于任何*工作流定义，创建它、存储它、检索它，然后验证检索到的定义应该与原始定义等价。

**验证：需求 1.4**

### 属性 2：节点类型验证

*对于任何*节点类型集合，只有来自固定集合（Start、End、Plugin、Wiki、AiChat、Condition、ForEach、Fork、JavaScript、DataProcess）的节点类型应该被接受，其他所有类型都应该被拒绝。

**验证：需求 1.2**

### 属性 3：工作流图有效性

*对于任何*工作流定义，验证应该确保恰好有一个 Start 节点、至少有一个 End 节点，并且所有连接形成有效的有向图。

**验证：需求 1.3**

### 属性 4：变量引用解析

*对于任何*变量引用和工作流上下文，解析应该从上下文中返回正确的值，或者如果变量不存在则返回错误。

**验证：需求 2.2, 4.2**

### 属性 5：JsonPath 表达式评估

*对于任何* JsonPath 表达式和 JSON 对象，评估应该返回与表达式匹配的正确值。

**验证：需求 2.3**

### 属性 6：字符串插值替换

*对于任何*包含变量占位符的模板字符串和工作流上下文，插值应该用上下文中的实际值替换所有占位符。

**验证：需求 2.4**

### 属性 7：输出扁平化一致性

*对于任何*嵌套 JSON 对象，扁平化然后通过点符号访问应该返回与原始嵌套访问相同的值。

**验证：需求 4.1**

### 属性 8：节点执行顺序

*对于任何*工作流定义和执行，节点应该按照定义的连接从 Start 节点开始按顺序执行。

**验证：需求 3.2**

### 属性 9：条件路由正确性

*对于任何* Condition 节点和条件表达式，执行应该被路由到与条件评估结果对应的正确分支。

**验证：需求 3.5**

### 属性 10：ForEach 循环完整性

*对于任何* ForEach 节点和集合，循环体应该为集合中的每个项目执行一次。

**验证：需求 3.6**

### 属性 11：Fork 并行执行

*对于任何* Fork 节点，所有分支应该并行执行，系统应该等待所有分支完成后再继续。

**验证：需求 3.7**

### 属性 12：执行历史完整性

*对于任何*工作流执行，完整的执行历史应该被存储，包括所有节点管道、输入、输出和状态。

**验证：需求 3.8, 8.4**

### 属性 13：错误捕获和报告

*对于任何*失败的节点执行，错误应该被捕获、记录并流式传输给客户端，包括错误消息和堆栈跟踪。

**验证：需求 3.4, 9.4**

### 属性 14：输入验证

*对于任何*节点配置，所有必需的输入字段都应该被提供且类型应该兼容，否则应该返回验证错误。

**验证：需求 2.5**

### 属性 15：API 端点响应一致性

*对于任何* API 端点调用，响应应该包含预期的数据结构和状态码。

**验证：需求 7.1-7.8**

## 错误处理

### 错误类型

1. **验证错误**：工作流定义或节点配置无效
2. **执行错误**：节点执行期间发生异常
3. **变量解析错误**：变量引用无效或缺失
4. **表达式评估错误**：表达式评估失败
5. **资源不存在错误**：工作流定义或实例不存在

### 错误处理策略

- 所有错误都应该被捕获并记录
- 错误应该包含清晰的消息和上下文信息
- 执行错误应该被流式传输给客户端
- 验证错误应该在执行前返回

## 测试策略

### 单元测试

- 变量解析服务的各种变量引用格式
- 表达式评估服务的各种表达式类型
- 工作流定义验证的各种图形配置
- 每个节点运行时的特定行为
- 错误处理和边界情况

### 属性测试

- 工作流定义往返一致性（属性 1）
- 节点类型验证（属性 2）
- 工作流图有效性（属性 3）
- 变量引用解析（属性 4）
- JsonPath 表达式评估（属性 5）
- 字符串插值替换（属性 6）
- 输出扁平化一致性（属性 7）
- 节点执行顺序（属性 8）
- 条件路由正确性（属性 9）
- ForEach 循环完整性（属性 10）
- Fork 并行执行（属性 11）
- 执行历史完整性（属性 12）
- 错误捕获和报告（属性 13）
- 输入验证（属性 14）
- API 端点响应一致性（属性 15）

### 测试配置

- 每个属性测试最少运行 100 次迭代
- 使用 xUnit 和 FsCheck 进行属性测试
- 每个测试应该标记其验证的属性
- 标记格式：`// Feature: ai-workflow, Property N: [Property Title]`

## 扩展性

### 添加新的节点类型

1. 创建新的 `INodeDefine` 实现
2. 创建新的 `INodeRuntime` 实现
3. 在 `NodeRuntimeFactory` 中注册运行时
4. 添加相应的单元和属性测试

### 添加新的表达式类型

1. 在 `FieldExpressionType` 枚举中添加新类型
2. 在 `ExpressionEvaluationService` 中实现评估逻辑
3. 添加相应的单元和属性测试

### 添加新的字段类型

1. 在 `FieldType` 枚举中添加新类型
2. 在验证逻辑中处理新类型
3. 添加相应的单元测试
