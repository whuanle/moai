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
| @DYN-S25 | local-dev/dynamic-plugin-e2e.mjs（注册表含 javascript_executor，配置示例与参数示例齐全） | PASS（2026-09-11） |
| @DYN-S26 | local-dev/dynamic-plugin-e2e.mjs（不同 JS 返回值 → 归一 ResultKind/ResultJson） | PASS（2026-09-11） |
| @DYN-S27 | local-dev/dynamic-plugin-e2e.mjs（空 JavaScriptCode / 缺 run / 语法错误 / 运行时错误 → 可读失败） | PASS（2026-09-11） |
| @DYN-S28 | local-dev/dynamic-plugin-e2e.mjs（Parameters 回显 / null+undefined 归一 / 不抛 500） | PASS（2026-09-11） |
| @DYN-S29 | local-dev/dynamic-plugin-e2e.mjs（注册表含 postgres_query / mysql_query，配置示例含 `ConnectionString`、参数示例含 `Sql`、配置类型齐全） | PASS（2026-09-11） |
| @DYN-S30 | local-dev/dynamic-plugin-e2e.mjs（`S30c` 12 类写操作/多语句被拒 + `S30d` MySQL 同样拒绝 + `S30e` 6 条合法只读放行到连接层） | PASS（2026-09-11） |
| @DYN-S31 | local-dev/dynamic-plugin-e2e.mjs（空 `Sql` / 空 `ConnectionString` → 可读失败） | PASS（2026-09-11） |
| @DYN-S32 | local-dev/dynamic-plugin-e2e.mjs（`PG_E2E_CONNECTION` 指向真实 PG：MaxRows 截断 / 列名与行 / 会话只读 / 同名列去重 / bytea+jsonb+时间 / 空结果集） | PASS（2026-09-11） |
| @DYN-S33 | local-dev/dynamic-plugin-e2e.mjs（`MYSQL_E2E_CONNECTION` 指向真实 MySQL：`SELECT 1` + `SHOW VARIABLES LIKE 'transaction_read_only'`） | SKIP（2026-09-11，本机与同网段无可达 MySQL、Docker 未运行；脚本已按环境变量开关就绪） |
| @DYN-S34 | 人工：插件管理页模板下拉选 `postgres_query` / `mysql_query`，检查配置编辑器与运行抽屉的示例文案 | 待运行（前端无改动，模板由注册表自动进入下拉） |
| @DYN-S36 | local-dev/paddleocr-e2e.mjs（注册表含 paddleocr_ocr / paddleocr_structure_v3 / paddleocr_vl，示例与配置类型齐全） | 待运行 |
| @DYN-S37 | local-dev/paddleocr-e2e.mjs（S37a~f 桩 /ocr → Pages/Text/OcrImage/InputImage 解析 + 鉴权头 + 参数透传） | 待运行 |
| @DYN-S38 | local-dev/paddleocr-e2e.mjs（S38a~f 桩 /layout-parsing StructureV3 → Pages/SealTexts/PrunedResultJson/OutputImages/InputImage + 鉴权头） | 待运行 |
| @DYN-S39 | local-dev/paddleocr-e2e.mjs（S39a~f 桩 /layout-parsing VL → Pages/MarkdownText/MarkdownImages/PrunedResultJson/OutputImages/InputImage + 鉴权头 + visualize=true） | 待运行 |
| @DYN-S40 | local-dev/paddleocr-e2e.mjs（空 ApiUrl → InitAsync 拒 + 恢复后仍可运行） | 待运行 |
| @DYN-S41 | local-dev/paddleocr-e2e.mjs（Token 为空 → 桩 401 → 业务异常带 HTTP 状态码 + 响应体） | 待运行 |
| @DYN-S42 | local-dev/paddleocr-e2e.mjs（桩服务被 /ocr 与 /layout-parsing 命中数） | 待运行 |
| @DYN-S43 | local-dev/dynamic-plugin-e2e.mjs（公开图片直传完成 → 设置头像 200 → 列表回读 avatarPath 一致） | PASS 99/99（2026-09-17） |
| @DYN-S44 | local-dev/dynamic-plugin-e2e.mjs（未登记 objectKey → 404 头像文件不存在或未完成上传） | PASS 99/99（2026-09-17） |
| @DYN-S45 | local-dev/dynamic-plugin-e2e.mjs（匿名 401、普通用户 403） | PASS 99/99（2026-09-17） |

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
node local-dev/paddleocr-e2e.mjs                                    # PaddleOCR 成功路径 + 响应解析（自建桩服务，无需真实 PaddleOCR）
```

## 自检记录

- 2026-09-11（PostgreSQL / MySQL 只读查询内置模板）
  - `dotnet build src/aiplugin/MoAI.AIPlugin.Dynamic/MoAI.AIPlugin.Dynamic.csproj --no-restore -p:GenerateDependencyFile=false` → **0 error、0 告警**（PG 会话设置从字面量拼接改为 `SELECT set_config(...)` 参数化后，`CA2100`/`CA1863` 消除；常量提到类顶部消除 `SA1203`）。
  - **宿主级**：`dotnet build src/MoAI/MoAI.csproj --no-restore` → **0 error、0 告警**（连同并行会话的 PaddleOCR 三模板一起编过，`MoAI.AIPlugin.Dynamic` 不再需要临时排除 props）。
    ⚠️ 沙箱内构建前须补 Windows 系统环境变量 `ProgramData`/`ALLUSERSPROFILE`/`APPDATA`，否则 NuGet 读资产文件报 `Value cannot be null (path1)`；
    ⚠️ **不要用 `-p:GenerateDependencyFile=false` 构建宿主**——SDK 会删掉已有的 `MoAI.deps.json`，`dotnet run --no-build` 随即起不来（叶子项目无此副作用）。
  - 新文件：`SqlReadOnlyGuard.cs`（文本层守卫）、`SqlQueryResult.cs`、`SqlResultReader.cs`、`Models/{Postgres,Mysql}Query{Config,Request,Response}.cs`、`Plugins/{PostgresQueryPlugin,MysqlQueryPlugin}.cs`。
  - `MoAI.AIPlugin.Dynamic.csproj` 加 `PackageReference Include="Npgsql"`（10.0.3）与 `MySqlConnector`（2.5.0），版本中心化管理在 `Directory.Packages.props`。
  - 运行态（2026-09-11）：换入 `MoAI.AIPlugin.Dynamic.dll` 并重启后端（:5000）后，`node local-dev/dynamic-plugin-e2e.mjs`（`PG_E2E_CONNECTION` 指向开发库 `192.168.50.199:5432/moai_v2`）→ **PASS 102 / FAIL 0 / SKIP 3**，其中本次新增 @DYN-S29~S32（34 条断言）全通过：
    - 守卫拒绝 12 类写操作/多语句（含 `WITH` 内嵌 `DELETE`、`SELECT INTO`、`SET` 会话变量、行注释后跟写操作、MySQL `/*! */` 可执行注释）；
    - 守卫放行 6 条合法只读语句——用「连接失败」这一**后续**错误反证守卫未误拦（前置校验先于连接，不依赖目标库可达）；
    - PG 真实库成功路径：`RowCount=3` + `Truncated=true`（MaxRows=3 截断 5 行）、`SHOW default_transaction_read_only` 返回 `on`（服务端兜底生效）、同名列自动 `id_2`、`bytea` → Base64 `AP8=`、`jsonb`/`timestamptz` 可序列化、空结果集零行且仍带列名。
  - @DYN-S33（MySQL）**SKIP**：本机与同网段（`192.168.50.199:3306/3307`）均无可达 MySQL，Docker 守护进程未运行；断言已按环境变量开关写好，提供 `MYSQL_E2E_CONNECTION` 即可启用。
  - `node local-dev/bocha-search-e2e.mjs` → **PASS 22 / FAIL 0**（回归确认，与本次改动无耦合）。
  - ⚠️ 沙箱内 `dotnet restore` 被 NuGet `ConfigurationDefaults` 阻断（同轮次 30/31/35~40）：依赖 `obj/project.assets.json` 已还原，用 `--no-restore -p:GenerateDependencyFile=false` 编译验证。
  - ⚠️ Windows 下宿主产物目录的 dll 被运行中进程锁定：换 dll 前必须 `taskkill` 占用 :5000 的 `MoAI.exe`（PID 取 `netstat -ano | grep ":5000 .*LISTENING"`），否则替换会被占用失败/静默无效。

- 2026-09-11（JavaScript 执行器内置模板）
  - `dotnet build src/aiplugin/MoAI.AIPlugin.Dynamic/MoAI.AIPlugin.Dynamic.csproj --no-restore -p:GenerateDependencyFile=false` → **0 error、0 告警**（新文件 0 告警：`JsResultKind` 用 `[SuppressMessage("Usage", "CA1720")]` 抑制类型名警告，`JavaScriptExecutorResponse.SerializeScalar` 暂未调用，留作内部 helper）。
  - `Plugins/JavaScriptExecutorPlugin.cs` 走 Jint 4.16.2：内存 4MB / 递归 100 / 超时 4s / 最大语句 1000，每次执行独立 `new Engine`；调用 `engine.Invoke(runFunction, parameters)`，返回 `JsValue` 通过 `IsNull/IsUndefined/IsString/IsBoolean/IsNumber/IsArray/IsObject` 分派归一为 `Parameters/ResultKind/ResultJson`。
  - `MoAI.AIPlugin.Dynamic.csproj` 加 `PackageReference Include="Jint"`（中心化管理已在 `Directory.Packages.props`）。
  - Jint 4.16.2 的 `JsValue.IsNullOrUndefined` 与 `JsValue.Invoke` 已从实例方法移除——分别改用 `IsNull/IsUndefined` 拆分判断、`engine.Invoke(JsValue, object[])` 调用（记入 sdd `javascript_executor` 细节 #4）。
  - 运行态：把新 dll 换入宿主产物目录并重启后端（:5000）后，`node local-dev/dynamic-plugin-e2e.mjs` 扩展断言（DYN-S25~S28） → **PASS 102 / FAIL 0 / SKIP 3**（2026-09-11 复验；该数字含后续轮次新增的 SQL 断言）。
  - ⚠️ 复验时修正 @DYN-S27d：脚本未定义 `run` 时此前直接 `engine.Evaluate("run")` 会抛 Jint `ReferenceError`，用户只能看到不可读的 `run is not defined`，与 @DYN-S27 契约「提示必须定义 run(parameter)」不符；改为先 `engine.Evaluate("typeof run === 'function'")` 探测，缺失时返回 `JavaScript 代码必须定义 run(parameter) 函数`（记入 sdd `javascript_executor` 细节 #4）。
  - ⚠️ 复验时修正 E2E 断言自身缺陷（**非实现缺陷**）：@DYN-S26a/S26b/S28a 的请求体在单引号字符串里写成 `\"`，拼出的是**非法 JSON**（报 `'i' is invalid after a value`）；@DYN-S26b/S26c-string/S26c-array 的期望与契约（`ResultJson` 恒为 JSON 文本）相反。已改为 `JSON.stringify` 构造请求 + `safeParse` 解析后比对，避免对 `\u0022` 转义形式写正则。
  - ⚠️ Jint 的 `TimeoutInterval` 与 `MaxStatements` 在轮询点检查，无法保证毫秒级实时取消；调用方取消靠外层 `WaitAsync` 协作生效（见 sdd 细节 #3）。
  - 沙箱内 `dotnet restore` 被 NuGet `ConfigurationDefaults` 阻断（同轮次 30/31/35~40），宿主整体 `dotnet build` 待系统终端复验；本轮改动落在 `MoAI.AIPlugin.Dynamic` 单子项目，已单独编译通过并准备换入宿主运行验证。

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

- 2026-09-11（PaddleOCR 三内置模板）
  - `dotnet build src/aiplugin/MoAI.AIPlugin.Dynamic/MoAI.AIPlugin.Dynamic.csproj --no-restore -p:GenerateDependencyFile=false` → **0 error、0 告警**（共 7 个 Model + 1 个 Authorization + 3 个 Plugin）。
  - 新模型：`PaddleOcrConfig`（共享 `ApiUrl` + `Token`）、`PaddleOcrRequest/Response/Page`、`PaddleStructureV3Request/Response/Page`、`PaddleVlRequest/Response/Page`——`Page` 子类型独立成文件规避 SA1402。
  - 新插件：`PaddleOcrPlugin`（PP-OCRv5，`POST /ocr`）、`PaddleStructureV3Plugin`（PP-StructureV3，`POST /layout-parsing`，特征字段 `useSealRecognition`）、`PaddleVlPlugin`（PaddleOCR-VL，`POST /layout-parsing`，特征字段 `useLayoutDetection`，强制 `Visualize=true`）。
  - 共享辅助：`MoAI.AIPlugin.Dynamic/PaddleOcrAuthorization.cs::Build` 把 `Token` 拼成 PaddleOCR 协议要求的 `token {value}`，空 Token 短路返回空串（避免给未开启鉴权的部署发无意义的 `Authorization: token `）。
  - 命名空间冲突：`PaddleOcrResponse<T>` 在 infra 同时存在于 `MoAI.Infra.Paddleocr`（Refit 客户端用）与 `MoAI.Infra.Paddleocr.Models`（业务模型用）两个命名空间——插件用 `using InfraPaddle = MoAI.Infra.Paddleocr;` 别名指向 Refit 客户端的类型，保持与 `IPaddleocrClient` 一致。
  - 命名冲突：插件自己的 `PaddleOcrRequest` / `PaddleOcrResponse` 与 `MoAI.Infra.Paddleocr.Models.PaddleOcrRequest` 等业务模型同名——通过 `using InfraPaddleModels = MoAI.Infra.Paddleocr.Models;` 别名访问基础设施类型，避免与插件请求/响应模型混淆。
  - BaseAddress 每次执行重设：`IPaddleocrClient` 由 `AddRefitClient` 注册为 transient，每次插件执行由 DI 新建独立 `HttpClient`，并发运行互不干扰——`_client.Client.BaseAddress = new Uri(_config.ApiUrl.Trim())` 仅影响当前实例。
  - 响应契约：每插件按页聚合 `Pages[]`，文本字段按行拼接（OCR `rec_texts`）；StructureV3 的 `SealTexts` 沿用旧 `NativePlugin.PaddleocrStructureV3Plugin.InvokeAsync` 的「取每条印章 `rec_texts[0]`」语义，改为按印章拆为列表；VL 直接吐 `MarkdownText` / `MarkdownImages`；`prunedResult` 一律 `JsonElement.GetRawText()` 回写为 `PrunedResultJson`。
  - 新 E2E：`local-dev/paddleocr-e2e.mjs`（自建桩服务监听 `/ocr` 与 `/layout-parsing`，对未带 `token ` 的 Authorization 一律返 401，用请求体特征字段区分 VL/StructureV3），覆盖 @DYN-S36~S42；沙箱内 `dotnet restore` 被阻断（同上），脚本待系统终端运行。
  - 沙箱内 `dotnet restore` 被 NuGet `ConfigurationDefaults` 阻断（同轮次 30/31/35~40），宿主整体 `dotnet build` 待系统终端复验；本轮改动落在 `MoAI.AIPlugin.Dynamic` 单子项目，已单独编译通过并准备换入宿主运行验证。
