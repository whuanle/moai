# MoAI 开发指南（AGENTS.md）

> 项目入口文档。只讲现状、硬约束、文档索引；规范细节一律以 `docs/`、`ui/docs/` 真源为准，本文不复制全文。
> 动后端前读 [cqrs-conventions.md](./docs/cqrs-conventions.md)，动前端前读 [frontend-conventions.md](./ui/docs/frontend-conventions.md) + [design-system](./ui/docs/design-system/README.md)，改已有模块先读其 `sdd.md`/`bdd.md`，**写任何文档前必读 [DOC-STANDARD.md](./docs/DOC-STANDARD.md)**。

## 项目简介

开源 AI 应用平台：.NET 10 模块化单体（Maomi.Core 模块框架 + EF Core + PostgreSQL/pgvector + Redis + MinIO + RabbitMQ + MediatR）+ React 19 前端。
已落地：认证账号与用户治理、设置、OAuth 连接器、文件存储、后台任务、分类、团队（成员/角色/转让）、团队插件授权、团队变量、知识库（含向量化）、AI 渠道与模型、AI 插件（静态/动态/自定义）、团队应用（Agent/流程应用的创建与基础信息）、提示词（个人/团队/市场上架审批）、技能（平台内置/团队/个人归属维护 + 市场上架审批 + 技能包下载 + 应用默认技能·用户对话内勾选，沙箱加载执行）、应用外部渠道（飞书/钉钉/企业微信机器人接入对话，Agent/流程应用，按第三方登录绑定自动归属站内用户会话）、子系统注册与接入（appid+一次性密钥/token 换取+refresh 轮换/心跳在线/团队授权全员或指定/委托 SSO 登录）+ 应用接入统一表（团队 moai-ac- 与系统级 moai-sa- 同表分域，appid+密钥，重置密钥，token 换取）+ 系统资源协作面 /api/subsys/system/*（组织/用户/团队治理按 scope 分档，供 ERP 等系统推送）、角色组织（全局层级角色树/path 物化路径/用户角色分配/默认角色注册自动赋予，仅管理员）。**团队模型网关已下线**：模型仅供建模应用（对话/工作流/知识库等）内部调用，不再对外提供直连模型网关（后续由独立子系统承接），见 [docs/gateway/sdd.md](./docs/gateway/sdd.md)。进度见 [rounds-log.md](./docs/rounds-log.md)。

## 仓库结构

```
src/MoAI/         组合宿主（Program.cs、MainModule、OpenApiModule）
src/{auth,account,settings,oauthconnect,storage,common,infra,database,hangfire}/   平台底座
src/{classify,team,teamplugin,variable,wiki,app,publication}/                      团队协作
src/{feishu,dingtalk,weixinwork}/                                                       外部渠道（飞书/钉钉/企业微信机器人接入）
src/subsystem/                                                                      子系统接入 + 应用接入统一表 + 系统资源协作面
src/org/                                                                           角色组织（全局层级角色树/path 物化路径/用户角色分配）
src/{aichannel,aimodel,aiplugin,skill}/                                       AI 业务层（团队模型网关已下线，见 docs/gateway/sdd.md）
src/{ai,admin,plugin}/                                                             在建
ui/               前端（React 19 + TS + Vite + antd 5 + zustand + Kiota）
docs/ ui/docs/    规范与领域文档    local-dev/  E2E 脚本    tests/  .NET 单测
```

## 硬约束（违反即返工）

**后端**（[真源](./docs/cqrs-conventions.md)）
- CQRS 三层：`*.Shared`（Command/Query）→ `*.Core`（Handler）→ `*.Api`（Controller），依赖单向
- 请求模型必须实现 `IModelValidator<T>` 并写 `static Validate`
- 角色门禁只在 Controller；目标保护规则（不能动 root/自己/其他 admin）在 Handler
- Handler **禁止注入** `IUserContextProvider`/`UserContext`；需用户维度时 Command 继承 `IUserIdContext`
- 改用户相关数据后必须 `RemoveUserStateAsync` 失效 Redis 用户态
- 密码：RSA(PKCS1) 传输 → 解密校验强度 → `PBKDF2Helper.ToHash` 落库
- 审计属性与软删除由框架注入/过滤，禁止手动赋值或写 `.Where(IsDeleted == 0)`
- 时间用 `DateTimeOffset`；Guid 用 `Guid.CreateVersion7()`；枚举必须带 `JsonPropertyName`
- `BusinessException` 必须显式设 `StatusCode`（否则默认 500）
- DI 用 Maomi 特性（`[InjectOnScoped]`），不手写 `AddScoped`
- 列表 DTO 继承 `AuditsInfo`，用 `IUserInfoFillService.FillAsync` 填充人名

**前端**（[真源](./ui/docs/frontend-conventions.md)）
- 禁止直接 import antd `Table`/`Form` 等被封装组件，一律用 `@/design-system`
- 颜色/间距取 token，禁 `#hex` 硬编码；危险操作必须 `Popconfirm`
- 文案全走 `t()`，zh-CN 与 en-US 同步改
- `src/api/client/` 是 Kiota 生成物**禁手改**；手写封装放 `src/api/*.ts`，页面只调封装层
- Kiota 锁 `1.0.0-preview.93`，勿用 `^` 升级
- `Page` 不重复渲染大标题、不加 `maxWidth`；操作栏左对齐；Modal 一律 `maskClosable={false}`
- 时间用 `formatDateTime()`；提示用 `App.useApp()` 的 message，不用静态 `message`

**前后端对接**（[真源](./docs/api_interface.md)）
- 流程：写后端 Controller → `cd src/MoAI && dotnet run` → `cd ui && npm run syncapi`
- 接口文档由 NSwag 自动生成：`/openapi/v1.json` 与 Scalar 仅 Development 暴露；外部接口 `/openapi/external.json` 与子系统协作面 `/openapi/subsys.json`（/api/subsys/*，机器 token 鉴权）全环境暴露（部署后仅保留这两份，其余 openapi/scalar 路径 404）；勿手写接口文档
- 前后对接务必使用 kiota，禁止自行拼接 http 请求

**文档**（[真源](./docs/DOC-STANDARD.md)）
- 改代码 → 更新 `bdd.md` 场景 → `tdd.md` 补映射并执行 → `sdd.md`/`sop.md` 同步
- 场景编号 `@<缩写>-S<n>` 永久不复用；分层不重复内容，跨层一律链接

## 文档索引

- [docs/README.md](./docs/README.md) — 文档地图（L0）与模块四件套索引
- [docs/gateway/external-api-scope-system.md](./docs/gateway/external-api-scope-system.md) — 团队资源外部接口与范围体系细节总账（scope 位表、端点分档、新增资源组清单、踩坑；模型网关已下线）
- [cqrs-conventions.md](./docs/cqrs-conventions.md) ｜ [api_interface.md](./docs/api_interface.md) ｜ [DOC-STANDARD.md](./docs/DOC-STANDARD.md)
- [aiplugin-authoring.md](./docs/aiplugin-authoring.md) ｜ [settings.md](./docs/settings.md) ｜ [storage-file-layout.md](./docs/storage-file-layout.md)
- [ui/AGENTS.md](./ui/AGENTS.md) — 前端专属入口（动 `ui/` 时读这份）
- [frontend-conventions.md](./ui/docs/frontend-conventions.md) ｜ [design-system](./ui/docs/design-system/README.md)
- [rounds-log.md](./docs/rounds-log.md) — 轮次闭环台账与证据

> ⚠️ `docs/README.md` 模块地图滞后于 `src/`：`aichannel`、`aimodel`、`ai`、`admin`、`plugin` 尚无四件套，改动以源码为准并补文档。

## 本地开发

- 后端：`cd src/MoAI && dotnet run`，默认 **5000**（同监听 5001），取自 `MoAI:Port`；`MAI_FILE=... ASPNETCORE_ENVIRONMENT=Development dotnet run` 可覆盖
- 文档：`http://127.0.0.1:5000/openapi/v1.json` ｜ Scalar：`/scalar/v1`
- 前端：`cd ui && npm run dev`（4000）
- 一体部署：`bash deploy/deploy-compose.sh`（postgres 5432、redis 6379、rabbitmq 5672/15672、**rustfs 9000/9001**、opensandbox-server 18123、moai 8080）；配置以 `.env` 为来源自动生成 `configs/system.json` 后挂载注入。详见 [docs/deployment/docker.md](./docs/deployment/docker.md)。图数据库（Memgraph）不内置，按需接入
- 种子账号：admin / abcd123456（root）
- i18n：`dotnet run --project tool/I18nScanner` 扫描 BusinessException 消息生成 `src/MoAI/i18n/{zh-CN,en-US}.json`（已有翻译保留），后端拦截器按 Accept-Language 翻译返回，见 [tool/I18nScanner/README.md](./tool/I18nScanner/README.md)
- git 需代理：`git -c http.proxy=http://127.0.0.1:7897 -c https.proxy=http://127.0.0.1:7897 fetch`

## 验证命令（提交前全绿）

```bash
dotnet build src/MoAI/MoAI.csproj                 # 0 error
cd ui && npm run typecheck && npm run lint && npm run test
# E2E（需后端运行中）
node local-dev/user-management-e2e.mjs   # UM 37
node local-dev/team-e2e.mjs              # TM 47
node local-dev/wiki-e2e.mjs              # WK 47（CRUD 门禁/统计字段 + 上传大小上限 @WK-S26 + 允许上传文件类型设置：默认回显/收窄 400 带允许清单/恢复放行 @WK-S43 + 文档内嵌图片附件：登记/下载/页码/重提取替换/跨文档共享保护 @WK-S44~S45；桩 docx/pdf 脚本自带）
node local-dev/wiki-workflow-e2e.mjs     # WK 27（默认工作流配置 + 多选批量工作流：切割/生成元数据/向量化三步自由组合与单步执行、错误隔离与参数校验；依赖模型渠道的场景在环境无可用模型时自动跳过）
node local-dev/wiki-recall-e2e.mjs      # WK 47（召回测试：参数校验/团队门禁 + 向量召回/文档范围过滤/相似度阈值 + AI 优化问题与生成回答 + 重排序接入与渠道故障降级 + 相邻片段上下文去重 @WK-S42；内置本地 OpenAI 兼容桩模型，无需真实模型渠道）
node local-dev/wiki-source-e2e.mjs      # WS 66（外部源：权限与校验 + 爬虫真实抓取全链路 + 增量哈希比对 + 父页无变化仍发现新页面 + 强制全量 + 文档分页筛选 + 更新挡板/cron 语义 + 停用后同步 409 + 飞书源假凭证创建兜底 + 删除不删已入库文档；自带本地站点桩，无需外网）
node local-dev/wiki-mcp-e2e.mjs         # WM 33（知识库 MCP 服务器：接入 key 鉴权/wiki_mcp 范围/wikiId 归属门禁 + 三只读工具（知识库列表/文件搜索/向量召回）+ 切片序号与相邻上下文去重 @WM-S7 + 无状态协议行为；内置本地桩 embedding）
node local-dev/wiki-external-e2e.mjs    # WX 52（知识库外部开放接口：上传/提取/切割/AI 切割/向量化/删除/重命名/过滤 + 原文件下载 @WX-08 + 向量召回 recall/list 与读档分档 @WX-09 + 切片元数据生成 @WX-10 + 允许上传文件类型设置与放开 .sh 纯文本兜底提取 @WX-11；内置桩 embedding + 对话元数据桩，无需真实模型渠道）
node local-dev/kg-text2cypher-e2e.mjs    # KT（知识图谱 Text2Cypher：实例绑定校验/schema 自描述/只读守卫/$kgId 隔离/行数截断/接入图；依赖 Memgraph，需含 kg_cypher_query 的新构建）
node local-dev/kg-search-e2e.mjs         # KGS 46（知识图谱图检索：向量化同步/语义检索/绑定校验/工作流节点/召回测试；本地桩渠道，需含 SP-A 的新构建）
node local-dev/kg-external-e2e.mjs       # KX 101（知识图谱外部接口：应用 token/团队隔离/类型节点边 CRUD/批量整批拒绝/kg_read·kg_write 分档/批量导入（类型名引用+业务 key 幂等 upsert+逐条失败报告）/validateOnly 预检/按 key 同步闭环（keys/list+edges/batch-delete）；依赖 Memgraph）
node local-dev/kg-mcp-e2e.mjs            # KGM 35（知识图谱 MCP：kg_mcp 鉴权门禁/工具域隔离（wiki 端点不掺 KG 工具）/图谱列表/schema/节点搜索/向量召回（本地桩 embedding）/协议行为；依赖 Memgraph）
node local-dev/kg-import-e2e.mjs         # KG 20（知识图谱导入：AI 导入桩模型全链路（KG-S26）+ JSON 结构化导入（KG-S29：类型名引用/业务 key upsert/预检/逐条报告/幂等/超限/疑似重复检测）；依赖 Memgraph）
node local-dev/variable-e2e.mjs          # VR 30（变量：{key} SmartFormat 插值 + 私密解密 + 未匹配/JSON 花括号字面保留 + 增删改查权限）
node local-dev/team-timer-smoke.mjs      # TIMER 28（团队自动化：CRUD/门禁 + cron 校验 + Hangfire 下次调度回显 + 立即执行成功落历史 + 失败重试终态 + 停用移除调度 + 删除保留历史 + 列表执行状态 lastExecutionStatus + 执行落会话 userType=timer 管理员日志页可见/本人列表排除 @TIMER-S14；自带桩模型，零真实依赖）
node local-dev/team-plugin-e2e.mjs       # TP 47（团队插件：MCP/OpenAPI 导入刷新删除权限 + 团队变量插值 MCP 桩验证 + 落库保留占位符 + OpenAPI header/query 保存回显 + 系统插件 key 全局保留与授权团队可用）
node local-dev/app-e2e.mjs               # AP 217（含 AP-60 审批策略 a~q：六类绑定资源白名单校验/自动放通/知识库检索多源全量判定 + AP-61 知识库工具链路：search_knowledge_base 片段序号/上下文 + get_knowledge_base_chunk 按序号补取片段 + AP-97 版本号只在发布/上架时设置且只能递增不能回退 + AP-98/99 会话上下文用量查询：门禁/零值回显/对话后热态累计与生效模型回显/缓存命中捕获与使用分析聚合，AP-99d 含约 120s 审计落库轮询）
node local-dev/app-usecount-e2e.mjs      # AU 13（应用使用次数计数：会话创建推 Redis 计数每分钟批量落库 + 删除会话不回退 + 累计/存活双口径 + 调试会话不计入；含约 75s 计数周期等待）
node local-dev/chat-attachment-e2e.mjs   # CA 12（对话附件：pre_upload_chat_file 直传 + chat-attachment/extract 提取 + 白名单/越权防护）
node local-dev/sandbox-limits-e2e.mjs    # SB 21（沙箱上限：系统设置三项 + 格式校验 + 应用配置强校验/未启用放行/回读）
node local-dev/settings-logo-e2e.mjs    # SET 28（网站 Logo：root 上传/恢复默认 + 匿名 serverinfo 暴露 logoPath + 门禁与伪造 objectKey 防护；网站名称：root 保存/超长 400/清空回退默认；登录页背景图：上传/生效/恢复 + serverinfo 暴露 loginBackgroundPath + 门禁与伪造防护）
node local-dev/settings-smtp-e2e.mjs     # SET 34（SMTP 邮件服务：八项保存回读/大小写归一/格式校验/门禁/未启用与未配置防护 + 测试邮件发送与 AUTH 认证全链路；内置本地 SMTP 桩，零真实依赖）
node local-dev/auth-email-code-e2e.mjs   # AUTH 34（注册/找回密码邮箱验证码：serverinfo 双开关回显/未启用 SMTP 拒绝/发码 60s 冷却/验证码一次性与错 5 次作废/默认与自定义模板 SmartFormat 插值/重置密码旧号失效全链路 + 验证码模板设置保存校验；内置本地 SMTP 桩，零真实依赖）
node local-dev/workflow-e2e.mjs          # WF 130（流程应用：草稿/发布/调试执行/条件分支与脚本/多条件/知识库检索/问题分类/HTTP 请求节点/运行历史/系统设置·开场白/发布应用对话与 sys 系统变量/开始节点固定 question 契约与旧编排 query 镜像/Agent 应用节点与循环嵌套防护/核心节点不变量/对话实时过程：CUSTOM 节点事件 + AI 正文流式/图片理解节点：桩视觉模型多模态全链路·非视觉模型拒绝·来源防护；内置桩视觉模型零依赖，存量依赖真实模型的用例在环境无模型时失败属预期）
node local-dev/publication-e2e.mjs       # PB 49（上架审核：申请/审批/撤回，is_public 审批制；上架自动取最新团队发布版本，携带版本需递增）
node local-dev/prompt-e2e.mjs            # PT 46（提示词：个人/团队 CRUD + 上架审批 + 市场 + 编辑器/头像）
node local-dev/skill-userconfig-e2e.mjs  # SKL 25（技能三级归属权限 + 应用默认技能：管理员配置默认技能、用户技能勾选仅限默认范围、专家按个人/团队可用范围校验）
node local-dev/skill-usecount-e2e.mjs    # SKC 8（技能使用计数：装载工具调用自增 use_count + options/userconfig 双接口回显；本地桩模型）
node local-dev/skill-market-e2e.mjs      # SM 28（技能市场：市场/详情/下载可见性 + 上架审批 + 删除联动）
node local-dev/team-apikey-scope-e2e.mjs  # TA 24（应用接入 key 功能范围：范围回显/非法代码 400/知识库读写范围/应用对话门禁/兼容/刷新吊销/key 直连知识库/密钥兑换语义：缺密钥 401、错密钥 401、重置后旧密钥失效新密钥可换 @TA-40~42 + 模型网关下线断言；团队接入 key 已下线）
node local-dev/ai-channel-auth-e2e.mjs    # AC 23（渠道级批量授权：授权视图统计+并集回显/全量替换所有私有模型/公开模型不受影响/模型列表 authorizedTeamNames 回显/团队可用性联动（/ai/model/available）/清空/admin 门禁/团队渠道 400）
node local-dev/team-aichannel-e2e.mjs     # TC 35（团队自有模型渠道：门禁/渠道模型 CRUD/同步导入桩/聚合可用模型 isTeamChannel 标记（/ai/model/available）/模型网关下线断言/团队隔离/额度端点下线；内置 OpenAI 兼容桩，零真实依赖）
node local-dev/subsystem-e2e.mjs          # SUB 53（子系统注册与接入+委托 SSO：root 门禁/appid+一次性密钥（PBKDF2 哈希落库）/token 换取+机器 token refresh 轮换 @SUB-S45/心跳在线 60s TTL/停用即拒/密钥重置/团队授权全员与指定/删除释放 appid + 委托码签发-兑换-刷新全链路与门禁；零外部依赖）
node local-dev/system-access-app-e2e.mjs http://127.0.0.1:5000  # SAA 61（系统应用接入统一表：管理 CRUD+重置密钥/appid+密钥与 access_token 双轨鉴权 @SAA-S17~S29/refresh 轮换/停用双轨 403/scope 分档 @SAA-S30~S33/系统资源接口：用户 upsert 幂等推送+禁用+root 保护 @SAA-S34~S42、组织角色树与用户角色分配 @SAA-S43~S49、团队与成员治理 @SAA-S50~S56；零外部依赖，需后端运行中）
node local-dev/org-e2e.mjs                # ORG 38（角色组织：管理员门禁/角色树创建与 path 物化路径计算/同父重名 409/子树整体迁移重写 path/防环/删除保护与级联解绑/用户角色分配·去重·整体覆盖 + 默认角色：设置·清空·注册自动获得·删除联动；零外部依赖）
node local-dev/aimodel-sync-e2e.mjs       # AM 14（模型同步挑选导入：sync 只探测不落库 + 候选项 recognized/exists/modelKind + import 默认停用 + 再 sync exists 标记；内置 /models 桩服务，零真实依赖）
node local-dev/model-catalog-e2e.mjs      # MC 16（模型目录在线同步：catalog/sync 门禁 401/403 + configs/models.json 优先内置目录且按时间戳自动重载 + configs 损坏回退不崩 + 真实下载 opencode 目录覆盖 configs 立即生效；下载场景需外网或 HTTPS_PROXY 可达 models.opencode.ai，网络不通时该场景失败属预期）
node local-dev/external-app-e2e.mjs       # EA 75（外部应用接入：三类外部 token/会话对话/访问点/沙箱技能限制/key 直连团队资源与直连会话/悬浮组件 tokenProvider·onRequest 拦截器与全局 mount + 模型网关下线断言）
node local-dev/app-acp-e2e.mjs            # ACP 21（应用 ACP：app_acp scope 门禁 + JSON-RPC initialize/session/* + Agent/Workflow 应用 ACP 对话 + cancel 中断 + 范围缓存立即性；内置桩模型与确定性流程编排，零 SKIP）
node local-dev/security-strategies-e2e.mjs  # SEC 23（应用安全五策略：输入检查点/工具参数预检拦截/回复哈希/事件审计/模型回复缓冲拦截/注入启发式；内置回显桩模型，零真实依赖）
node local-dev/app-a2a-e2e.mjs          # A2A 20（应用 A2A 协议：app_a2a 门禁/Agent Card/message-send 与 stream（SSE 事件序列）/tasks-get·cancel/会话续聊与落库/用户 token/范围立即性）
node local-dev/subagent-e2e.mjs          # SA 10（子代理协作：spawn_subagent 委派全链路 + 深度固定 1 桩捕获断言 + 未开启不注册；本地桩模型）
node local-dev/agent-app-tool-e2e.mjs    # AP 21（Agent 应用绑定为工具：绑定校验/回显/null 不清空/互绑与传递环 400/含自身节点流程放行（流程侧守卫负责）/agent__ 无头单轮全链路按发布快照/history 透传/解绑下线；本地桩模型，零 SKIP）
node local-dev/agent-memory-e2e.mjs      # MEM 36（长期记忆：平台未开启时应用开启 400 + 工具显式写入 + flush→MQ 两段式自动提取 + 每轮注入 + 用户隔离/模式 + 删除清空 + 压缩折叠视图 + 统一模型锁定（有任意记忆换平台模型/维度 400、应用关闭重开不受影响、root 清空全部后解锁换模型恢复写入）；模型/维度统一系统设置、应用配置剥离模型字段；本地桩 embedding+chat，零 SKIP，收尾还原 MEMORY_* 设置）
node local-dev/memory-management-e2e.mjs # MEM 54（记忆管理：跨应用总览/应用记忆编辑 + 个人全局记忆 CRUD/用户隔离/应用记忆存为全局 + 三级开关：平台 MEMORY_ENABLED 总开关/管理员 allowGlobalMemory/用户接入开关 + 全局跨应用注入与注入不丢上下文 + memory_save scope=global/forget 全局兜底 + MEMORY_* 统一设置门禁校验与模型锁定（应用配置剥离模型字段）/root 清空全部记忆（应用+全局）解锁；本地桩 embedding+chat，零 SKIP，收尾自清理；中断残留先跑 memory-global-reset.mjs）
node local-dev/dynamic-plugin-e2e.mjs    # DYN 169（实例管理 + 失败路径 + 内置模板注册 + 系统插件页团队隔离过滤 @DYN-S70 + 插件头像 S43~S45/创建携带 avatarPath S121~S123；真实 Key/真库用例环境门控 SKIP）
node local-dev/bocha-search-e2e.mjs      # DYN 22（博查成功路径与响应解析，自建桩服务，无需真实 Key）
node local-dev/jina-search-e2e.mjs       # DYN 18（Jina 搜索/网页阅读成功路径与解析，自建桩服务按请求体 q/url 区分端点，无需真实 Key）
node local-dev/yahoo-finance-e2e.mjs     # DYN 29（雅虎财经三模式：批量报价+走势/财经新闻/代码搜索，自建数据桩 v8 chart+v1 search，无需真实外网；数据源地址在实例配置 BaseUrl，可指向镜像）
node local-dev/metaso-search-e2e.mjs     # DYN 21（秘塔搜索工具集：六范围搜索+网页阅读 markdown/json 双形态，自建桩服务；前置 dotnet build -o .builds/metaso）
node local-dev/hot-board-e2e.mjs         # DYN 29（新闻热榜：16 平台热榜聚合（36氪/知乎/微博/掘金/头条等）成功路径+解析细节+Limit 截断+失败隔离，自建 16 域名数据桩走 Gateway 网关改写；前置 dotnet build -o .builds/hotboard）
node local-dev/email-plugins-e2e.mjs     # DYN 37（邮件双模板：SMTP 发送（多收件人/HTML/Base64 附件/认证失败归一）+ IMAP 读取（recent/中文搜索/未读联动/detail/folders），自建 SMTP+IMAP 双协议桩；前置 dotnet build src/MoAI/MoAI.csproj -o .builds/email）
node local-dev/paddleocr-e2e.mjs         # DYN 36~42（PaddleOCR 三模板成功路径 + 响应解析，自建桩服务，无需真实 PaddleOCR）
node local-dev/ops-p1-plugins-e2e.mjs    # DYN 39（P1 运维插件：http_probe 拨测 http/tcp/dns+内网防护 / zabbix_query 双鉴权+主机富化 / redis_query 六模式只读诊断；自建 HTTP+RESP2 双桩，无需真实运维系统；前置 dotnet build -o .builds/ops-p1）
node local-dev/ops-p2-plugins-e2e.mjs    # DYN 30+2SKIP（P2 运维插件：ssh_executor 白名单守卫+处置执行 / grafana_query 注解时间线 / sqlserver_query 只读查询；Grafana 桩+守卫用例无条件，SSH 真机/SqlServer 真库按 SSH_E2E_CONNECTION/SQLSERVER_E2E_CONNECTION 门控；前置 dotnet build -o .builds/ops-p2）
node local-dev/ops-p0-plugins-e2e.mjs    # DYN 30（P0 运维插件：alertmanager_query 告警静默 / loki_query LogQL 纳秒归一 / kubernetes_query 资源与日志 / 钉钉企微机器人 HMAC 加签；四桩全链路，无需真实运维系统；前置 dotnet build -o .builds/ops-p0）
node local-dev/observability-plugin-e2e.mjs # DYN 61（观测四插件：Prometheus/ES/Tempo 桩全链路 + clickhouse_query 自由只读 SQL @DYN-S71~S72（摸库摸表/SHOW CREATE/FORMAT JSON meta/三层只读防护）；自建四段路由桩，无需真实观测服务；前置 dotnet build -o .builds/observability）
node local-dev/clickstack-e2e.mjs        # DYN 20（ClickStack 查询：sources 数据源 / search 原始检索 / chart 时间线聚合，走 HyperDX 对外 API（Personal API Access Key，ClickHouse 8123 不公开时的唯一查询面，与 clickhouse_query 分立）；自建三端点桩，无需真实 ClickStack；前置 dotnet build -o .builds/clickstack）
node local-dev/oauth-weixinwork-smoke.mjs  # OCW 17（企业微信第三方登录：连接器 AgentId 校验/回显 + 固定 wwlogin 授权地址 + 登录页 URL 拼接（login_type=CorpApp/appid/agentid）+ 更新同步；假凭证即可，@OC-S26~S28）
node local-dev/feishu-e2e.mjs            # FS 42（飞书应用连接 CRUD + 应用渠道创建即绑定/删除即彻底移除/绑定解绑端点下线 + 编辑可改 AppID + 应用列表回显外部渠道 applink 入口与禁用隐藏 @FS-S29~S30，假凭证即可）
node local-dev/dingtalk-e2e.mjs          # DT 39（钉钉应用连接 CRUD + 应用渠道创建即绑定/删除即彻底移除/编辑可改 AppKey/非成员 404 + 应用列表钉钉渠道绑定即露出（AppKey 兜底）@DT-S27，假凭证即可）
node local-dev/weixinwork-e2e.mjs        # WX 54（企业微信自建应用渠道：连接 CRUD/门禁 + 创建即绑定 + 回调 URL 验证与推送全链路（签名/AES 加解密/receiveid 防护）+ 单聊 message/send 与群聊 appchat/send 桩模型回复 + MsgId 去重/会话延续/禁用与未发布不回复 @WX-S1~S27；内置 qyapi 桩与回调加密模拟，零真实依赖）
node local-dev/audit-345.mjs node local-dev/audit-storage.mjs node local-dev/auth-lockout-check.mjs
```

## Skills

分层总入口 [agent-tools/skills/README.md](./agent-tools/skills/README.md)，跨层只能上层调下层。

| 层 | Skill | 职责 |
|---|---|---|
| L1 | `moai-feature` | 全栈新功能编排（后端 → syncapi → 前端 → 验证） |
| L2 | `moai-cqrs-backend` | 后端 CQRS 三层细则 |
| L2 | `moai-frontend-ui` | 前端页面细则（design-system / Kiota / i18n） |
| L3 | `moai-cqrs-review` | 铁律审查与修复五步标准 |

新增 skill 需在 `skills/README.md` 与本文同时登记；skill 只写浓缩铁律与实踩坑，用 REQUIRED REFERENCE 指向 `docs/` 真源。
