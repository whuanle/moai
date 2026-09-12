# Agent 运行时验证映射（TDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md)

## 回归命令

```bash
dotnet build src/MoAI/MoAI.csproj                 # 0 error
cd ui && npm run typecheck && npm run lint && npm run test
# E2E（待补 local-dev/agent-chat-e2e.mjs；需后端运行 + 已执行 SOP 第 1 节 DDL）
```

## 场景映射

| 场景 | 验证物 | 结果（日期） |
|---|---|---|
| @AI-S1 / S2 / S3 / S4 | @manual（待补 local-dev/agent-chat-e2e.mjs#publish；需后端与 DDL） | PENDING（2026-09-11） |
| @AI-S5 / S6 | 代码走查（`CreateAppSessionCommandHandler` 发布态/类型校验）；HTTP 实测建会话 200（2026-09-11） | PASS（2026-09-11） |
| @AI-S7 | 代码走查（`QueryAppSessionsCommandHandler` 归属 + 倒序） | PASS（2026-09-11） |
| @AI-S8 | 代码走查（`AppAgentDispatcher.ResolveInnerAsync` 归属校验 / `AppSessionController` Handler 404） | PASS（2026-09-11） |
| @AI-S9 | HTTP 实测重命名/删除/列表均 200（2026-09-11） | PASS（2026-09-11） |
| @AI-S10 ~ S13 | HTTP 实测 `POST /api/agent/{appId}/chat` 返回 200 `text/event-stream` 且含 `RUN_STARTED`/`TEXT_MESSAGE_START`（未配模型时以可见文本回错误）（2026-09-11） | PASS（2026-09-11） |
| @AI-S13 | 代码走查（`AppAgentFactory` 未配置模型 → 400 → RunError） | PASS（2026-09-11） |
| @AI-S14 | 代码走查（`AppChatFlushService` + `AppCompactionStrategyFactory`） | PASS（2026-09-11） |
| @AI-S15 | @manual（配置 enableSummarization 后走查） | PENDING（2026-09-11） |
| @AI-S16 / S17 | 代码走查（`WikiAppToolProvider` + `WikiSearchService`）；单测 `WikiAppToolProviderTests` | PASS（2026-09-11） |
| @AI-S18 | 代码走查（`UsageCapturingChatClient`） | PASS（2026-09-11） |
| @AI-S21 / S23 / S24 | E2E（临时脚本 `tools-e2e.mjs`：桩模型驱动 `list_tools`→`call_tool` 调用动态插件 `dynamic_greet`，返回 `Hello MoAI`）；单测 `AppToolContextProviderTests` / `AppToolContextProviderContributorTests` | PASS（2026-09-11） |
| @AI-S22 | 单测 `WikiAppToolProviderTests`（命中返回、缺 query 失败）；代码走查 | PASS（2026-09-11） |
| @AI-S25 | 单测 `AppToolContextProviderContributorTests.Create_NoTools_ReturnsNull` | PASS（2026-09-11） |
| @AI-S26 | @manual（MCP/OpenAPI 需真实服务器/文档；调用代码走查 `McpToolCallService` / `OpenApiToolCallService`） | PENDING（2026-09-11） |
| @AI-S27 / S29 | E2E（`sandbox-e2e.mjs`：桩模型驱动 `list_tools`→`call_tool(sandbox_run_shell)`，返回 `{"success":true,"stdout":"sandbox_ok","exitCode":0}`）；单测 `SandboxAppToolProviderTests` | PASS（2026-09-11） |
| @AI-S28 | 单测 `SandboxAppToolProviderTests.GetTools_Disabled/ExplicitlyDisabled_ReturnsEmpty` | PASS（2026-09-11） |
| @AI-S30 | @manual（代码/文件需真实沙箱；结果映射单测 `SandboxExecutionMappingTests`） | PENDING（2026-09-11） |
| @AI-S31 | @manual（会话隔离与 TTL 回收走查 + `OpenSandboxService` 逻辑） | PENDING（2026-09-11） |
| 前端对话页 | ui/src/pages/teams/apps/__tests__/AppChat.test.tsx | PASS 3/3（2026-09-11） |
| 应用管理页沙箱配置 | ui/src/pages/teams/apps/__tests__/AppManage.test.tsx（开关 + 存活时间/续期/镜像/CPU/内存/网络参数回显；保存携带 sandbox 配置） | PASS 7/7（2026-09-11） |
| @AI-S19 / S19b / S20 | ui/src/pages/teams/apps/__tests__/AppChat.test.tsx（沉浸式布局/无面包屑、Markdown 渲染）+ Playwright 走查（明暗主题、无横向溢出） | PASS（2026-09-11） |
| 编译 | dotnet build src/MoAI/MoAI.csproj | PASS 0 error（2026-09-11） |
| 前端静态检查 | npm run typecheck / lint / test | PASS（typecheck 0、lint 0、test 234/234）（2026-09-11） |

## 单测覆盖

| 目标 | 位置 | 说明 |
|---|---|---|
| 工具列表/筛选/惰性参数示例/调用/错误 | `tests/MoAI.AI.Core.Tests/AppToolContextProviderTests.cs` | 6 例，PASS |
| 工具来源聚合与按名去重、无工具返回 null | `tests/MoAI.AI.Core.Tests/AppToolContextProviderContributorTests.cs` | 2 例，PASS |
| 知识库工具生成与调用 | `tests/MoAI.AI.Core.Tests/WikiAppToolProviderTests.cs` | 3 例，PASS |
| 沙箱配置解析 | `tests/MoAI.AI.Core.Tests/SandboxSettingsTests.cs` | 4 例，PASS |
| 沙箱工具门禁与清单 | `tests/MoAI.AI.Core.Tests/SandboxAppToolProviderTests.cs` | 4 例，PASS |
| 沙箱执行结果映射 | `tests/MoAI.AI.Core.Tests/SandboxExecutionMappingTests.cs` | 2 例，PASS |
| `ChatMessageMapper` 双向映射 | （待补） | 工具调用/结果/思维链 round-trip |
| flush 压缩替换 | （待补） | 压缩后行数与 seq 连续 |
| `AppAgentSessionStore` 归属 | （待补） | 非归属不恢复 |

> PENDING 项需后端运行且已执行 [SOP 第 1 节](./sop.md#1-应用-schema-变更) 的 DDL，再补 `local-dev/agent-chat-e2e.mjs` 后执行。

## 本轮修复（2026-09-11）

1. **「应用 id 不正确」**：`CreateAppSessionCommand` / `UpdateAppSessionTitleCommand` 把 Controller 从路由回填的 id 放进了 `Validate`，而 `[ApiController]` 会在回填**之前**自动校验 body 绑定的命令 → 必然 400。按既有约定（见 `UpdateAppCommand`）移除这两个命令中路由 id 的校验规则，只校验请求体字段。
2. **会话 `user_type` 恒为 0**：`UserContextProvider.Parse()` 未从 token 的 `typ` 声明填充 `UserType`（`TokenProvider` 有填充，HTTP 路径没有）。已补上，内部用户会话现为 `Normal=3`。
3. **未配模型时对话返回空流**：`AppAgentDispatcher` 现在把解析期/运行期异常转成可见助手文本，前端能显示「应用尚未配置对话模型，无法对话.」。
4. **一轮多段文本被覆盖**（对话页）：AG-UI 的 `textMessageBuffer` 是按 `messageId` 的缓冲，模型一轮内若产出多段文本（文本 → 工具调用 → 文本），原实现只保留最后一段。已改为按 `messageId` 累积后拼接，并补 `ui/src/api/__tests__/agentChat.test.ts`（单段累积 / 多段拼接 / 仅发本轮消息）。
5. **端到端实测**（2026-09-11）：自建 OpenAI 兼容 SSE 桩模型 → 建渠道/模型/应用 → 发布 → 对话，SSE 事件序列正确（RUN_STARTED → TEXT_MESSAGE_START → N×CONTENT → END → RUN_FINISHED），落库 user/assistant 两条且标题自动生成；对话页 Markdown（粗体/行内码/代码块/列表）渲染正确、复制按钮存在。
