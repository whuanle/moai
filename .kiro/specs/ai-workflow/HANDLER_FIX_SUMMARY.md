# Handler 修复总结

## 问题描述

CreateWorkflowDefinitionCommandHandler 和 UpdateWorkflowDefinitionCommandHandler 的实现与命令定义不匹配：

- **命令**已更新为草稿-发布机制（移除了 Nodes、Connections 等字段）
- **Handler** 仍在使用旧的实现（尝试访问不存在的字段）

## 修复内容

### 1. CreateWorkflowDefinitionCommandHandler

#### 修复前 ❌
```csharp
public async Task<SimpleGuid> Handle(CreateWorkflowDefinitionCommand request, ...)
{
    // ❌ 尝试访问不存在的字段
    var definition = new WorkflowDefinition
    {
        Nodes = request.Nodes,  // ❌ 字段不存在
        Connections = request.Connections  // ❌ 字段不存在
    };
    
    // ❌ 进行验证（创建时不应该验证）
    _workflowDefinitionService.ValidateWorkflowDefinition(definition);
    
    // ❌ 序列化并保存到 FunctionDesgin
    var functionDesignJson = JsonSerializer.Serialize(definition, ...);
    workflowDesignEntity.FunctionDesgin = functionDesignJson;
}
```

#### 修复后 ✅
```csharp
public async Task<SimpleGuid> Handle(CreateWorkflowDefinitionCommand request, ...)
{
    // ✅ 只创建基础信息
    var workflowDesignEntity = new WorkflowDesignEntity
    {
        Id = Guid.NewGuid(),
        TeamId = request.TeamId,
        Name = request.Name,
        Description = request.Description ?? string.Empty,
        Avatar = request.Avatar ?? string.Empty,
        UiDesign = string.Empty,  // ✅ 初始化为空
        FunctionDesgin = string.Empty,  // ✅ 初始化为空
        UiDesignDraft = string.Empty,  // ✅ 初始化为空
        FunctionDesignDraft = string.Empty,  // ✅ 初始化为空
        IsPublish = false  // ✅ 未发布
    };
    
    // ✅ 直接保存，不进行验证
    await _databaseContext.WorkflowDesigns.AddAsync(workflowDesignEntity, cancellationToken);
    await _databaseContext.SaveChangesAsync(cancellationToken);
    
    return new SimpleGuid { Value = workflowDesignEntity.Id };
}
```

**变更说明**:
- ✅ 移除了 `WorkflowDefinitionService` 依赖（创建时不需要验证）
- ✅ 移除了节点和连接的处理逻辑
- ✅ 移除了 JSON 序列化逻辑
- ✅ 只保存基础信息，所有设计字段初始化为空字符串
- ✅ 设置 `IsPublish = false`

---

### 2. UpdateWorkflowDefinitionCommandHandler

#### 修复前 ❌
```csharp
public async Task<EmptyCommandResponse> Handle(UpdateWorkflowDefinitionCommand request, ...)
{
    // ❌ 尝试访问不存在的字段
    var definition = new WorkflowDefinition
    {
        Nodes = request.Nodes,  // ❌ 字段不存在
        Connections = request.Connections  // ❌ 字段不存在
    };
    
    // ❌ 进行验证（更新草稿时不应该验证）
    _workflowDefinitionService.ValidateWorkflowDefinition(definition);
    
    // ❌ 创建版本快照（更新草稿时不应该创建快照）
    var snapshot = new WorkflowDesginSnapshootEntity { ... };
    
    // ❌ 更新已发布版本（应该更新草稿）
    workflowDesignEntity.FunctionDesgin = functionDesignJson;
}
```

#### 修复后 ✅
```csharp
public async Task<EmptyCommandResponse> Handle(UpdateWorkflowDefinitionCommand request, ...)
{
    // 查询现有实体
    var workflowDesignEntity = await _databaseContext.WorkflowDesigns
        .FirstOrDefaultAsync(w => w.Id == request.Id && w.IsDeleted == 0, cancellationToken);
        
    if (workflowDesignEntity == null)
    {
        throw new BusinessException("工作流定义不存在") { StatusCode = 404 };
    }
    
    // ✅ 部分更新：只更新提供的字段
    if (!string.IsNullOrEmpty(request.Name))
    {
        workflowDesignEntity.Name = request.Name;
    }
    
    if (request.Description != null)
    {
        workflowDesignEntity.Description = request.Description;
    }
    
    if (request.Avatar != null)
    {
        workflowDesignEntity.Avatar = request.Avatar;
    }
    
    // ✅ 更新草稿字段（不影响已发布版本）
    if (request.UiDesignDraft != null)
    {
        workflowDesignEntity.UiDesignDraft = request.UiDesignDraft;
    }
    
    if (request.FunctionDesignDraft != null)
    {
        workflowDesignEntity.FunctionDesignDraft = request.FunctionDesignDraft;
    }
    
    // ✅ 保存更改（审计字段会自动更新）
    await _databaseContext.SaveChangesAsync(cancellationToken);
    
    return EmptyCommandResponse.Default;
}
```

**变更说明**:
- ✅ 移除了 `WorkflowDefinitionService` 依赖（更新草稿时不需要验证）
- ✅ 移除了节点和连接的处理逻辑
- ✅ 移除了 JSON 序列化逻辑
- ✅ 移除了版本快照创建逻辑（发布时才创建快照）
- ✅ 改为部分更新模式（只更新提供的字段）
- ✅ 更新草稿字段而非已发布版本

---

## 构建结果

✅ **编译成功**
- 无编译错误
- 131 个代码分析警告（不影响功能）

警告类型：
- StyleCop 代码风格警告（SA1xxx）
- 代码分析建议（CA1xxx）
- 可空引用警告（CS8xxx）

这些警告是代码质量建议，不影响功能正常运行。

---

## 下一步

现在需要实现 **PublishWorkflowDefinitionCommandHandler**，用于将草稿发布为正式版本：

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
        catch (JsonException)
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

## 总结

### 修复的问题

1. ✅ **CreateWorkflowDefinitionCommandHandler** - 移除了对不存在字段的访问
2. ✅ **UpdateWorkflowDefinitionCommandHandler** - 改为更新草稿字段

### 核心变更

1. ✅ **创建** - 只保存基础信息，不进行验证
2. ✅ **更新** - 更新草稿字段，不影响已发布版本，不进行验证
3. ⏳ **发布** - 需要实现（验证草稿并发布为正式版本）

### 设计优势

- ✅ 创建简单，降低使用门槛
- ✅ 草稿机制，安全编辑
- ✅ 发布时验证，确保质量
- ✅ 版本控制，可以回滚
- ✅ 不影响正在运行的工作流
