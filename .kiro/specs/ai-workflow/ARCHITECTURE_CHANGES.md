# AI Workflow 架构调整说明

## 调整概述

根据新的架构设计，AI Workflow 不再是独立的模块，而是作为一种特殊类型的应用（AppType.Workflow）集成到 App 管理体系中。

## 主要变更

### 1. Workflow 作为应用类型

- **之前**：Workflow 是独立的实体，有自己的管理接口
- **现在**：Workflow 是 AppEntity 的一种类型（AppType.Workflow = 2）
- **影响**：创建 Workflow 时必须先创建 AppEntity，然后创建对应的 AppWorkflowDesignEntity

### 2. 统一的应用管理入口

- **创建/删除**：通过 `TeamAppController` 统一管理
  - POST `/app/team/create` - 创建应用（包括 Workflow 类型）
  - DELETE `/app/team/delete` - 删除应用
  - POST `/app/team/set_disable` - 启用/禁用应用
  - POST `/app/team/list` - 查询应用列表

### 3. Workflow 独立的编辑接口

- **编辑/发布/执行**：通过 Workflow 专用接口管理
  - GET `/app/workflow/definition/{appId}` - 获取工作流定义
  - PUT `/app/workflow/definition/{appId}` - 更新工作流定义（草稿）
  - POST `/app/workflow/definition/{appId}/publish` - 发布工作流定义
  - POST `/app/workflow/execute` - 执行工作流
  - GET `/app/workflow/instance/{id}` - 获取执行历史
  - GET `/app/workflow/instance` - 查询执行历史列表

### 4. API 层调整

- **撤销**：独立的 `MoAI.Workflow.Api` 项目
- **新方案**：将 Workflow 控制器放在 `MoAI.App.Api/Workflow/` 目录下
- **保留**：`MoAI.App.Workflow.Shared` 和 `MoAI.App.Workflow.Core` 项目

### 5. 数据模型关系

```
AppEntity (AppType=Workflow)
    ↓ (1:1)
AppWorkflowDesignEntity
    ↓ (1:N)
AppWorkflowHistoryEntity (执行记录)
    ↓ (1:N)
WorkflowDesignSnapshotEntity (版本快照)
```

## 数据库实体

### AppEntity
- 应用基础信息（名称、描述、团队、分类等）
- `AppType` 字段标识应用类型（0=Chat, 1=Agent, 2=Workflow）

### AppWorkflowDesignEntity
- 工作流设计定义
- `AppId` 关联到 AppEntity
- `UiDesign` / `UiDesignDraft` - UI 设计（发布版/草稿）
- `FunctionDesign` / `FunctionDesignDraft` - 功能设计（发布版/草稿）
- `IsPublish` - 是否已发布

### AppWorkflowHistoryEntity
- 工作流执行历史
- `AppId` 关联到 AppEntity
- `WorkflowDesignId` 关联到 AppWorkflowDesignEntity
- `State` - 执行状态
- `Data` - 执行数据（节点输入输出等）

## 工作流程

### 创建工作流应用

1. 用户通过 `TeamAppController.CreateApp` 创建应用
2. 指定 `AppType = Workflow`
3. 系统创建 `AppEntity` 和 `AppWorkflowDesignEntity`
4. 返回 AppId

### 编辑工作流

1. 用户通过 `WorkflowController.UpdateDefinition` 更新工作流
2. 系统验证节点和连接
3. 更新 `FunctionDesignDraft` 和 `UiDesignDraft` 字段
4. 草稿可以多次修改

### 发布工作流

1. 用户通过 `WorkflowController.PublishDefinition` 发布工作流
2. 系统验证草稿的有效性
3. 创建版本快照（保存当前发布版本）
4. 将草稿复制到发布字段（`FunctionDesign`, `UiDesign`）
5. 设置 `IsPublish = true`

### 执行工作流

1. 用户通过 `WorkflowController.Execute` 执行工作流
2. 系统读取发布版本的 `FunctionDesign`
3. 创建 `AppWorkflowHistoryEntity` 记录
4. 按节点顺序执行，流式返回结果
5. 保存完整执行历史

### 删除工作流应用

1. 用户通过 `TeamAppController.DeleteApp` 删除应用
2. 系统软删除 `AppEntity` 和 `AppWorkflowDesignEntity`
3. 保留 `AppWorkflowHistoryEntity` 执行历史（不删除）

## 现有代码调整

### 已完成的修复 ✅

1. **CreateAppCommandHandler.cs**
   - ✅ 修复：添加了 `AppId` 字段赋值到 `AppWorkflowDesignEntity`
   - ✅ 修复：使用 `Guid.CreateVersion7()` 替代 `Guid.NewGuid()`

2. **DeleteAppCommandHandler.cs**
   - ✅ 新增：删除 App 时同时软删除关联的 `AppWorkflowDesignEntity`
   - ✅ 保留：`AppWorkflowHistoryEntity` 执行历史不删除

3. **UpdateWorkflowDefinitionCommandHandler.cs**
   - ✅ 修复：使用 `AppWorkflowDesigns` 替代错误的 `WorkflowDesigns`
   - ✅ 修复：通过 `AppId` 查询，而不是工作流设计的 `Id`
   - ✅ 修复：基础信息（Name、Description、Avatar）更新到 `AppEntity`
   - ✅ 修复：设计字段（UiDesignDraft、FunctionDesignDraft）更新到 `AppWorkflowDesignEntity`

4. **UpdateWorkflowDefinitionCommand.cs**
   - ✅ 修复：将 `Id` 改为 `AppId`
   - ✅ 更新：注释说明基础信息存储在 `AppEntity` 中

5. **PublishWorkflowDefinitionCommandHandler.cs**
   - ✅ 修复：使用 `AppWorkflowDesigns` 替代错误的 `WorkflowDesigns`
   - ✅ 修复：通过 `AppId` 查询
   - ✅ 临时：注释掉快照相关代码（需要确认快照表结构）

6. **PublishWorkflowDefinitionCommand.cs**
   - ✅ 修复：将 `Id` 改为 `AppId`

7. **QueryWorkflowDefinitionCommandHandler.cs**
   - ✅ 修复：使用 `AppWorkflowDesigns` 替代错误的 `WorkflowDesigns`
   - ✅ 修复：通过 `AppId` 查询
   - ✅ 修复：从 `AppEntity` 获取基础信息（Name、Description、Avatar）
   - ✅ 修复：从 `AppWorkflowDesignEntity` 获取设计数据
   - ✅ 新增：验证应用类型是否为 Workflow

8. **QueryWorkflowDefinitionCommand.cs**
   - ✅ 修复：将 `Id` 改为 `AppId`
   - ✅ 更新：注释说明数据来源

9. **QueryWorkflowDefinitionCommandResponse.cs**
   - ✅ 新增：`AppId` 字段
   - ✅ 新增：`IsPublish` 字段
   - ✅ 更新：注释说明各字段的数据来源

### 需要进一步确认的问题 ⚠️

1. **快照表结构**
   - 代码中引用了 `WorkflowDesginSnapshoots` 和 `WorkflowDesginSnapshootEntity`
   - 需要确认实际的快照表名称和结构
   - 当前已注释掉快照相关代码

2. **DeleteWorkflowDefinitionCommandHandler.cs**
   - 根据新架构，删除应该通过 `TeamAppController.DeleteApp` 完成
   - 这个 Handler 可能不再需要，或者需要重新定位其用途

### 待修复的其他错误

1. **ExecuteWorkflowCommandHandler.cs**
   - 需要确认是否使用了正确的实体名称（`AppWorkflowHistoryEntity`）
   - 需要确认是否通过 `AppId` 查询工作流定义

2. **QueryWorkflowInstanceCommandHandler.cs**
   - 需要确认是否使用了正确的实体名称
   - 需要确认查询逻辑

3. **QueryWorkflowDefinitionListCommandHandler.cs**
   - 需要调整为从 `AppEntity` 和 `AppWorkflowDesignEntity` 联合查询
   - 需要过滤 `AppType = Workflow` 的应用

4. **QueryWorkflowInstanceListCommandHandler.cs**
   - 需要确认是否使用了正确的实体名称

5. **DeleteWorkflowDefinitionCommandHandler.cs**
   - 根据新架构，删除应该通过 `TeamAppController.DeleteApp` 完成
   - 这个 Handler 可能不再需要，或者需要重新定位其用途
   - 建议：标记为废弃或删除

## 下一步行动

1. ✅ 更新 requirements.md - 反映新的架构设计
2. ⏳ 更新 design.md - 更新架构图和组件说明
3. ⏳ 更新 tasks.md - 调整实现任务列表
4. ⏳ 修复现有代码中的错误
5. ⏳ 在 MoAI.App.Api 中创建 Workflow 控制器
6. ⏳ 确保 TeamAppController 正确处理 Workflow 类型的应用
