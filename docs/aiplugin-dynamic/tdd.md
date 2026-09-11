# 动态插件（DynamicPlugin）验证映射（TDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 证据：[local-dev/dynamic-plugin-e2e.mjs](../../local-dev/dynamic-plugin-e2e.mjs) ｜ [local-dev/bocha-search-e2e.mjs](../../local-dev/bocha-search-e2e.mjs)
> 规范：[../DOC-STANDARD.md](../DOC-STANDARD.md)。场景复述见 BDD，本文只做编号→验证物映射。

## 后端 E2E

两个脚本分工：`dynamic-plugin-e2e.mjs` 针对**已运行的真实后端**做实例管理与失败路径断言；`bocha-search-e2e.mjs`
自带 BoCha 桩服务并拉起一个独立后端（`MoAI__BoCha__Endpoint` 指向桩），覆盖**成功路径与响应解析**，无需真实 API Key、不消耗额度。

```bash
# 1) 实例管理与失败路径（需后端 :5000 运行中）
node local-dev/dynamic-plugin-e2e.mjs

# 2) 博查成功路径与解析（脚本自建桩服务 + 独立后端 :5199，无需真实 Key）
node local-dev/bocha-search-e2e.mjs
```

| 场景 | 验证物 | 结果（日期） |
|---|---|---|
| @DYN-S1 | local-dev/dynamic-plugin-e2e.mjs（列表出现实例并带模板 key/配置） | PASS（2026-09-11） |
| @DYN-S2 | local-dev/dynamic-plugin-e2e.mjs（未创建时列表不含目标 key） | PASS（2026-09-11） |
| @DYN-S3 | local-dev/dynamic-plugin-e2e.mjs（创建实例 200） | PASS（2026-09-11） |
| @DYN-S4 | local-dev/dynamic-plugin-e2e.mjs（实例 key = 模板 key → 409） | PASS（2026-09-11） |
| @DYN-S5 | local-dev/dynamic-plugin-e2e.mjs（同 key 重复提交走更新，列表仍单行） | PASS（2026-09-11） |
| @DYN-S6 | local-dev/dynamic-plugin-e2e.mjs（大写/数字开头/超长 → 400） | PASS（2026-09-11） |
| @DYN-S7 | local-dev/dynamic-plugin-e2e.mjs（模板不存在 → 404） | PASS（2026-09-11） |
| @DYN-S8 | local-dev/dynamic-plugin-e2e.mjs（改配置后运行使用新 Prefix） | PASS（2026-09-11） |
| @DYN-S9 | local-dev/dynamic-plugin-e2e.mjs（更新后 key 不变、标题变更） | PASS（2026-09-11） |
| @DYN-S10 | local-dev/dynamic-plugin-e2e.mjs（运行返回 Hello MoAI） | PASS（2026-09-11） |
| @DYN-S11 | local-dev/dynamic-plugin-e2e.mjs（运行不存在实例 → 提示不存在） | PASS（2026-09-11） |
| @DYN-S12 | local-dev/dynamic-plugin-e2e.mjs（删除 200 + 列表消失） | PASS（2026-09-11） |
| @DYN-S13 | local-dev/dynamic-plugin-e2e.mjs（重复删除 → 404） | PASS（2026-09-11） |
| @DYN-S14 | local-dev/dynamic-plugin-e2e.mjs（成员查询/新建/删除/运行全 403） | PASS（2026-09-11） |
| @DYN-S15 | local-dev/dynamic-plugin-e2e.mjs（注册表含 dynamic_greet 与 bocha_web_search） | PASS（2026-09-11） |
| @DYN-S16 | local-dev/bocha-search-e2e.mjs（桩服务返回网页/图片 → `S16a~f` 解析 + 参数兜底 + 鉴权头） | PASS（2026-09-11） |
| @DYN-S17 | local-dev/dynamic-plugin-e2e.mjs（空 ApiKey / 空 Query → 可读失败） | PASS（2026-09-11） |
| @DYN-S18 | local-dev/dynamic-plugin-e2e.mjs（无效 Key → `HTTP 401` + 博查响应体） | PASS（2026-09-11） |
| @DYN-S19 | local-dev/dynamic-plugin-e2e.mjs（注册表含 bocha_ai_search，示例与配置类型齐全） | PASS（2026-09-11） |
| @DYN-S20 | local-dev/dynamic-plugin-e2e.mjs（空 ApiKey / 空 Query → 可读失败） | PASS（2026-09-11） |
| @DYN-S21 | local-dev/dynamic-plugin-e2e.mjs（无效 Key → `HTTP 401` + 博查响应体；真实上游） | PASS（2026-09-11） |
| @DYN-S21b | local-dev/bocha-search-e2e.mjs（桩服务 401 → 可读失败；确定性覆盖，不依赖外网） | PASS（2026-09-11） |
| @DYN-S22 | local-dev/bocha-search-e2e.mjs（`S22a~l` 答案/追问/网页/图片/模态卡解析 + 非流式 + 参数兜底 + Answer=false） | PASS（2026-09-11） |
| @DYN-S23 | local-dev/dynamic-plugin-e2e.mjs（注册表含 feishu_webhook_text，示例与配置类型齐全） | PASS（2026-09-11） |
| @DYN-S24 | local-dev/dynamic-plugin-e2e.mjs（空 WebhookKey / 空 Text / 占位 token → 飞书 19001） | PASS（2026-09-11） |

## 前端测试

| 场景 | 验证物 | 结果（日期） |
|---|---|---|
| 实例列表与模板加载 | ui/src/pages/plugins/__tests__/DynamicPluginPanel.test.tsx | PASS 3/3（2026-09-03） |
| 新建实例弹窗 | ui/src/pages/plugins/__tests__/DynamicPluginPanel.test.tsx | PASS（2026-09-03） |
| 删除实例 | ui/src/pages/plugins/__tests__/DynamicPluginPanel.test.tsx | PASS（2026-09-03） |

> 博查两轮（全网搜索、AI 搜索）**均无前端改动**：新模板由后端注册表自动进入模板下拉与运行抽屉，无需改 `DynamicPluginPanel`，也无需 `npm run syncapi`。

## 已知缺口

- 管理员侧「与其它动态实例 key 重复 → 409」分支实际不可达：`SaveDynamicPluginCommandHandler` 先把同 key 记录判为更新（@DYN-S5），`EnsureInstanceKeyUniqueAsync` 的 DB 判重谓词与 `existing` 查询完全相同，属死代码。跨团队冲突由团队插件链路负责（teamplugin TP-13）。
- `docs/aiplugin-static/` 引用的 `local-dev/static-plugin-e2e.mjs` 仍然缺失（本轮只补齐了动态插件脚本）。
- 插件运行结果的 `dataJson` 是 **PascalCase**（`PluginExecutor` 用 `JsonSerializer.Serialize(obj, type)` 的默认选项，未走 ASP.NET 的 camelCase 约定），与 HTTP 接口的响应风格不一致；消费方需按 PascalCase 读字段。属引擎既有行为，改动会影响 `dynamic_greet` 等已有模板，未在本轮调整。
- 桩服务脚本验证的是「插件 + Refit + 解析」链路对**官方样例报文**的适配；博查真实返回若与文档样例存在差异（新增/改名字段），仍需一次人工走查（见 [sop.md](./sop.md) 博查排障）。

## 构建与回归

```bash
dotnet build src/MoAI/MoAI.csproj                                   # 后端 0 error
cd ui && npm run typecheck && npm run lint && npm run test          # 前端全绿
node local-dev/dynamic-plugin-e2e.mjs                               # 实例管理 + 失败路径（需后端 5000 运行中）
node local-dev/bocha-search-e2e.mjs                                 # 博查成功路径 + 响应解析（自建桩服务，无需真实 Key）
```

## 自检记录

- 2026-09-11（博查 AI 搜索内置模板）
  - `dotnet build src/aiplugin/MoAI.AIPlugin.Dynamic/MoAI.AIPlugin.Dynamic.csproj --no-restore -p:GenerateDependencyFile=false` → **0 error、新文件 0 告警**（集合 DTO 用 `IReadOnlyList<T> { get; init; }`）。
  - `dotnet build src/infra/MoAI.Infra.ExternalHttp/MoAI.Infra.ExternalHttp.csproj --no-restore -p:GenerateDependencyFile=false` → **0 error**（`InfraExternalHttpModule.cs` 0 告警）。
  - 运行态：把 `MoAI.AIPlugin.Dynamic.dll`、`MoAI.Infra.ExternalHttp.dll` 换入宿主产物目录并重启后端后：
    - `node local-dev/dynamic-plugin-e2e.mjs` → **PASS 37 / FAIL 0 / SKIP 2**（新增 @DYN-S19~S21）。
    - `node local-dev/bocha-search-e2e.mjs` → **PASS 22 / FAIL 0**（新脚本，覆盖 @DYN-S16 / @DYN-S21b / @DYN-S22，含响应解析）。
  - 真实上游实测（非桩）：无效 Key 下 AI 搜索返回 `插件执行失败: 博查 AI Search 调用失败（HTTP 401）：{"code":"401","log_id":"10bfe899d9ef3257","message":"Invalid API KEY"}`，证明 `/v1/ai-search` 路径与 `Bearer` 头对真实博查服务有效。
  - ⚠️ 环境注意：本机 shell 设有 `http_proxy/https_proxy`，`curl` 与 .NET `HttpClient` 会经代理，请求行可能变成 absolute-form（`http://host/path`）；桩服务按 `pathname` 匹配，本地探活 curl 建议加 `--noproxy '*'`。
  - ⚠️ 编译证据采集方式修正：`dotnet build ... -t:CoreCompile` **单独执行不可靠**（引用不解析，误报成片 `CS0400/CS0246`）；判 0 error/0 告警请用完整 `dotnet build ... --no-restore -p:GenerateDependencyFile=false`（必要时先 `touch` 源文件强制重编）。上一轮 tdd 中「`-t:CoreCompile` → 0 error」的说法据此更正。
  - 沙箱内 `dotnet restore` 被 NuGet `ConfigurationDefaults` 阻断（同轮次 30/31/35~40），**宿主整体 `dotnet build` 仍待系统终端复验**；本轮改动落在 `MoAI.AIPlugin.Dynamic` 与 `MoAI.Infra.ExternalHttp` 两个子项目，均已单独编译通过并换入宿主运行验证。

- 2026-09-11（博查全网搜索内置模板）
  - `dotnet build src/aiplugin/MoAI.AIPlugin.Dynamic/MoAI.AIPlugin.Dynamic.csproj --no-restore -p:GenerateDependencyFile=false` → **0 error、新文件 0 告警**（集合 DTO 用 `IReadOnlyList<T>` 规避 CA1002）。
  - 运行态：把 `MoAI.AIPlugin.Dynamic.dll` / `MoAI.Infra.ExternalHttp.dll` 换入宿主产物目录并重启后端（:5000）后，`node local-dev/dynamic-plugin-e2e.mjs` → **PASS 30 / FAIL 0 / SKIP 1**。
  - 无效 Key 实测失败信息：`插件执行失败: 博查 Web Search 调用失败（HTTP 401）：{"code":"401","log_id":"...","message":"Invalid API KEY"}`（证明 `IBoChaClient` 注入成功且对外请求真实发出）。
  - 沙箱内 `dotnet restore` 被 NuGet `ConfigurationDefaults` 阻断（同轮次 30/31/35~40），宿主整体 `dotnet build` 待系统终端复验。
