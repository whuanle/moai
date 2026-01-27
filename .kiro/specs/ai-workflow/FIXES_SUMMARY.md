# AI Workflow 代码修复总结

## 修复概述

根据新的架构设计（Workflow 作为 AppEntity 的一种类型），我们对现有代码进行了全面修复，确保：
1. 应用基础信息（Name、Description、Avatar）存储在 `AppEntity` 中
2. 工作流设计数据存储在 `AppWorkflowDesignEntity` 中
3. 所有操作通过 `AppId` 进行，而不是工作流设计的独立 ID
4. 使用正确的数据库表名称（`AppWorkflowDesigns` 而不是 `WorkflowDesigns`）

## 已完成的修复（9 个文件）

### 1. CreateAppCommandHandler.cs ✅
**问题**：创建 Workflow 应用时缺少 `AppId` 字段赋值

**修复**：
```csharp
// 之前
var workflowDesignEntity = new AppWorkflowDesignEntity
{
    Id = Guid.NewGuid(),
    TeamId = request.TeamId,
    // 缺少 AppId
    ...
};

// 修复后
var workflowDesignEntity = new AppWorkflowDesignEntity
{
    Id = Guid.CreateVersion7(),
    TeamId = request.TeamId,
    AppId = appId,  // 关联到 AppEntity
    ...
};
```

### 2. DeleteAppCommandHandler.cs ✅
**问题**：删除应用时没有同时删除关联的 `AppWorkflowDesignEntity`

**修复**：
```csharp
// 新增逻辑
if (appEntity.AppType == (int)App.Models.AppType.Workflow)
{
    var workflowDesignEntity = await _databaseContext.AppWorkflowDesigns
        .FirstOrDefaultAsync(w => w.AppId == request.AppId && w.IsDeleted == 0, cancellationToken);

    if (workflowDesignEntity != null)
    {
        _databaseContext.AppWorkflowDesigns.Remove(workflowDesignEntity);
    }
}
// 注意：AppWorkflowHistoryEntity 执行历史不会被删除，保留以供审计
```

### 3. UpdateWorkflowDefinitionCommandHandler.cs ✅
**问题**：
- 使用了错误的表名 `WorkflowDesigns`
- 试图更新 `AppWorkflowDesignEntity` 中不存在的字段（Name、Description、Avatar）

**修复**：
```csharp
// 1. 查询工作流设计实体
var workflowDesignEntity = await _databaseContext.AppWorkflowDesigns
    .FirstOrDefaultAsync(w => w.AppId == request.AppId && w.IsDeleted == 0, cancellationToken);

// 2. 查询应用实体（用于更新基础信息）
var appEntity = await _databaseContext.Apps
    .FirstOrDefaultAsync(a => a.Id == request.AppId && a.IsDeleted == 0, cancellationToken);

// 3. 更新应用基础信息到 AppEntity
if (!string.IsNullOrEmpty(request.Name))
{
    appEntity.Name = request.Name;
}

// 4. 更新设计字段到 AppWorkflowDesignEntity
if (request.UiDesignDraft != null)
{
    workflowDesignEntity.UiDesignDraft = JsonSerializer.Serialize(...);
}
```

### 4. UpdateWorkflowDefinitionCommand.cs ✅
**问题**：使用 `Id` 字段而不是 `AppId`

**修复**：
```csharp
// 之前
public Guid Id { get; set; }

// 修复后
public Guid AppId { get; set; }  // 应用 ID（AppEntity.Id）
```

### 5. PublishWorkflowDefinitionCommandHandler.cs ✅
**问题**：
- 使用了错误的表名 `WorkflowDesigns`
- 使用了不存在的快照表 `WorkflowDesginSnapshoots`

**修复**：
```csharp
// 1. 修复表名
var workflowDesignEntity = await _databaseContext.AppWorkflowDesigns
    .FirstOrDefaultAsync(w => w.AppId == request.AppId && w.IsDeleted == 0, cancellationToken);

// 2. 临时注释掉快照相关代码（需要确认快照表结构）
// TODO: 需要确认快照表的实际名称和结构
```

### 6. PublishWorkflowDefinitionCommand.cs ✅
**问题**：使用 `Id` 字段而不是 `AppId`

**修复**：
```csharp
// 之前
public Guid Id { get; set; }

// 修复后
public Guid AppId { get; set; }  // 应用 ID（AppEntity.Id）
```

### 7. QueryWorkflowDefinitionCommandHandler.cs ✅
**问题**：
- 使用了错误的表名 `WorkflowDesigns`
- 试图从 `AppWorkflowDesignEntity` 读取不存在的字段（Name、Description、Avatar）

**修复**：
```csharp
// 1. 查询应用实体（获取基础信息）
var appEntity = await _databaseContext.Apps
    .Where(a => a.Id == request.AppId && a.IsDeleted == 0)
    .FirstOrDefaultAsync(cancellationToken);

// 2. 验证是否为 Workflow 类型
if (appEntity.AppType != (int)App.Models.AppType.Workflow)
{
    throw new BusinessException("该应用不是工作流类型") { StatusCode = 400 };
}

// 3. 查询工作流设计实体（获取设计数据）
var workflowDesign = await _databaseContext.AppWorkflowDesigns
    .Where(w => w.AppId == request.AppId && w.IsDeleted == 0)
    .FirstOrDefaultAsync(cancellationToken);

// 4. 构建响应（基础信息来自 AppEntity，设计数据来自 AppWorkflowDesignEntity）
return new QueryWorkflowDefinitionCommandResponse
{
    Id = workflowDesign.Id,
    AppId = appEntity.Id,
    Name = appEntity.Name,  // 来自 AppEntity
    Description = appEntity.Description,  // 来自 AppEntity
    Avatar = appEntity.Avatar,  // 来自 AppEntity
    UiDesign = workflowDesign.UiDesign,  // 来自 AppWorkflowDesignEntity
    FunctionDesign = workflowDesign.FunctionDesgin,  // 来自 AppWorkflowDesignEntity
    ...
};
```

### 8. QueryWorkflowDefinitionCommand.cs ✅
**问题**：使用 `Id` 字段而不是 `AppId`

**修复**：
```csharp
// 之前
public Guid Id { get; init; }

// 修复后
public Guid AppId { get; init; }  // 应用 ID（AppEntity.Id）
```

### 9. QueryWorkflowDefinitionCommandResponse.cs ✅
**问题**：缺少 `AppId` 和 `IsPublish` 字段

**修复**：
```csharp
// 新增字段
public Guid AppId { get; init; }  // 应用 ID
public bool IsPublish { get; init; }  // 是否已发布

// 更新注释说明数据来源
public string Name { get; init; }  // 工作流名称（来自 AppEntity）
public string Description { get; init; }  // 工作流描述（来自 AppEntity）
public string Avatar { get; init; }  // 工作流头像（来自 AppEntity）
```

## 关键架构变更

### 数据分离原则

| 数据类型 | 存储位置 | 字段示例 |
|---------|---------|---------|
| 应用基础信息 | `AppEntity` | Name, Description, Avatar, TeamId, AppType |
| 工作流设计数据 | `AppWorkflowDesignEntity` | UiDesign, FunctionDesign, UiDesignDraft, FunctionDesignDraft |
| 工作流执行历史 | `AppWorkflowHistoryEntity` | State, Data, RunParameters |

### ID 使用规范

- **AppId**：`AppEntity.Id`，用于标识应用
- **WorkflowDesignId**：`AppWorkflowDesignEntity.Id`，用于内部关联
- **所有外部接口都使用 AppId**，不暴露 WorkflowDesignId

### 查询模式

```csharp
// 标准查询模式：联合查询 AppEntity 和 AppWorkflowDesignEntity
var appEntity = await _databaseContext.Apps
    .Where(a => a.Id == appId && a.IsDeleted == 0)
    .FirstOrDefaultAsync(cancellationToken);

var workflowDesign = await _databaseContext.AppWorkflowDesigns
    .Where(w => w.AppId == appId && w.IsDeleted == 0)
    .FirstOrDefaultAsync(cancellationToken);

// 组合数据返回
return new Response
{
    Name = appEntity.Name,  // 来自 AppEntity
    UiDesign = workflowDesign.UiDesign  // 来自 AppWorkflowDesignEntity
};
```

## 待处理事项

### 1. 快照表确认 ⚠️
- 需要确认快照表的实际名称和结构
- 当前代码中引用了 `WorkflowDesginSnapshoots` 和 `WorkflowDesginSnapshootEntity`
- 已临时注释掉相关代码

### 2. 其他 Handler 修复 ⏳
需要检查和修复以下文件：
- `ExecuteWorkflowCommandHandler.cs`
- `QueryWorkflowInstanceCommandHandler.cs`
- `QueryWorkflowDefinitionListCommandHandler.cs`
- `QueryWorkflowInstanceListCommandHandler.cs`

### 3. DeleteWorkflowDefinitionCommand 废弃 ⏳
- 根据新架构，删除应该通过 `TeamAppController.DeleteApp` 完成
- 建议标记 `DeleteWorkflowDefinitionCommand` 为废弃或删除

### 4. API 控制器创建 ⏳
- 在 `MoAI.App.Api/Workflow/` 目录下创建 `WorkflowController`
- 实现工作流的编辑、发布、执行接口

## 测试建议

### 单元测试
1. 测试创建 Workflow 应用时 `AppId` 正确赋值
2. 测试删除 Workflow 应用时关联数据正确删除
3. 测试更新工作流时基础信息和设计数据分别更新到正确的表
4. 测试查询工作流时正确组合两个表的数据

### 集成测试
1. 完整的工作流创建 → 编辑 → 发布 → 执行 → 删除流程
2. 验证执行历史在删除应用后仍然保留
3. 验证草稿和发布版本的隔离

## 总结

本次修复确保了 Workflow 作为 AppEntity 的一种类型的架构设计得以正确实现。主要成果：

✅ 修复了 9 个文件中的数据模型错误
✅ 统一了 ID 使用规范（使用 AppId）
✅ 明确了数据分离原则（基础信息 vs 设计数据）
✅ 确保了删除操作的正确性（保留执行历史）
✅ 建立了标准的查询模式（联合查询）

下一步需要继续修复其他 Handler 并创建 API 控制器。
