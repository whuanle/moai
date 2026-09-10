# Wiki Document Embedding Task Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 使用通用 UUID `worker_task` 表跟踪知识库文档 MQ 向量化任务，并让前端可靠展示等待、处理、成功和失败状态。

**Architecture:** 通用任务实体位于 Database/Infra 公共层，Wiki 只用 `bind_type = embedding` 和 JSON `data` 绑定文档任务。HTTP 处理器创建任务并发布只含 `TaskId` 的消息，消费者以状态条件更新获得执行权，向量化服务只读取现有切片与现有元数据。详情接口返回最新任务状态，React 页面在活动状态下轮询。

**Tech Stack:** .NET / EF Core / PostgreSQL / Maomi.MQ / MediatR / xUnit / React 19 / TypeScript / antd 5 / Vitest

---

### Task 1: 通用任务实体与数据库映射

**Files:**
- Create: `src/database/MoAI.Database.Shared/Entities/WorkerTaskEntity.cs`
- Create: `src/database/MoAI.Database.Postgres/Data/WorkerTaskConfiguration.cs`
- Modify: `src/database/MoAI.Database.Shared/DatabaseContext.cs`
- Modify: `src/infra/MoAI.Infra.Shared/Enums/WorkerState.cs`
- Test: `tests/MoAI.UsageCounters.Tests/WorkerTaskModelTests.cs`

- [ ] **Step 1: 写失败的模型映射测试**

测试 `WorkerTaskEntity.Id` 为 `Guid`，实现 `IFullAudited`，`DatabaseContext.WorkerTasks` 为 `DbSet<WorkerTaskEntity>`；使用 Npgsql 模型元数据断言表名 `worker_task`、主键、字段长度、默认值和 `(BindType, BindId, State)` 索引。

- [ ] **Step 2: 运行测试并确认失败**

Run: `dotnet test F:\workspace\moai\tests\MoAI.UsageCounters.Tests\MoAI.UsageCounters.Tests.csproj --filter WorkerTaskModelTests`

Expected: FAIL，因为 `WorkerTaskEntity` 和 `WorkerTasks` 尚不存在。

- [ ] **Step 3: 实现通用任务实体与状态枚举**

实体包含 `Guid Id`、`string BindType`、`int BindId`、`int State`、`string Message`、`string Data` 及完整审计字段。将 `WorkerState` 命名空间改为 `MoAI.Infra.Models`，保留现有数值：`None=0`、`Wait=1`、`Processing=2`、`Cancal=3`、`Successful=4`、`Failed=5`。

- [ ] **Step 4: 配置 PostgreSQL 映射**

映射 `worker_task`，UUID 主键默认 `uuid_generate_v4()`，`bind_type` 最大长度 20，`data` 默认 `'{}'::text`，审计字段沿用 UTC 默认值和软删除约定，并建立 `idx_worker_task_binding_state` 普通组合索引。并发最终保护由消费者状态条件更新完成，不建立包含历史终态的唯一索引。

- [ ] **Step 5: 运行模型测试与编译**

Run: `dotnet test F:\workspace\moai\tests\MoAI.UsageCounters.Tests\MoAI.UsageCounters.Tests.csproj --filter WorkerTaskModelTests`

Expected: PASS。

Run: `dotnet build F:\workspace\moai\src\database\MoAI.Database.Postgres\MoAI.Database.Postgres.csproj --no-restore`

Expected: Build succeeded。

### Task 2: 向量化命令创建通用任务

**Files:**
- Create: `src/wiki/MoAI.Wiki.Shared/Commands/EmbeddingDocumentCommandResponse.cs`
- Create: `src/wiki/MoAI.Wiki.Core/Consumers/Events/WikiDocumentEmbeddingTaskData.cs`
- Modify: `src/wiki/MoAI.Wiki.Shared/Commands/EmbeddingDocumentCommand.cs`
- Modify: `src/wiki/MoAI.Wiki.Core/Consumers/Events/EmbeddingDocumentTaskMessage.cs`
- Modify: `src/wiki/MoAI.Wiki.Core/Handlers/EmbeddingDocumentCommandHandler.cs`
- Modify: `src/wiki/MoAI.Wiki.Api/Controllers/WikiDocumentController.cs`
- Test: `tests/MoAI.UsageCounters.Tests/EmbeddingDocumentCommandHandlerTests.cs`

- [ ] **Step 1: 写命令处理器失败测试**

覆盖：两项均关闭返回 400；仅原文无切片返回 409；仅元数据无元数据返回 409；存在等待/处理中任务返回 409；有效请求创建 `WorkerTaskEntity`，`Data` 包含四个配置字段，发布消息只含 `TaskId`，响应返回相同任务 ID。

- [ ] **Step 2: 运行测试确认失败**

Run: `dotnet test F:\workspace\moai\tests\MoAI.UsageCounters.Tests\MoAI.UsageCounters.Tests.csproj --filter EmbeddingDocumentCommandHandlerTests`

Expected: FAIL，旧命令仍要求 `MetadataModelId` 并返回 `EmptyCommandResponse`。

- [ ] **Step 3: 收窄命令契约**

删除 `MetadataModelId`，保留 `IsEmbedSourceText` 和 `IsEmbedMetadata`，增加至少一项为真的 FluentValidation 规则。新增只含 `Guid TaskId` 的响应类型，并将 Controller 返回类型改为该响应。

- [ ] **Step 4: 创建任务并发布消息**

处理器完成权限、模型、源数据、已有元数据和重复活动任务校验；创建 `Wait` 状态任务，`Message = "任务已创建"`，`Data` 使用 `System.Text.Json` 序列化。保存后发布 `EmbeddingDocumentTaskMessage { TaskId }`。发布异常时更新任务为 `Failed`、保存异常消息并重新抛出。

- [ ] **Step 5: 运行处理器测试**

Run: `dotnet test F:\workspace\moai\tests\MoAI.UsageCounters.Tests\MoAI.UsageCounters.Tests.csproj --filter EmbeddingDocumentCommandHandlerTests`

Expected: PASS。

### Task 3: 消费者状态机与纯向量化服务

**Files:**
- Modify: `src/wiki/MoAI.Wiki.Core/Consumers/EmbeddingDocumentConsumer.cs`
- Modify: `src/wiki/MoAI.Wiki.Core/Services/WikiEmbeddingService.cs`
- Test: `tests/MoAI.UsageCounters.Tests/EmbeddingDocumentConsumerTests.cs`
- Test: `tests/MoAI.UsageCounters.Tests/WikiEmbeddingServiceTests.cs`

- [ ] **Step 1: 写消费者状态失败测试**

覆盖：不存在任务直接 Ack；终态任务不执行；只有 `Wait` 能更新为 `Processing`；成功更新 `Successful`；执行异常及死信更新 `Failed` 和错误消息；重复消息不会再次调用服务。

- [ ] **Step 2: 写服务行为失败测试**

验证 `IsEmbedSourceText` 决定是否创建源文本记录，`IsEmbedMetadata` 只加载已保存元数据；服务不依赖 `IAiChatCompletionService`，不删除或生成元数据。

- [ ] **Step 3: 运行聚焦测试确认失败**

Run: `dotnet test F:\workspace\moai\tests\MoAI.UsageCounters.Tests\MoAI.UsageCounters.Tests.csproj --filter "EmbeddingDocumentConsumerTests|WikiEmbeddingServiceTests"`

Expected: FAIL，消费者尚不读取任务，服务仍接收元数据模型并生成元数据。

- [ ] **Step 4: 实现状态机**

消费者从 `WorkerTasks` 读取配置，使用 `ExecuteUpdateAsync` 条件 `State == Wait` 抢占任务；反序列化失败、服务失败、重试回调和死信回调均通过共享方法写 `Failed`。成功后写 `Successful` 和完成消息。

- [ ] **Step 5: 移除向量化内的元数据生成**

将 `WikiEmbeddingService.ProcessAsync` 参数改为两个 bool；删除聊天模型解析和 `GenerateMetadataAsync` 调用，仅在 `IsEmbedMetadata` 为真时调用 `LoadMetadataByChunkAsync`。独立的 `GenerateAndSaveChunkMetadataAsync` 及其聊天依赖继续保留，避免影响元数据模块。

- [ ] **Step 6: 运行聚焦测试与 Wiki Core 编译**

Run: `dotnet test F:\workspace\moai\tests\MoAI.UsageCounters.Tests\MoAI.UsageCounters.Tests.csproj --filter "EmbeddingDocumentConsumerTests|WikiEmbeddingServiceTests"`

Expected: PASS。

Run: `dotnet build F:\workspace\moai\src\wiki\MoAI.Wiki.Core\MoAI.Wiki.Core.csproj --no-restore`

Expected: Build succeeded。

### Task 4: 查询响应暴露最新任务状态

**Files:**
- Modify: `src/wiki/MoAI.Wiki.Shared/Queries/Responses/QueryWikiDocumentEmbeddingCommandResponse.cs`
- Modify: `src/wiki/MoAI.Wiki.Core/Handlers/QueryWikiDocumentEmbeddingCommandHandler.cs`
- Test: `tests/MoAI.UsageCounters.Tests/QueryWikiDocumentEmbeddingCommandHandlerTests.cs`

- [ ] **Step 1: 写查询失败测试**

创建同一文档多条任务，断言优先返回最新活动任务；没有活动任务时返回最新终态任务；无任务时三个字段为空。断言字段为 `Guid? TaskId`、`int? TaskState`、`string? TaskMessage`。

- [ ] **Step 2: 运行测试确认失败**

Run: `dotnet test F:\workspace\moai\tests\MoAI.UsageCounters.Tests\MoAI.UsageCounters.Tests.csproj --filter QueryWikiDocumentEmbeddingCommandHandlerTests`

Expected: FAIL，响应尚无任务字段。

- [ ] **Step 3: 实现任务查询**

查询 `BindType == "embedding" && BindId == document.Id`，按活动状态优先、`CreateTime` 倒序选择一条并映射到响应。

- [ ] **Step 4: 运行查询测试**

Run: `dotnet test F:\workspace\moai\tests\MoAI.UsageCounters.Tests\MoAI.UsageCounters.Tests.csproj --filter QueryWikiDocumentEmbeddingCommandHandlerTests`

Expected: PASS。

### Task 5: 同步 Kiota 契约并更新前端 API

**Files:**
- Generated: `ui/src/api/client/**`
- Modify: `ui/src/api/wiki.ts`
- Test: `ui/src/pages/wiki/__tests__/WikiDocumentDetail.test.tsx`

- [ ] **Step 1: 启动后端并同步 OpenAPI**

Run: `dotnet run --project F:\workspace\moai\src\MoAI\MoAI.csproj`

Expected: 后端监听配置端口且 `/openapi/v1.json` 可访问。

Run: `npm --prefix F:\workspace\moai\ui run syncapi`

Expected: Kiota 客户端重新生成，`EmbeddingDocumentCommand` 无 `metadataModelId`，POST 响应含 `taskId`，查询响应含任务字段。

- [ ] **Step 2: 更新手写 API 封装**

`triggerDocumentEmbedding` payload 只接收两个 bool，并返回 `{ taskId?: string | null }`；不直接手改 `src/api/client/`。

- [ ] **Step 3: 运行 TypeScript 类型检查**

Run: `npm --prefix F:\workspace\moai\ui run typecheck`

Expected: 仅页面旧调用因 `metadataModelId` 和新任务字段产生预期失败，API 封装无类型错误。

### Task 6: 前端提交、状态展示与轮询

**Files:**
- Modify: `ui/src/pages/wiki/WikiDocumentDetail.tsx`
- Modify: `ui/src/i18n/locales/zh-CN/common.json`
- Modify: `ui/src/i18n/locales/en-US/common.json`
- Modify: `ui/src/pages/wiki/__tests__/WikiDocumentDetail.test.tsx`

- [ ] **Step 1: 更新失败测试**

删除“提交 metadataModel”用例，增加：页面不存在元数据模型选择器；两个开关可分别提交；全部关闭时表单校验且不调用 API；`Wait/Processing` 时按钮禁用并轮询；`Successful/Failed` 后停止轮询并展示对应消息。

- [ ] **Step 2: 运行页面测试确认失败**

Run: `npm --prefix F:\workspace\moai\ui run test -- WikiDocumentDetail.test.tsx`

Expected: FAIL，页面仍展示元数据生成模型且无任务状态逻辑。

- [ ] **Step 3: 修改向量化表单**

删除 `metadataModelId` 字段和选择器，保留两个 Checkbox；增加至少一项必选的 Form validator。提交成功提示“任务已创建”，立即刷新详情。

- [ ] **Step 4: 添加状态与轮询**

将 `taskState` 1、2 视为活动状态，每 2 秒调用 `load(false)`；组件卸载或进入终态时清理 timer。活动状态展示 Tag/Alert 和任务消息并禁用按钮；状态 4 展示成功，状态 5 展示失败。

- [ ] **Step 5: 同步中英文文案**

新增任务等待、处理中、成功、失败、至少选择一项等文案；删除仅由向量化表单使用的元数据模型文案引用，但保留独立元数据模块文案。

- [ ] **Step 6: 运行页面测试、类型检查和 lint**

Run: `npm --prefix F:\workspace\moai\ui run test -- WikiDocumentDetail.test.tsx`

Expected: PASS。

Run: `npm --prefix F:\workspace\moai\ui run typecheck`

Expected: PASS。

Run: `npm --prefix F:\workspace\moai\ui run lint`

Expected: PASS。

### Task 7: 全链路验证

**Files:**
- Verify only: all changed backend and frontend files

- [ ] **Step 1: 后端完整编译与测试**

Run: `dotnet build F:\workspace\moai\src\MoAI\MoAI.csproj --no-restore`

Expected: Build succeeded。

Run: `dotnet test F:\workspace\moai\tests\MoAI.UsageCounters.Tests\MoAI.UsageCounters.Tests.csproj --no-build`

Expected: 全部通过。

- [ ] **Step 2: 前端完整验证**

Run: `npm --prefix F:\workspace\moai\ui run typecheck`

Run: `npm --prefix F:\workspace\moai\ui run lint`

Run: `npm --prefix F:\workspace\moai\ui run test`

Expected: 全部通过。

- [ ] **Step 3: 浏览器验证**

打开 `/team/1/wiki/1/document/5/embedding`，验证不存在向量化元数据模型选择器；选择任一或两个来源可创建任务；页面展示等待/处理中并自动更新到成功或失败；刷新页面后活动任务状态仍可恢复。

- [ ] **Step 4: 差异检查**

Run: `git -C F:\workspace\moai diff --check`

Expected: 无空白错误；生成客户端仅包含 OpenAPI 契约变化，没有手工编辑痕迹。