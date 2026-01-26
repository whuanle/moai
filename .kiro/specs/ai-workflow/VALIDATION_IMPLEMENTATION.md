# 工作流命令验证实现总结

## 概述

已成功为所有工作流 Commands 和 Queries 实现 `IModelValidator<>` 接口，使用 FluentValidation 替代 DataAnnotations 进行模型验证。

## 工作流设计理念

### 草稿与发布机制

工作流采用**草稿-发布**的设计模式：

1. **创建阶段** - 只创建基础信息（名称、描述、头像），不包含设计数据
2. **编辑阶段** - 编辑设计草稿（`UiDesignDraft`、`FunctionDesignDraft`），不影响已发布版本
3. **发布阶段** - 将草稿发布为正式版本（`UiDesign`、`FunctionDesign`），创建版本快照

这种设计允许用户：
- ✅ 安全地编辑工作流而不影响正在运行的版本
- ✅ 在发布前预览和测试草稿
- ✅ 维护版本历史记录
- ✅ 回滚到之前的版本

## 实现的命令

### Commands（命令）

#### 1. CreateWorkflowDefinitionCommand
**文件**: `src/workflow/MoAI.Workflow.Shared/Commands/CreateWorkflowDefinitionCommand.cs`

**用途**: 创建新的工作流定义，只包含基础信息

**字段**:
- `TeamId` - 团队 ID
- `Name` - 工作流名称
- `Description` - 工作流描述（可选）
- `Avatar` - 工作流头像 ObjectKey（可选）

**验证规则**:
- `Name`: 不能为空，最大长度 100 字符
- `Description`: 最大长度 500 字符（可选）
- `Avatar`: 最大长度 200 字符（可选）

**设计说明**: 创建时不需要设计数据，用户可以在创建后通过更新命令添加设计草稿。

---

#### 2. UpdateWorkflowDefinitionCommand
**文件**: `src/workflow/MoAI.Workflow.Shared/Commands/UpdateWorkflowDefinitionCommand.cs`

**用途**: 更新工作流的基础信息和设计草稿

**字段**:
- `Id` - 工作流定义 ID
- `Name` - 工作流名称（可选）
- `Description` - 工作流描述（可选）
- `Avatar` - 工作流头像 ObjectKey（可选）
- `UiDesignDraft` - UI 设计草稿（JSON 格式，可选）
- `FunctionDesignDraft` - 功能设计草稿（JSON 格式，可选）

**验证规则**:
- `Id`: 不能为空
- `Name`: 最大长度 100 字符（如果提供）
- `Description`: 最大长度 500 字符（如果提供）
- `Avatar`: 最大长度 200 字符（如果提供）

**设计说明**: 
- 所有字段（除了 ID）都是可选的，允许部分更新
- 设计草稿保存在 `UiDesignDraft` 和 `FunctionDesignDraft` 字段
- 更新草稿不会影响已发布的版本（`UiDesign`、`FunctionDesign`）

---

#### 3. PublishWorkflowDefinitionCommand ⭐ 新增
**文件**: `src/workflow/MoAI.Workflow.Shared/Commands/PublishWorkflowDefinitionCommand.cs`

**用途**: 将草稿版本发布为正式版本

**字段**:
- `Id` - 工作流定义 ID

**验证规则**:
- `Id`: 不能为空

**设计说明**: 
- 将 `UiDesignDraft` 复制到 `UiDesign`
- 将 `FunctionDesignDraft` 复制到 `FunctionDesign`
- 创建版本快照（`WorkflowDesignSnapshotEntity`）
- 验证工作流定义的有效性（节点类型、连接等）
- 设置 `IsPublish = true`

---

#### 4. DeleteWorkflowDefinitionCommand
**文件**: `src/workflow/MoAI.Workflow.Shared/Commands/DeleteWorkflowDefinitionCommand.cs`

**验证规则**:
- `Id`: 不能为空

---

#### 5. ExecuteWorkflowCommand
**文件**: `src/workflow/MoAI.Workflow.Shared/Commands/ExecuteWorkflowCommand.cs`

**验证规则**:
- `WorkflowDefinitionId`: 不能为空
- `StartupParameters`: 不能为 null

**设计说明**: 执行时使用已发布的版本（`FunctionDesign`），不使用草稿。

---

### Queries（查询）

#### 6. QueryWorkflowDefinitionCommand
**文件**: `src/workflow/MoAI.Workflow.Shared/Queries/QueryWorkflowDefinitionCommand.cs`

**验证规则**:
- `Id`: 不能为空

**设计说明**: 
- `IncludeDraft = false` - 返回已发布版本
- `IncludeDraft = true` - 返回草稿版本

---

#### 7. QueryWorkflowInstanceCommand
**文件**: `src/workflow/MoAI.Workflow.Shared/Queries/QueryWorkflowInstanceCommand.cs`

**验证规则**:
- `Id`: 不能为空

---

## 数据库字段映射

### WorkflowDesignEntity

| 字段 | 类型 | 说明 |
|------|------|------|
| `Id` | Guid | 工作流定义 ID |
| `TeamId` | int | 团队 ID |
| `Name` | string | 工作流名称 |
| `Description` | string | 工作流描述 |
| `Avatar` | string | 工作流头像 ObjectKey |
| `UiDesign` | string | **已发布的** UI 设计（JSON） |
| `FunctionDesgin` | string | **已发布的** 功能设计（JSON） |
| `UiDesignDraft` | string | **草稿** UI 设计（JSON） |
| `FunctionDesignDraft` | string | **草稿** 功能设计（JSON） |
| `IsPublish` | bool | 是否已发布 |

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
   └─ IsPublish 保持 false
   ↓
3. 发布版本（PublishWorkflowDefinitionCommand）⭐
   ├─ 验证 FunctionDesignDraft 的有效性
   │  ├─ 验证节点类型
   │  ├─ 验证 Start/End 节点
   │  ├─ 验证连接有效性
   │  └─ 如果验证失败，返回错误，不发布
   ├─ UiDesign = UiDesignDraft
   ├─ FunctionDesign = FunctionDesignDraft
   ├─ 创建版本快照（WorkflowDesignSnapshotEntity）
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

## 实现模式

所有命令都遵循相同的模式：

```csharp
using FluentValidation;
using MediatR;
// ... 其他 using

public class XxxCommand : IRequest<TResponse>, IModelValidator<XxxCommand>
{
    // 属性定义
    public string Name { get; set; }
    
    // 实现验证方法
    public static void Validate(AbstractValidator<XxxCommand> validate)
    {
        validate.RuleFor(x => x.Name)
            .NotEmpty().WithMessage("错误消息")
            .MaximumLength(100).WithMessage("长度限制消息");
            
        // 可选字段使用 When 条件
        validate.RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("描述不能超过500个字符")
            .When(x => !string.IsNullOrEmpty(x.Description));
    }
}
```

## 优势

### 1. 草稿-发布机制
- ✅ 安全编辑：编辑草稿不影响正在运行的工作流
- ✅ 版本控制：每次发布创建版本快照
- ✅ 预览测试：可以在发布前测试草稿
- ✅ 回滚能力：可以回滚到之前的版本

### 2. 统一的验证方式
- ✅ 所有命令使用相同的 `IModelValidator<>` 接口
- ✅ 符合 MoAI 项目的 CQRS 约定
- ✅ 与其他模块（如 Wiki、Plugin）保持一致

### 3. FluentValidation 的优势
- ✅ 更强大和灵活的验证规则
- ✅ 更好的可读性和可维护性
- ✅ 支持复杂的条件验证（`When`、`Unless`）
- ✅ 更清晰的错误消息定制

### 4. 关注点分离
- ✅ 基本格式验证：FluentValidation（字段长度、非空等）
- ✅ 业务逻辑验证：WorkflowDefinitionService（节点类型、连接等）
- ✅ 发布时验证：PublishWorkflowDefinitionCommand Handler

## 验证流程

### 创建工作流
```
1. API 请求到达（POST /api/workflow/definition）
   ↓
2. ASP.NET Core 模型绑定（JSON 反序列化）
   ↓
3. FluentValidation 自动验证
   ├─ 验证 Name 不为空且长度 ≤ 100
   ├─ 验证 Description 长度 ≤ 500（如果提供）
   └─ 如果失败，返回 400 Bad Request
   ↓
4. MediatR 发送命令到 CreateWorkflowDefinitionCommandHandler
   ↓
5. Handler 创建实体并保存到数据库
   ├─ 保存基础信息
   ├─ 设置空的草稿字段
   └─ IsPublish = false
```

### 更新工作流
```
1. API 请求到达（PUT /api/workflow/definition/{id}）
   ↓
2. FluentValidation 验证
   ├─ 验证 Id 不为空
   ├─ 验证可选字段的长度限制
   └─ 如果失败，返回 400 Bad Request
   ↓
3. MediatR 发送命令到 UpdateWorkflowDefinitionCommandHandler
   ↓
4. Handler 更新实体
   ├─ 更新基础信息（如果提供）
   ├─ 更新草稿字段（如果提供）
   └─ 不修改已发布版本
```

### 发布工作流 ⭐
```
1. API 请求到达（POST /api/workflow/definition/{id}/publish）
   ↓
2. FluentValidation 验证
   └─ 验证 Id 不为空
   ↓
3. MediatR 发送命令到 PublishWorkflowDefinitionCommandHandler
   ↓
4. Handler 执行发布流程
   ├─ 检索工作流定义
   ├─ 反序列化 FunctionDesignDraft
   ├─ 调用 WorkflowDefinitionService.ValidateWorkflowDefinition()
   │  ├─ 验证节点类型
   │  ├─ 验证 Start/End 节点
   │  ├─ 验证连接有效性
   │  └─ 如果验证失败，返回详细错误，不发布
   ├─ 创建版本快照（WorkflowDesignSnapshotEntity）
   ├─ 复制草稿到正式版本
   │  ├─ UiDesign = UiDesignDraft
   │  └─ FunctionDesign = FunctionDesignDraft
   └─ IsPublish = true
```

## 构建结果

✅ 所有文件编译成功
✅ 无编译错误
⚠️ 8 个代码分析警告（不影响功能）

警告主要是：
- CA1720: 枚举值包含类型名称（String、Object）
- CA1805: 显式初始化为默认值
- CA2227: 集合属性建议只读

这些警告是代码质量建议，不影响功能正常运行。

## 下一步

1. ✅ 实现 `CreateWorkflowDefinitionCommandHandler` - 只保存基础信息
2. ✅ 实现 `UpdateWorkflowDefinitionCommandHandler` - 更新基础信息和草稿
3. ⭐ 实现 `PublishWorkflowDefinitionCommandHandler` - 发布草稿并验证
4. ✅ 实现 `DeleteWorkflowDefinitionCommandHandler` - 软删除
5. ✅ 实现 `ExecuteWorkflowCommandHandler` - 使用已发布版本执行

## API 端点设计

### 工作流定义管理

```
POST   /api/workflow/definition              创建工作流（基础信息）
GET    /api/workflow/definition/{id}         获取工作流定义
PUT    /api/workflow/definition/{id}         更新工作流（基础信息+草稿）
DELETE /api/workflow/definition/{id}         删除工作流
POST   /api/workflow/definition/{id}/publish 发布工作流（草稿→正式版本）⭐
GET    /api/workflow/definition              列表查询
```

### 工作流执行

```
POST   /api/workflow/execute                 执行工作流（使用已发布版本）
GET    /api/workflow/instance/{id}           获取执行历史
GET    /api/workflow/instance                列表查询
```

## 参考

- FluentValidation 文档: https://docs.fluentvalidation.net/
- MoAI CQRS 约定: `.kiro/steering/cqrs-conventions.md`
- 验证需求文档: `.kiro/specs/ai-workflow/VALIDATION_REQUIREMENTS.md`
- 数据库实体: `src/database/MoAI.Database.Shared/Entities/WorkflowDesignEntity.cs`
