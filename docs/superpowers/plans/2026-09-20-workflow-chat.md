# 流程应用对话（question 契约 + MAF 压缩 + 工作台 Tab）实施计划

> **For agentic workers:** 本计划在当前会话内联执行（自主模式），步骤用 checkbox 跟踪。仓库工作区已有用户未提交改动，**全程不做 git commit**。

**Goal:** 流程应用支持带历史的对话：开始节点固定 `question` 字段、`sys.history` 用 MAF 压缩组件单独压缩、工作台头部新增 设计/调试/配置 Tab（配置含 信息/日志/监控/外部渠道 二级菜单）。

**Architecture:** 后端只改 `WorkflowAppChatInvoker`（用户问题进 `input.question`，同时镜像 `query` 兼容旧流程；历史加载后经 `AppCompactionStrategyFactory` + `CompactionProvider.CompactAsync` 压缩再注入 `sys.history`——MAF 压缩组件脱离 Agent 管线单独使用）。前端开始节点在 utils 双向转换层固定 question 契约；`AppWorkspace` 为流程应用改渲染顶部 Tabs；新增 `AppWorkflowDebugSection` 内嵌会话式对话组件（复用 `ChatMessageList` + 会话/AG-UI API）。

**Tech Stack:** .NET 10 + Microsoft.Agents.AI 1.20.0（Compaction）；React 19 + antd 5 + @ag-ui/client。

---

## 背景事实（已核实）

- `WorkflowAppChatInvoker`（src/app/workflow/MoAI.App.Workflow.Core/Services/WorkflowAppChatInvoker.cs）：`input["query"]=request.Query`（L91-94）；`LoadHistoryAsync` 取全部消息内存过滤后 `TakeLast(20)`（L126-147）。
- `MoAI.App.Workflow.Core.csproj` L15 已引用 `MoAI.AI.Core` → 可直接注入 `AppCompactionStrategyFactory`（[InjectOnScoped]，`BuildAsync(AppAgentConfigEntity?)` 返回 MAF `CompactionStrategy`）。
- 静态压缩入口：`CompactionProvider.CompactAsync(strategy, IReadOnlyList<ChatMessage>, ILogger, ct)`（AppChatFlushService.cs:127 同款用法）；默认参数 `PreserveTurns=6`（滑动窗口）→ 历史按轮次封顶。
- 前端开始节点无独立组件：`NodeForm.tsx` case 'start'（L1145-1155）+ `StartInputsEditor`（L1286-1361）；`utils.ts` `toEditorFormat` L141-154（引擎 outputs → data.inputs）、`fromEditorFormat` L285-296（data.inputs → 引擎 outputs）、`collectUpstreamVariables` L929-936（start 透传 data.inputs 为变量）。
- `AppWorkspace.tsx`：SECTIONS L25，workflow 分支菜单 L102-108，design 分区绕过 Page 外壳 L122-136。
- 流程应用会话/消息已复用 `app_agent_session`/`app_agent_message`，`createAppSession` 放行 Workflow；AG-UI 对话 `/api/agent/{appId}/chat` 已可用（e2e WF-22）。
- i18n：`ui/src/i18n/locales/{zh-CN,en-US}/common.json` 的 `workflowDesigner` 块在文件尾部（约 1725 行起），`appWorkspace` 块约 1488 行起。

---

### Task 1: 后端 — WorkflowAppChatInvoker：question 契约 + MAF 历史压缩

**Files:**
- Modify: `Directory.Packages.props`（加 `Microsoft.Agents.AI` 1.20.0 集中版本）
- Modify: `src/app/workflow/MoAI.App.Workflow.Core/MoAI.App.Workflow.Core.csproj`（显式引用 Microsoft.Agents.AI）
- Modify: `src/app/workflow/MoAI.App.Workflow.Core/Services/WorkflowAppChatInvoker.cs`

- [ ] **Step 1.1** Directory.Packages.props 第 34 行后加：
```xml
    <PackageVersion Include="Microsoft.Agents.AI" Version="1.20.0" />
```

- [ ] **Step 1.2** Workflow.Core.csproj 第一个 ItemGroup 加：
```xml
    <PackageReference Include="Microsoft.Agents.AI" />
```

- [ ] **Step 1.3** 重写 `WorkflowAppChatInvoker`：

```csharp
using System.Text.Json.Nodes;
using Maomi;
using Microsoft.Agents.AI.Compaction;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using MoAI.AI.Services;
using MoAI.App.Workflow.Definition;
using MoAI.App.Workflow.Instance;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;

namespace MoAI.App.Workflow.Services;

/// <summary>
/// <inheritdoc cref="IWorkflowAppChatInvoker"/>
/// 一轮对话 = 一次已发布流程执行：
/// 1. 校验应用为流程应用且已发布；
/// 2. 用户消息作为启动参数 <c>question</c> 驱动流程（开始节点固定 question 字段；同时镜像 <c>query</c> 兼容旧发布流程）；
/// 3. 注入 sys.* 对话上下文（userId/appId/conversationId/messageId/history），
///    history 单独走 MAF 压缩组件（CompactionProvider + 应用 execution_settings 策略），不依赖 Agent 管线；
/// 4. 从结束节点输出提取回复文本（reply/answer/output/text/result 优先，单一字符串属性次之，否则整体序列化）.
/// </summary>
[InjectOnScoped]
public class WorkflowAppChatInvoker : IWorkflowAppChatInvoker
{
    /// <summary>压缩前从库内加载的最大消息条数（限制内存与压缩耗时）.</summary>
    private const int HistoryLoadLimit = 200;

    /// <summary>压缩失败退回时的兜底条数（与旧版行为一致）.</summary>
    private const int HistoryFallbackLimit = 20;

    private static readonly string[] ReplyKeys = ["reply", "answer", "output", "text", "result"];

    private readonly DatabaseContext _databaseContext;
    private readonly WorkflowEngine _workflowEngine;
    private readonly Stores.DatabaseWorkflowDefinitionStore _definitionStore;
    private readonly WorkflowExecutionContext _executionContext;
    private readonly AppCompactionStrategyFactory _compactionStrategyFactory;
    private readonly ILogger<WorkflowAppChatInvoker> _logger;

    public WorkflowAppChatInvoker(
        DatabaseContext databaseContext,
        WorkflowEngine workflowEngine,
        Stores.DatabaseWorkflowDefinitionStore definitionStore,
        WorkflowExecutionContext executionContext,
        AppCompactionStrategyFactory compactionStrategyFactory,
        ILogger<WorkflowAppChatInvoker> logger)
    {
        _databaseContext = databaseContext;
        _workflowEngine = workflowEngine;
        _definitionStore = definitionStore;
        _executionContext = executionContext;
        _compactionStrategyFactory = compactionStrategyFactory;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<WorkflowAppChatResult> InvokeAsync(WorkflowAppChatRequest request, CancellationToken cancellationToken = default)
    {
        var app = await _databaseContext.Apps.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.AppId, cancellationToken).ConfigureAwait(false);
        if (app == null || app.IsExternal || app.AppType != (int)AppType.Workflow)
        {
            throw new BusinessException("应用不存在或不是流程应用.") { StatusCode = 404 };
        }

        if (app.IsDisable)
        {
            throw new BusinessException("应用已被禁用.") { StatusCode = 403 };
        }

        if (app.PublishStatus != 1)
        {
            throw new BusinessException("流程尚未发布，无法对话.") { StatusCode = 400 };
        }

        var config = await _definitionStore.FindConfigEntityAsync(app.Id, cancellationToken).ConfigureAwait(false);
        if (config?.PublishedDefinition == null)
        {
            throw new BusinessException("流程尚未发布，无法对话.") { StatusCode = 400 };
        }

        _executionContext.TeamId = app.TeamId;
        _executionContext.ConfigId = config.Id;
        _executionContext.IsDebug = false;

        var systemContext = new JsonObject
        {
            ["userId"] = request.UserId.ToString(),
            ["appId"] = request.AppId.ToString(),
            ["conversationId"] = request.SessionId.ToString(),
            ["messageId"] = Guid.CreateVersion7().ToString("N"),
            ["history"] = await LoadHistoryAsync(request.AppId, request.SessionId, cancellationToken).ConfigureAwait(false),
        };

        var input = new JsonObject
        {
            // 开始节点固定 question 字段；query 镜像兼容旧发布流程（绑定 start.query 的历史编排）
            ["question"] = request.Query,
            ["query"] = request.Query,
        };

        var instance = await _workflowEngine.StartAsync(
            app.Id.ToString(),
            input,
            systemVariables: null,
            systemContext,
            Guid.CreateVersion7().ToString("N"),
            cancellationToken).ConfigureAwait(false);

        if (instance.Status != InstanceStatus.Completed)
        {
            return new WorkflowAppChatResult
            {
                Success = false,
                InstanceId = instance.Id,
                ErrorMessage = instance.ErrorMessage ?? "流程执行未完成.",
            };
        }

        return new WorkflowAppChatResult
        {
            Success = true,
            InstanceId = instance.Id,
            Reply = ExtractReply(instance.Output) ?? string.Empty,
        };
    }

    /// <summary>
    /// 读取会话历史并压缩：流程应用不走 Agent 管线（无 AIContextProviders），
    /// 单独使用 MAF 压缩组件按应用 execution_settings 组装策略压缩后注入 sys.history；
    /// 压缩失败退回最近 <see cref="HistoryFallbackLimit"/> 条原文。形状为 [{role, content}].
    /// </summary>
    private async Task<JsonArray> LoadHistoryAsync(Guid appId, Guid sessionId, CancellationToken cancellationToken)
    {
        var records = await _databaseContext.AppAgentMessages.AsNoTracking()
            .Where(x => x.SessionId == sessionId)
            .OrderByDescending(x => x.Seq)
            .Select(x => new { x.Role, x.Content })
            .Take(HistoryLoadLimit)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        records.Reverse();

        var messages = records
            .Where(x => (x.Role == "user" || x.Role == "assistant") && !string.IsNullOrWhiteSpace(x.Content))
            .Select(x => new ChatMessage(x.Role == "user" ? ChatRole.User : ChatRole.Assistant, x.Content))
            .ToList();
        if (messages.Count == 0)
        {
            return [];
        }

        try
        {
            var agentConfig = await _databaseContext.AppAgentConfigs.AsNoTracking()
                .FirstOrDefaultAsync(x => x.AppId == appId, cancellationToken).ConfigureAwait(false);
            var strategy = await _compactionStrategyFactory.BuildAsync(agentConfig, cancellationToken).ConfigureAwait(false);
            var compacted = await CompactionProvider.CompactAsync(strategy, messages, _logger, cancellationToken).ConfigureAwait(false);

            var history = new JsonArray();
            foreach (var message in compacted)
            {
                var role = message.Role == ChatRole.User ? "user" : "assistant";
                history.Add(new JsonObject { ["role"] = role, ["content"] = message.Text });
            }

            return history;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "流程对话历史压缩失败，退回最近 {Limit} 条原文. SessionId={SessionId}", HistoryFallbackLimit, sessionId);
            var history = new JsonArray();
            foreach (var message in messages.TakeLast(HistoryFallbackLimit))
            {
                var role = message.Role == ChatRole.User ? "user" : "assistant";
                history.Add(new JsonObject { ["role"] = role, ["content"] = message.Text });
            }

            return history;
        }
    }

    /// <summary>
    /// 从结束节点输出提取回复文本：常用字符串字段优先，其次唯一字符串属性，否则整体序列化为 JSON.
    /// </summary>
    private static string? ExtractReply(JsonObject? output)
    {
        if (output == null)
        {
            return null;
        }

        foreach (var key in ReplyKeys)
        {
            if (output.TryGetPropertyValue(key, out var node)
                && node is JsonValue value
                && value.TryGetValue<string>(out var text)
                && !string.IsNullOrWhiteSpace(text))
            {
                return text;
            }
        }

        if (output.Count == 1)
        {
            var only = output.FirstOrDefault();
            return only.Value switch
            {
                JsonValue stringValue when stringValue.TryGetValue<string>(out var text) => text,
                null => null,
                _ => only.Value!.ToJsonString(),
            };
        }

        return output.ToJsonString();
    }
}
```

注意：压缩结果里若出现非 user/assistant 角色（如摘要 system 标记），统一映射为 assistant（MAF 摘要策略产物对流程节点而言就是上下文文本）。

- [ ] **Step 1.4** `WorkflowAppChatClient.cs` 仅更新头部注释（query→question 表述）：
```
/// 把一轮 AG-UI 对话转换为一次流程执行：取最后一条用户消息作为启动参数（question）...
```

- [ ] **Step 1.5** 编译验证：`dotnet build src/MoAI/MoAI.csproj`（0 error）。

---

### Task 2: 前端 — 开始节点固定 question

**Files:**
- Modify: `ui/src/pages/teams/apps/workflow/constants.ts`（start 模板 inputs）
- Modify: `ui/src/pages/teams/apps/workflow/NodeForm.tsx`（case 'start' 固定展示，删 StartInputsEditor）
- Modify: `ui/src/pages/teams/apps/workflow/utils.ts`（toEditorFormat/fromEditorFormat 固定契约）
- Modify: `ui/src/pages/teams/apps/workflow/__tests__/utils.test.ts`（fixture 与断言改 question，新增旧定义收敛用例）

- [ ] **Step 2.1** constants.ts start 模板（L74-84）inputs 改为固定 question：
```ts
    inputs: {
      question: { expressionType: 'run', value: '', required: true, fieldType: 'string' },
    },
```

- [ ] **Step 2.2** NodeForm.tsx：
  - case 'start'（L1145-1155）改为固定只读展示（保留 NodeKeySection）：
```tsx
      case 'start':
        return (
          <>
            <NodeKeySection data={nodeData} nodeId={nodeId} nodeType={nodeType} onUpdateData={patch} />
            <div className="wf-node-sec">
              <SectionTitle text={t('workflowDesigner.userQuestion')} />
              <div className="wf-b-row">
                <div className="wf-b-row-top">
                  <Input size="small" value="question" disabled className="wf-field-name" />
                  <span className="wf-out-type">string</span>
                </div>
                <div className="wf-config-hint">{t('workflowDesigner.startQuestionHint')}</div>
              </div>
            </div>
          </>
        )
```
  - 删除 `StartInputsEditor` 整个函数（L1284-1361）及其区块注释；若 `FIELD_TYPE_OPTIONS`/`DeleteOutlined`/`Popconfirm`/`PlusOutlined` 因此变为未使用 import，一并清理（BindingsEditor 还在用则保留）。
  - 插值 placeholder `{start.query}` → `{start.question}`（L120、L747、L1060）。

- [ ] **Step 2.3** utils.ts：
  - `toEditorFormat`（L141-154）start 分支改为固定：
```ts
      // 开始节点：固定 question 启动参数（旧定义的自定义参数收敛为 question）
      const startInputs =
        n.type === 'start'
          ? { question: { expressionType: 'run', value: '', required: true, fieldType: 'string' } as FieldBinding }
          : undefined
```
  - `fromEditorFormat`（L285-296）start 分支改为固定输出：
```ts
      // 开始节点：固定唯一启动参数 question（用户问题，流程对话时由服务端注入）
      const outputs =
        type === 'start'
          ? [{ name: 'question', fieldType: 'string', isRequired: true }]
          : sanitizeOutputs(n.data?.outputs)
```
  - `collectUpstreamVariables`（L929-936）start 分支不变（读 data.inputs → 自动产出 `start.question`），仅更新注释为「开始节点固定 question 输出」。
  - 顶部文件注释无需改。

- [ ] **Step 2.4** `__tests__/utils.test.ts`：
  - 所有 `start.query` → `start.question`；start 节点 data.inputs 的 `query` 键 → `question`；L201-202 断言改：
```ts
    expect(startDef.outputs).toHaveLength(1)
    expect(startDef.outputs[0]).toMatchObject({ name: 'question', fieldType: 'string', isRequired: true })
```
  - L190-193 注释与取值 `start.data!.inputs!.question!`。
  - 新增用例：`toEditorFormat 收敛旧自定义开始参数为固定 question`：
```ts
  it('toEditorFormat：旧定义自定义开始参数收敛为固定 question', () => {
    const definition = { /* 引擎定义：start.outputs=[query(string,required), foo(string)] */ }
    const editor = toEditorFormat(definition as unknown as WorkflowDefinition)
    const start = editor.nodes.find((n) => n.type === 'start')!
    expect(Object.keys(start.data!.inputs!)).toEqual(['question'])
  })
  it('fromEditorFormat：开始节点忽略画布数据固定输出 question', () => {
    const editor = createDefaultEditorData()
    const def = fromEditorFormat(editor, 't')
    const start = def.nodes.find((n) => n.type === 'start')!
    expect(start.inputs).toEqual({})
    expect(start.outputs).toEqual([{ name: 'question', fieldType: 'string', isRequired: true }])
  })
```
（fixture 具体形状按文件内现有构造方式补全。）

- [ ] **Step 2.5** `npx vitest run src/pages/teams/apps/workflow/__tests__/utils.test.ts`（或 `npm run test -- utils`）通过。

---

### Task 3: 前端 — AppWorkspace 流程应用顶部 Tab + 配置二级菜单

**Files:**
- Modify: `ui/src/pages/teams/apps/AppWorkspace.tsx`

- [ ] **Step 3.1** SECTIONS 加 `'debug'`：
```ts
const SECTIONS = ['config', 'info', 'design', 'runs', 'logs', 'monitor', 'access', 'channels', 'debug'] as const
```

- [ ] **Step 3.2** 菜单/Tab 逻辑改造（替换 L102-112 分支）：
  - Agent 应用：保持左侧 Menu 不变。
  - 流程应用：不再用左侧 Menu，改为内容区顶部 `Tabs`（antd）：
    - Tab 项：`design 设计`(canManage)、`debug 调试`、`configGroup 配置`（active 当 section ∈ info/logs/monitor/channels，key='info'）、`runs 运行历史`、外部应用追加 `access 访问点`(canManage)。
  - `validSection` 判定改为：流程应用合法 key 集合 = {design, debug, runs, access, info, logs, monitor, channels}（按 canManage/isExternal 过滤），默认 canManage ? 'design' : 'runs'。
  - Tabs `activeKey`：section ∈ {info,logs,monitor,channels} → 'info'（配置组），否则 section。
  - Tab onChange 导航：'info'（配置组）→ `/info`，其余 → `/{key}`。

- [ ] **Step 3.3** 渲染改造：
  - `renderSection` 增加 debug 分支：
```tsx
    if (validSection === 'debug') {
      return <AppWorkflowDebugSection teamId={teamId} appId={appId} detail={detail} canManage={canManage} />
    }
```
  - 流程应用内容区结构：`Tabs`（顶部）+ 配置组激活时二级 `Tabs size="small"`（信息/日志/监控/外部渠道，channels 对外部应用隐藏，logs/monitor 仅 canManage）+ 当前 section 组件（复用既有 AppInfoSection/AppLogsSection/AppMonitorSection/AppChannelsSection 分支）。
  - Agent 应用渲染路径完全不变（Sider + Menu）。

- [ ] **Step 3.4** design 分区绕过外壳逻辑不动（L122-136 保持）。

---

### Task 4: 前端 — AppWorkflowDebugSection 对话组件（带会话历史）

**Files:**
- Create: `ui/src/pages/teams/apps/AppWorkflowDebugSection.tsx`

- [ ] **Step 4.1** 新建组件（复用 ChatMessageList + 会话/AG-UI API，样式走 antd 组件 + design token，无自定义色值）：

```tsx
import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { DeleteOutlined, PlusOutlined, SendOutlined, StopOutlined } from '@ant-design/icons'
import { Alert, Button, Input, Popconfirm, Spin } from 'antd'
import type { TextAreaRef } from 'antd/es/input/TextArea'
import { useTranslation } from 'react-i18next'
import type { HttpAgent } from '@ag-ui/client'
import { feedback } from '@/design-system'
import { spacing } from '@/design-system/theme'
import { useAppStore } from '@/store/app'
import { resolveStorageUrl } from '@/utils/storage'
import { formatDateTime } from '@/utils/datetime'
import {
  createAppSession,
  deleteAppSession,
  getAppSessionMessages,
  getAppSessions,
  type AppSessionItem,
} from '@/api/app'
import { abortAppChat, createAppChatAgent, runAppChat } from '@/api/agentChat'
import { ChatMessageList, type DisplayMessage } from './chat/ChatMessageList'
import type { AppDetail } from './AppConfigSection'

const OPENING_MESSAGE_ID = 'opening-statement'

/**
 * 流程应用「调试」分区：左侧会话历史列表 + 右侧流式对话，
 * 一轮消息 = 一次已发布流程执行（question 注入开始节点，sys.history 由服务端压缩注入）。
 */
export function AppWorkflowDebugSection({
  teamId: _teamId,
  appId,
  detail,
  canManage: _canManage,
}: {
  teamId: number
  appId: string
  detail: AppDetail | null
  canManage: boolean
}) {
  ...（发送/选择会话/新建/删除/停止/复制逻辑与 AppChat 同构，去掉专家与用户设置侧栏；
    未发布时顶部 Alert 提示；开场白仅本地展示；
    容器 flex 两栏：左 232px 会话列表，右 ChatMessageList + composer，高度 calc(100vh - 320px)）
}
```
实现要点（完整代码在执行时按 AppChat.tsx 对应逻辑落盘）：
- `send()`：无会话先 `createAppSession(appId)`（Member 未发布会由后端 400，全局中间件提示）；`createAppChatAgent(appId, sessionId)` + `runAppChat` onDelta/onError；结束后 `loadSessions()`。
- 卸载时 `abortAppChat`。
- 消息加载过滤规则与 AppChat.selectSession 相同。
- i18n 全部复用 `appChat.*` 与 `appWorkspace.debugUnpublishedHint`（新增）。

- [ ] **Step 4.2** AppWorkspace import 并接入 debug 分支（Task 3 已预留）。

---

### Task 5: i18n（zh-CN 与 en-US 同步）

**Files:**
- Modify: `ui/src/i18n/locales/zh-CN/common.json`
- Modify: `ui/src/i18n/locales/en-US/common.json`

- [ ] **Step 5.1** `workflowDesigner` 块：
  - 新增 `userQuestion`：用户问题 / User Question
  - 新增 `startQuestionHint`：「开始节点固定接收用户问题 question：流程对话时自动传入，调试运行时在启动参数中填写。」/ "The start node receives a fixed `question` field (the user's question). It is injected automatically in conversations and provided via startup parameters in debug runs."
  - 删除已死的 `startInputsHint`（grep 确认无引用后）。
- [ ] **Step 5.2** `appWorkspace` 块：
  - 新增 `menuDebug`：调试 / Debug
  - 新增 `debugUnpublishedHint`：流程发布后才能对话，请先在「设计」中发布。/ Publish the workflow before chatting.
  - 其余 Tab 标签复用 `menuDesign/menuRuns/menuConfig/menuInfo/menuLogs/menuMonitor/menuChannels`。

---

### Task 6: E2E — workflow-e2e.mjs

**Files:**
- Modify: `local-dev/workflow-e2e.mjs`

- [ ] **Step 6.1** 契约迁移：文件内所有 `start.query`、`inputs: { query: ... }`（start 的启动参数/绑定/插值）→ `question`：
  - `buildChatDefinition`：start.outputs `[query]` → `[question]`；end 插值 `{start.query}` → `{start.question}`。
  - 分类/知识库等绑定 value `start.query` → `start.question`；调试 `inputJson: {"query":...}` → `{"question":...}`。
- [ ] **Step 6.2** 新增场景（沿用既有 SSE/断言工具，编号顺延现有最大号）：
  - **WF-23 流程对话 question 契约**：发布定义（JS 节点读 `nodes.start.question` 与 `nodes.start.query` 拼进 reply），对话两轮，断言回复含用户问题文本（question 与镜像 query 同值）。
  - **WF-24 对话历史压缩**：定义 JS 节点输出 `h=sys.history.length`；同会话连发 10 轮；断言末轮 `h >= 2 && h <= 14`（PreserveTurns=6 滑动窗口封顶，值稳定在 ~12）。
- [ ] **Step 6.3** 头部场景计数注释与 `docs/app/bdd.md` 场景号同步。

---

### Task 7: 文档同步（DOC-STANDARD）

**Files:**
- Modify: `docs/app/bdd.md`（新增 @WF-S23/@WF-S24 场景，修订开始节点契约描述）
- Modify: `docs/app/tdd.md`（新映射：utils.test.ts 新用例、e2e WF-23/24）
- Modify: `docs/app/sdd.md`（WorkflowAppChatInvoker 压缩 + question；AppWorkspace Tabs；AppWorkflowDebugSection；开始节点固定 question）
- Modify: `docs/ai/sdd.md`（压缩设计决策补充：流程对话 sys.history 单独走 MAF 压缩组件）
- Modify: `docs/rounds-log.md`（本轮条目）

---

### Task 8: 全量验证

- [ ] `dotnet build src/MoAI/MoAI.csproj` → 0 error
- [ ] `cd ui && npm run typecheck && npm run lint && npm run test`
- [ ] 后端 `dotnet run`（需 postgres/redis/rabbitmq 容器与 MinIO 就绪）→ `node local-dev/workflow-e2e.mjs` 全绿
- [ ] 回归抽查：`node local-dev/skill-userconfig-e2e.mjs`（不动则跳过）

---

## 自查记录

- 需求1（MAF 压缩单独支持流程对话）→ Task 1；需求2（头部 Tab/调试对话/配置二级）→ Task 3/4；需求3（开始节点固定 question）→ Task 1（服务端注入）+ Task 2（设计器契约）。
- 运行历史保留为顶部第 4 个 Tab（用户未提，既有功能不可丢）。
- 旧数据兼容：旧发布流程绑定 `start.query` 由 invoker 镜像键兜底；旧草稿加载即收敛为固定 question。
- 无 git commit（工作区含用户未提交改动）。
