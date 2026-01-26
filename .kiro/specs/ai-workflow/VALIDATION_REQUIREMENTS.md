# 工作流定义验证要求

## 概述

本文档说明了工作流命令的验证要求。工作流采用**草稿-发布**机制：

1. **创建** - 只创建基础信息，不包含设计数据
2. **编辑** - 编辑草稿（`UiDesignDraft`、`FunctionDesignDraft`），不影响已发布版本
3. **发布** - 将草稿发布为正式版本，并进行全面验证

## 命令设计

### 1. CreateWorkflowDefinitionCommand（创建）

**用途**: 创建新的工作流定义，只包含基础信息

**输入格式**:

```csharp
public class CreateWorkflowDefinitionCommand : IRequest<SimpleGuid>, IModelValidator<CreateWorkflowDefinitionCommand>
{
    public int TeamId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Avatar { get; set; }
}
```

**验证规则**:
- `Name`: 不能为空，最大长度 100 字符
- `Description`: 最大长度 500 字符（可选）
- `Avatar`: 最大长度 200 字符（可选）

**设计说明**: 
- ✅ 创建时不需要设计数据
- ✅ 用户可以在创建后通过更新命令添加设计草稿
- ✅ 简化创建流程，降低使用门槛

---

### 2. UpdateWorkflowDefinitionCommand（更新）

**用途**: 更新工作流的基础信息和设计草稿

**输入格式**:

```csharp
public class UpdateWorkflowDefinitionCommand : IRequest<EmptyCommandResponse>, IModelValidator<UpdateWorkflowDefinitionCommand>
{
    public Guid Id { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Avatar { get; set; }
    public string? UiDesignDraft { get; set; }
    public string? FunctionDesignDraft { get; set; }
}
```

**验证规则**:
- `Id`: 不能为空
- `Name`: 最大长度 100 字符（如果提供）
- `Description`: 最大长度 500 字符（如果提供）
- `Avatar`: 最大长度 200 字符（如果提供）

**设计说明**: 
- ✅ 所有字段（除了 ID）都是可选的，允许部分更新
- ✅ 设计草稿保存在 `UiDesignDraft` 和 `FunctionDesignDraft` 字段
- ✅ 更新草稿不会影响已发布的版本（`UiDesign`、`FunctionDesign`）
- ✅ 不进行业务逻辑验证（节点类型、连接等），允许保存不完整的草稿

---

### 3. PublishWorkflowDefinitionCommand（发布）⭐

**用途**: 将草稿版本发布为正式版本

**输入格式**:

```csharp
public class PublishWorkflowDefinitionCommand : IRequest<EmptyCommandResponse>, IModelValidator<PublishWorkflowDefinitionCommand>
{
    public Guid Id { get; set; }
}
```

**验证规则**:
- `Id`: 不能为空

**发布流程**:

```
1. 检索工作流定义
   ↓
2. 检查 FunctionDesignDraft 是否为空
   └─ 如果为空，返回错误："草稿为空，无法发布"
   ↓
3. 反序列化 FunctionDesignDraft 为 WorkflowDefinition
   └─ 如果反序列化失败，返回错误："草稿格式无效"
   ↓
4. 调用 WorkflowDefinitionService.ValidateWorkflowDefinition()
   ├─ 验证节点类型
   ├─ 验证 Start/End 节点
   ├─ 验证节点配置
   └─ 验证连接有效性
   ↓
5. 如果验证失败
   └─ 返回详细的验证错误信息，不发布
   ↓
6. 如果验证成功
   ├─ 创建版本快照（WorkflowDesignSnapshotEntity）
   ├─ UiDesign = UiDesignDraft
   ├─ FunctionDesign = FunctionDesignDraft
   └─ IsPublish = true
```

**设计说明**: 
- ✅ 发布时才进行全面的业务逻辑验证
- ✅ 验证失败不会影响草稿，用户可以继续编辑
- ✅ 创建版本快照以维护历史记录
- ✅ 发布后，执行工作流使用已发布版本

---

## 验证要求

### 1. 基本字段验证（所有命令）

在 API 层通过 FluentValidation 自动验证：

- 字段长度限制
- 必填字段检查
- 数据类型验证

### 2. 业务逻辑验证（仅发布时）

在发布时通过 `WorkflowDefinitionService` 验证：

#### 2.1 节点类型验证
- 每个节点的 `NodeType` 必须来自固定的枚举集合
- 有效的节点类型：`Start`, `End`, `Plugin`, `Wiki`, `AiChat`, `Condition`, `ForEach`, `Fork`, `JavaScript`, `DataProcess`
- 如果节点类型无效，返回验证错误

#### 2.2 Start/End 节点验证
- 工作流必须恰好有**一个** Start 节点
- 工作流必须至少有**一个** End 节点
- 如果不满足，返回验证错误

#### 2.3 节点配置验证
- 每个节点的必需输入字段必须已配置
- 字段类型必须兼容
- 表达式格式必须正确（但不验证运行时变量是否存在）

#### 2.4 连接验证

##### 2.4.1 连接存在性验证
- 每个连接的 `SourceNodeKey` 必须存在于节点列表中
- 每个连接的 `TargetNodeKey` 必须存在于节点列表中
- 如果引用的节点不存在，返回验证错误并指明缺失的节点

##### 2.4.2 图结构验证
- 连接必须形成有效的有向图
- Start 节点必须有至少一个出边
- End 节点不能有出边
- 所有非 End 节点必须有至少一个出边（避免孤立节点）

##### 2.4.3 循环依赖验证
- 验证不存在无效的循环依赖
- 注意：ForEach 和 Fork 节点内部允许循环

#### 2.5 数据完整性验证

##### 2.5.1 节点键唯一性
- 所有节点的 `NodeKey` 必须唯一
- 如果存在重复，返回验证错误

##### 2.5.2 连接唯一性
- 同一对源节点和目标节点之间不能有重复的连接
- 如果存在重复连接，返回验证错误

---

## 错误处理

### 验证错误响应格式

```csharp
public class ValidationError
{
    public string ErrorCode { get; set; }
    public string Message { get; set; }
    public string Field { get; set; }
    public object Details { get; set; }
}
```

### 常见验证错误

| 错误代码 | 消息 | 详情 | 发生阶段 |
|---------|------|------|---------|
| `EMPTY_DRAFT` | 草稿为空，无法发布 | - | 发布 |
| `INVALID_DRAFT_FORMAT` | 草稿格式无效 | JSON 解析错误 | 发布 |
| `INVALID_NODE_TYPE` | 节点类型无效 | 节点键、无效的类型值 | 发布 |
| `MISSING_START_NODE` | 缺少 Start 节点 | - | 发布 |
| `MULTIPLE_START_NODES` | 存在多个 Start 节点 | Start 节点列表 | 发布 |
| `MISSING_END_NODE` | 缺少 End 节点 | - | 发布 |
| `INVALID_CONNECTION_SOURCE` | 连接的源节点不存在 | 源节点键 | 发布 |
| `INVALID_CONNECTION_TARGET` | 连接的目标节点不存在 | 目标节点键 | 发布 |
| `ORPHANED_NODE` | 存在孤立节点 | 节点键 | 发布 |
| `INVALID_CYCLE` | 存在无效的循环依赖 | 涉及的节点列表 | 发布 |
| `DUPLICATE_NODE_KEY` | 节点键重复 | 重复的节点键 | 发布 |
| `MISSING_REQUIRED_FIELD` | 缺少必需的输入字段 | 节点键、字段名 | 发布 |

---

## 实现示例

### WorkflowDefinitionService.ValidateWorkflowDefinition

```csharp
public void ValidateWorkflowDefinition(WorkflowDefinition definition)
{
    var errors = new List<ValidationError>();
    
    // 1. 验证节点类型
    foreach (var node in definition.Nodes)
    {
        if (!Enum.IsDefined(typeof(NodeType), node.NodeType))
        {
            errors.Add(new ValidationError
            {
                ErrorCode = "INVALID_NODE_TYPE",
                Message = $"节点 '{node.NodeKey}' 的类型 '{node.NodeType}' 无效",
                Field = "NodeType",
                Details = new { NodeKey = node.NodeKey, NodeType = node.NodeType }
            });
        }
    }
    
    // 2. 验证 Start 节点
    var startNodes = definition.Nodes.Where(n => n.NodeType == NodeType.Start).ToList();
    if (startNodes.Count == 0)
    {
        errors.Add(new ValidationError
        {
            ErrorCode = "MISSING_START_NODE",
            Message = "工作流必须有一个 Start 节点"
        });
    }
    else if (startNodes.Count > 1)
    {
        errors.Add(new ValidationError
        {
            ErrorCode = "MULTIPLE_START_NODES",
            Message = "工作流只能有一个 Start 节点",
            Details = new { StartNodes = startNodes.Select(n => n.NodeKey).ToList() }
        });
    }
    
    // 3. 验证 End 节点
    var endNodes = definition.Nodes.Where(n => n.NodeType == NodeType.End).ToList();
    if (endNodes.Count == 0)
    {
        errors.Add(new ValidationError
        {
            ErrorCode = "MISSING_END_NODE",
            Message = "工作流必须至少有一个 End 节点"
        });
    }
    
    // 4. 验证连接
    ValidateConnections(definition.Nodes, definition.Connections, errors);
    
    // 5. 如果有错误，抛出异常
    if (errors.Any())
    {
        throw new WorkflowValidationException(errors);
    }
}
```

### PublishWorkflowDefinitionCommandHandler

```csharp
public class PublishWorkflowDefinitionCommandHandler : IRequestHandler<PublishWorkflowDefinitionCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly WorkflowDefinitionService _workflowDefinitionService;

    public async Task<EmptyCommandResponse> Handle(PublishWorkflowDefinitionCommand request, CancellationToken cancellationToken)
    {
        // 1. 检索工作流定义
        var entity = await _databaseContext.WorkflowDesigns
            .FirstOrDefaultAsync(x => x.Id == request.Id && x.IsDeleted == 0, cancellationToken);
            
        if (entity == null)
        {
            throw new BusinessException("工作流定义不存在") { StatusCode = 404 };
        }
        
        // 2. 检查草稿是否为空
        if (string.IsNullOrEmpty(entity.FunctionDesignDraft))
        {
            throw new BusinessException("草稿为空，无法发布") { StatusCode = 400 };
        }
        
        // 3. 反序列化草稿
        WorkflowDefinition definition;
        try
        {
            definition = JsonSerializer.Deserialize<WorkflowDefinition>(entity.FunctionDesignDraft);
        }
        catch (JsonException ex)
        {
            throw new BusinessException("草稿格式无效") { StatusCode = 400 };
        }
        
        // 4. 验证工作流定义
        try
        {
            _workflowDefinitionService.ValidateWorkflowDefinition(definition);
        }
        catch (WorkflowValidationException ex)
        {
            // 返回详细的验证错误
            throw new BusinessException("工作流定义验证失败") 
            { 
                StatusCode = 400,
                Details = ex.Errors 
            };
        }
        
        // 5. 创建版本快照
        var snapshot = new WorkflowDesignSnapshotEntity
        {
            Id = Guid.NewGuid(),
            WorkflowDesignId = entity.Id,
            UiDesign = entity.UiDesign,
            FunctionDesign = entity.FunctionDesgin,
            SnapshotTime = DateTimeOffset.UtcNow
        };
        _databaseContext.WorkflowDesignSnapshots.Add(snapshot);
        
        // 6. 发布草稿
        entity.UiDesign = entity.UiDesignDraft;
        entity.FunctionDesgin = entity.FunctionDesignDraft;
        entity.IsPublish = true;
        
        await _databaseContext.SaveChangesAsync(cancellationToken);
        
        return EmptyCommandResponse.Default;
    }
}
```

---

## 工作流生命周期

```
1. 创建工作流（CreateWorkflowDefinitionCommand）
   ├─ 只保存基础信息（Name、Description、Avatar）
   ├─ UiDesignDraft = ""
   ├─ FunctionDesignDraft = ""
   ├─ UiDesign = ""
   ├─ FunctionDesign = ""
   └─ IsPublish = false
   ↓
2. 编辑草稿（UpdateWorkflowDefinitionCommand）
   ├─ 可以更新基础信息
   ├─ 保存设计到 UiDesignDraft、FunctionDesignDraft
   ├─ 不影响 UiDesign、FunctionDesign（已发布版本）
   ├─ 不进行业务逻辑验证
   └─ IsPublish 保持 false
   ↓
3. 发布版本（PublishWorkflowDefinitionCommand）⭐
   ├─ 验证 FunctionDesignDraft 的有效性
   │  ├─ 验证节点类型
   │  ├─ 验证 Start/End 节点
   │  ├─ 验证连接有效性
   │  └─ 如果验证失败，返回错误，不发布
   ├─ 创建版本快照（WorkflowDesignSnapshotEntity）
   ├─ 复制草稿到正式版本
   │  ├─ UiDesign = UiDesignDraft
   │  └─ FunctionDesign = FunctionDesignDraft
   └─ IsPublish = true
   ↓
4. 执行工作流（ExecuteWorkflowCommand）
   └─ 使用 FunctionDesign（已发布版本）执行
   ↓
5. 继续编辑（UpdateWorkflowDefinitionCommand）
   ├─ 修改 UiDesignDraft、FunctionDesignDraft
   └─ 不影响正在运行的版本
   ↓
6. 再次发布（PublishWorkflowDefinitionCommand）
   ├─ 创建新的版本快照
   └─ 更新已发布版本
```

---

## 总结

通过这些验证要求和草稿-发布机制，我们确保：

1. ✅ **简化创建流程** - 创建时只需要基础信息，降低使用门槛
2. ✅ **安全编辑** - 编辑草稿不影响正在运行的工作流
3. ✅ **发布时验证** - 只在发布时进行全面验证，允许保存不完整的草稿
4. ✅ **版本控制** - 每次发布创建版本快照，可以回滚
5. ✅ **清晰的错误信息** - 用户能够获得详细的验证错误信息
6. ✅ **数据完整性** - 数据库中只存储有效的已发布版本
7. ✅ **执行安全** - 执行引擎只使用已验证的已发布版本

这些验证是在**发布**时进行的，而不是在**编辑**时进行的，从而提供更好的用户体验和更灵活的工作流程。
