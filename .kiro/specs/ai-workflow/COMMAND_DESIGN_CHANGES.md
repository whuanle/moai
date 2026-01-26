# 工作流命令设计变更总结

## 变更概述

根据用户需求，对工作流命令进行了重大调整，采用**草稿-发布**机制，将创建、编辑和发布分离。

## 主要变更

### 1. CreateWorkflowDefinitionCommand（创建命令）

#### 变更前 ❌
```csharp
public class CreateWorkflowDefinitionCommand
{
    public int TeamId { get; set; }
    public string Name { get; set; }
    public string? Description { get; set; }
    public string? Avatar { get; set; }
    public string? UiDesign { get; set; }  // ❌ 移除
    public IReadOnlyCollection<NodeDesign> Nodes { get; set; }  // ❌ 移除
    public IReadOnlyCollection<Connection> Connections { get; set; }  // ❌ 移除
}
```

#### 变更后 ✅
```csharp
public class CreateWorkflowDefinitionCommand
{
    public int TeamId { get; set; }
    public string Name { get; set; }
    public string? Description { get; set; }
    public string? Avatar { get; set; }
    // ✅ 只保留基础信息，移除设计数据
}
```

**理由**: 
- 简化创建流程，降低使用门槛
- 用户可以先创建工作流，再逐步添加设计
- 符合"先创建骨架，再填充内容"的使用习惯

---

### 2. UpdateWorkflowDefinitionCommand（更新命令）

#### 变更前 ❌
```csharp
public class UpdateWorkflowDefinitionCommand
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string? Description { get; set; }
    public string? Avatar { get; set; }
    public string? UiDesign { get; set; }  // ❌ 改为 UiDesignDraft
    public IReadOnlyCollection<NodeDesign> Nodes { get; set; }  // ❌ 改为 FunctionDesignDraft
    public IReadOnlyCollection<Connection> Connections { get; set; }  // ❌ 改为 FunctionDesignDraft
}
```

#### 变更后 ✅
```csharp
public class UpdateWorkflowDefinitionCommand
{
    public Guid Id { get; set; }
    public string? Name { get; set; }  // ✅ 改为可选
    public string? Description { get; set; }
    public string? Avatar { get; set; }
    public string? UiDesignDraft { get; set; }  // ✅ 草稿字段
    public string? FunctionDesignDraft { get; set; }  // ✅ 草稿字段
}
```

**理由**: 
- 设计数据保存在草稿字段（`UiDesignDraft`、`FunctionDesignDraft`）
- 更新草稿不影响已发布的版本（`UiDesign`、`FunctionDesign`）
- 允许部分更新（所有字段除 ID 外都是可选的）
- 不进行业务逻辑验证，允许保存不完整的草稿

---

### 3. PublishWorkflowDefinitionCommand（发布命令）⭐ 新增

```csharp
public class PublishWorkflowDefinitionCommand
{
    public Guid Id { get; set; }
}
```

**用途**: 
- 将草稿版本（`UiDesignDraft`、`FunctionDesignDraft`）发布为正式版本（`UiDesign`、`FunctionDesign`）
- 发布时进行全面的业务逻辑验证（节点类型、连接等）
- 创建版本快照（`WorkflowDesignSnapshotEntity`）
- 设置 `IsPublish = true`

**理由**: 
- 分离编辑和发布，提供更安全的工作流程
- 只在发布时验证，允许保存不完整的草稿
- 版本控制，可以回滚到之前的版本

---

## 数据库字段映射

### WorkflowDesignEntity

| 字段 | 说明 | 何时更新 |
|------|------|---------|
| `Name` | 工作流名称 | 创建、更新 |
| `Description` | 工作流描述 | 创建、更新 |
| `Avatar` | 工作流头像 | 创建、更新 |
| `UiDesignDraft` | UI 设计草稿 | 更新 |
| `FunctionDesignDraft` | 功能设计草稿 | 更新 |
| `UiDesign` | **已发布的** UI 设计 | 发布 |
| `FunctionDesgin` | **已发布的** 功能设计 | 发布 |
| `IsPublish` | 是否已发布 | 发布 |

---

## 工作流程对比

### 变更前 ❌

```
1. 创建工作流
   └─ 必须提供完整的节点和连接配置
   ↓
2. 更新工作流
   └─ 直接更新已发布版本，影响正在运行的工作流
```

**问题**:
- ❌ 创建门槛高，必须一次性提供完整配置
- ❌ 更新直接影响已发布版本，不安全
- ❌ 没有草稿机制，无法预览和测试
- ❌ 没有版本控制，无法回滚

---

### 变更后 ✅

```
1. 创建工作流（CreateWorkflowDefinitionCommand）
   └─ 只提供基础信息（Name、Description、Avatar）
   ↓
2. 编辑草稿（UpdateWorkflowDefinitionCommand）
   ├─ 保存设计到 UiDesignDraft、FunctionDesignDraft
   └─ 不影响已发布版本
   ↓
3. 发布版本（PublishWorkflowDefinitionCommand）⭐
   ├─ 验证草稿的有效性
   ├─ 创建版本快照
   └─ 复制草稿到正式版本
   ↓
4. 执行工作流（ExecuteWorkflowCommand）
   └─ 使用已发布版本执行
   ↓
5. 继续编辑（UpdateWorkflowDefinitionCommand）
   └─ 修改草稿，不影响正在运行的版本
```

**优势**:
- ✅ 创建简单，降低使用门槛
- ✅ 草稿机制，安全编辑
- ✅ 发布时验证，确保质量
- ✅ 版本控制，可以回滚
- ✅ 不影响正在运行的工作流

---

## 验证策略变更

### 变更前 ❌

```
创建/更新时验证：
├─ 基本字段验证（长度、非空等）
└─ 业务逻辑验证（节点类型、连接等）
```

**问题**:
- ❌ 创建时必须提供完整且有效的配置
- ❌ 更新时必须保持配置完整性，无法保存不完整的草稿

---

### 变更后 ✅

```
创建时验证：
└─ 基本字段验证（Name、Description、Avatar）

更新时验证：
└─ 基本字段验证（长度限制等）
   （不验证业务逻辑，允许保存不完整的草稿）

发布时验证：⭐
├─ 基本字段验证
└─ 业务逻辑验证
   ├─ 验证节点类型
   ├─ 验证 Start/End 节点
   ├─ 验证连接有效性
   └─ 如果验证失败，返回错误，不发布
```

**优势**:
- ✅ 创建和更新时不强制完整性，提供更好的用户体验
- ✅ 发布时才进行全面验证，确保已发布版本的质量
- ✅ 验证失败不影响草稿，用户可以继续编辑

---

## API 端点变更

### 变更前 ❌

```
POST   /api/workflow/definition        创建工作流（需要完整配置）
PUT    /api/workflow/definition/{id}   更新工作流（直接更新已发布版本）
```

---

### 变更后 ✅

```
POST   /api/workflow/definition              创建工作流（只需基础信息）
PUT    /api/workflow/definition/{id}         更新工作流（更新草稿）
POST   /api/workflow/definition/{id}/publish 发布工作流（草稿→正式版本）⭐
```

---

## 实现影响

### 需要更新的 Handler

1. ✅ **CreateWorkflowDefinitionCommandHandler**
   - 移除节点和连接的处理逻辑
   - 只保存基础信息
   - 初始化空的草稿字段

2. ✅ **UpdateWorkflowDefinitionCommandHandler**
   - 改为更新草稿字段（`UiDesignDraft`、`FunctionDesignDraft`）
   - 移除业务逻辑验证
   - 支持部分更新

3. ⭐ **PublishWorkflowDefinitionCommandHandler**（新增）
   - 反序列化 `FunctionDesignDraft`
   - 调用 `WorkflowDefinitionService.ValidateWorkflowDefinition()`
   - 创建版本快照
   - 复制草稿到正式版本

4. ✅ **ExecuteWorkflowCommandHandler**
   - 使用 `FunctionDesign`（已发布版本）而非草稿

---

## 迁移指南

### 对于前端开发者

#### 创建工作流
```typescript
// 变更前 ❌
const createWorkflow = async () => {
  await api.post('/api/workflow/definition', {
    name: '我的工作流',
    description: '描述',
    nodes: [...],  // ❌ 必须提供
    connections: [...]  // ❌ 必须提供
  });
};

// 变更后 ✅
const createWorkflow = async () => {
  const result = await api.post('/api/workflow/definition', {
    name: '我的工作流',
    description: '描述'
    // ✅ 不需要提供节点和连接
  });
  return result.id;  // 返回工作流 ID
};
```

#### 编辑工作流
```typescript
// 变更前 ❌
const updateWorkflow = async (id: string, nodes: Node[], connections: Connection[]) => {
  await api.put(`/api/workflow/definition/${id}`, {
    nodes,  // ❌ 直接更新已发布版本
    connections
  });
};

// 变更后 ✅
const updateWorkflowDraft = async (id: string, uiDesign: string, functionDesign: string) => {
  await api.put(`/api/workflow/definition/${id}`, {
    uiDesignDraft: uiDesign,  // ✅ 更新草稿
    functionDesignDraft: functionDesign
  });
};
```

#### 发布工作流 ⭐
```typescript
// 新增功能
const publishWorkflow = async (id: string) => {
  try {
    await api.post(`/api/workflow/definition/${id}/publish`);
    message.success('发布成功');
  } catch (error) {
    // 显示验证错误
    message.error(error.message);
  }
};
```

---

### 对于后端开发者

#### Handler 实现变更

```csharp
// CreateWorkflowDefinitionCommandHandler
public async Task<SimpleGuid> Handle(CreateWorkflowDefinitionCommand request, CancellationToken cancellationToken)
{
    var entity = new WorkflowDesignEntity
    {
        Id = Guid.NewGuid(),
        TeamId = request.TeamId,
        Name = request.Name,
        Description = request.Description ?? string.Empty,
        Avatar = request.Avatar ?? string.Empty,
        UiDesignDraft = string.Empty,  // ✅ 初始化为空
        FunctionDesignDraft = string.Empty,  // ✅ 初始化为空
        UiDesign = string.Empty,
        FunctionDesgin = string.Empty,
        IsPublish = false  // ✅ 未发布
    };
    
    _databaseContext.WorkflowDesigns.Add(entity);
    await _databaseContext.SaveChangesAsync(cancellationToken);
    
    return new SimpleGuid { Id = entity.Id };
}
```

```csharp
// UpdateWorkflowDefinitionCommandHandler
public async Task<EmptyCommandResponse> Handle(UpdateWorkflowDefinitionCommand request, CancellationToken cancellationToken)
{
    var entity = await _databaseContext.WorkflowDesigns
        .FirstOrDefaultAsync(x => x.Id == request.Id && x.IsDeleted == 0, cancellationToken);
        
    if (entity == null)
    {
        throw new BusinessException("工作流定义不存在") { StatusCode = 404 };
    }
    
    // ✅ 部分更新
    if (!string.IsNullOrEmpty(request.Name))
        entity.Name = request.Name;
        
    if (request.Description != null)
        entity.Description = request.Description;
        
    if (request.Avatar != null)
        entity.Avatar = request.Avatar;
        
    // ✅ 更新草稿字段
    if (request.UiDesignDraft != null)
        entity.UiDesignDraft = request.UiDesignDraft;
        
    if (request.FunctionDesignDraft != null)
        entity.FunctionDesignDraft = request.FunctionDesignDraft;
    
    await _databaseContext.SaveChangesAsync(cancellationToken);
    
    return EmptyCommandResponse.Default;
}
```

---

## 总结

### 核心变更

1. ✅ **创建命令** - 移除设计数据，只保留基础信息
2. ✅ **更新命令** - 改为更新草稿字段，不影响已发布版本
3. ⭐ **发布命令** - 新增，用于将草稿发布为正式版本

### 主要优势

1. ✅ **简化创建** - 降低使用门槛
2. ✅ **安全编辑** - 草稿机制，不影响正在运行的工作流
3. ✅ **发布验证** - 只在发布时验证，允许保存不完整的草稿
4. ✅ **版本控制** - 每次发布创建快照，可以回滚

### 实现状态

- ✅ CreateWorkflowDefinitionCommand - 已更新
- ✅ UpdateWorkflowDefinitionCommand - 已更新
- ✅ PublishWorkflowDefinitionCommand - 已创建
- ✅ 所有命令的 FluentValidation 验证 - 已实现
- ⏳ Handler 实现 - 待更新
- ⏳ API 端点 - 待更新

### 下一步

1. 更新 `CreateWorkflowDefinitionCommandHandler`
2. 更新 `UpdateWorkflowDefinitionCommandHandler`
3. 实现 `PublishWorkflowDefinitionCommandHandler`
4. 更新 API 控制器，添加发布端点
5. 更新前端代码，适配新的 API
