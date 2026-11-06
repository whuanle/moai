# MoAI 开发指南（AGENTS.md）

> 项目入口文档。只讲现状、硬约束、文档索引；规范细节一律以 `docs/`、`ui/docs/` 真源为准，本文不复制全文。
> 动后端前读 [cqrs-conventions.md](./docs/cqrs-conventions.md)，动前端前读 [frontend-conventions.md](./ui/docs/frontend-conventions.md) + [design-system](./ui/docs/design-system/README.md)，改已有模块先读其 `sdd.md`/`bdd.md`，**写任何文档前必读 [DOC-STANDARD.md](./docs/DOC-STANDARD.md)**。

## 项目简介

开源 AI 应用平台：.NET 10 模块化单体（Maomi.Core 模块框架 + EF Core + PostgreSQL/pgvector + Redis + MinIO + RabbitMQ + MediatR）+ React 19 前端。
已落地：认证账号与用户治理（**登录一体化**：账号+密码+图形验证码+邮箱验证码单请求分级一体提交，独立二次验证页与 emailCode 票据退役；图形验证码自绘 SVG 零依赖、`LOGIN_CAPTCHA_MODE` off/always/after_fail 覆盖登录与注册/找回发码三面、与邮箱验证码分级递进（fail≥1 先图形码、fail≥阈值加邮箱码），见 [docs/auth/sdd.md](./docs/auth/sdd.md)）、设置、OAuth 连接器、文件存储、后台任务、分类、团队（协作与资源隔离单元；成员/角色/转让）、团队插件授权、团队变量、知识库（含向量化）、AI 渠道与模型、AI 插件（静态/动态/自定义；**动态插件多工具**：`[AiPluginTool]` 方法级契约一个模板暴露多工具、按 toolKey 独立调试与单工具参数示例接口、37 个存量模板零改动兼容；**插件语义配置**：插件级使用须知/结构化语义条目（常驻注入 + plugin_note 按需披露）/工具级补充说明与调用策略（denyPatterns 正则 block/confirm）拦截 + MCP readOnly/destructive 注解采集，见 [docs/aiplugin-dynamic/sdd.md](./docs/aiplugin-dynamic/sdd.md)）、团队应用（Agent/流程应用的创建与基础信息；**Agent 应用多模型**：`model_id` 语义升级为默认模型 + `model_ids` 可选列表（≤20、含默认、逐个团队可用校验），用户对话端经 `X-Moai-Model-Id` 请求头自由切换（快照外/不可用静默回退默认），会话 `last_model_id` 记录实际模型供用量回显，压缩阈值与摘要模型按本轮实际模型推导，见 docs/app/sdd.md 决策 D50）、提示词（个人/团队/市场上架审批）、技能（平台内置/团队/个人归属维护 + 市场上架审批 + 技能包下载 + 应用默认技能·用户对话内勾选，沙箱加载执行）、**团队智能体（Phase 1 管理面）**（主智能体 + ≤20 个命名子智能体的编排定义资产 `team_agent`：各带系统提示词/偏好模型/插件与知识库依赖声明——依赖双角色=绑定预检清单+运行时工具白名单，本期仅落清单校验（与应用配置绑定同口径）；**系统内置智能体**：规划师 Plan/构建师 Build 定义在代码（`BuiltinTeamAgents`），团队创建时自动种入（幂等，失败不阻断）、存量团队走 seed-builtin 管理端点补种，is_system 不可删除可编辑；子智能体标识预留 `subagent_{name}` 工具名格式；团队页「智能体」分区成员只读 Admin+ 管理；**Phase 2 应用绑定与运行时派发已落地**：Agent 应用配置绑定 `team_agent_ids/default_team_agent_id`（**绑定独立于子代理协作**——绑定即主智能体提示词注入主会话，子智能体派发由协作开关独立控制，D53；弱依赖缺失提示一键添加不阻断）、发布快照内嵌定义（发布后改智能体不影响线上）、对话端 `X-Moai-Team-Agent-Id` 切换（多选 ≥2 才出切换器、单选直接生效、非法头回退默认）、运行时主提示词按「应用提示词→[团队智能体]主提示词→专家提示词」注入（开启协作附子智能体名册引导自动派发，opencode 语义）、子智能体注册为具名 `subagent_{key}` 派发工具替代通用 spawn_subagent（子会话注入子提示词+声明即边界白名单+深度恒 1）、**子智能体过程实时可视化**：派发器旁路通道把子会话过程（正文增量/嵌套工具步骤）经 `moai.subagent` 自定义事件实时注入同一 SSE，前端渲染嵌套活动卡（codex 风格），历史按结论级回放（过程不持久化），见 [docs/app/sdd.md](./docs/app/sdd.md) 决策 D52/D53 与 [docs/team-agent/sdd.md](./docs/team-agent/sdd.md)）、应用外部渠道（飞书/钉钉/企业微信机器人接入对话，Agent/流程应用，按第三方登录绑定自动归属站内用户会话）、**系统级接入合并模型**（子系统与应用接入同表 access_app 同列表同接口：统一 /api/subsys/token 换取 + /api/subsys/** 协作面；子系统=自填 appid+委托 SSO 多回调地址白名单（redirect_uri 指定入口）+团队/组织授权+多实例心跳在线（按实例 ZSET 回显在线实例数）；应用接入=moai-sa- 结构化自校验 appid（Guid+HMAC，公网扫描不打库）+全量资源，资源范围分档已下线）+ 团队级应用接入与统一团队资源 key（**moai-ac- 去范围化**：同表 Level 分域、不再勾选访问范围、key/token 全量访问 /api/teamres；**teamres_key 表一资源一把**：应用开启对外协议接入（`is_acp_a2a_enabled` 单一开关，ACP 与 A2A 同开同关、应用工作台单一「ACP/A2A」分区）、知识库/知识图谱开启 MCP（is_mcp_enabled，各一把）时后端自动生成/复用 key（查库无则写有则复用，未开启不生成）；key 自身无启停状态，启停只由资源功能入口开关控制（关闭即 403 feature_disabled，重开复用原 key）；key 之间不可混用（绑定资源实例，越圈 403）；端点按开关拒绝（403 feature_disabled），资源 key 仅能访问绑定资源实例的端点，管理端 /api/team-resource-key（get-or-create/重置；key 不可删除），key 展示在应用 ACP/A2A 页与库/图 MCP 页（开关+复制端点+key），应用接入页无任何生成入口，key 明文回显仅团队 Admin+ 可见）+ 组织（通讯录层级·授权维度，不隔离资源；全局层级组织树/path 物化路径/用户组织分配，仅管理员；非 OA/ERP 轻量定位，不搞复杂组织管理与权限设计）+ 统一组织授权（org_grant 跨资源表 + IOrgGrantService 领域服务 + Redis 缓存，self/up/down 方向规则把资源定向放通给组织线，管理面 /org/grant/*） + 站内信与 SignalR 事件推送（/hub/moai 单 Hub **只推事件不推数据**：onEvent 负载仅事件名+标识字段，消费方收事件后调 REST 取详情；message 表每收件人一行独立已读态/软删；RabbitMQ message.push 异步推送 + Redis backplane 多实例共享连接；IMessageService 发个人/团队/全员、IEventPushService 通用事件推送，其它模块仅引用 Message.Shared；读侧 REST /api/message/*；前端已接入：`@microsoft/signalr` 客户端（token 实时工厂+自动重连+15s 首连重试，withCredentials=false 规避 CORS）+ 侧边栏左下角未读角标 + 右上角 Notification 带标题悬浮提醒（duration=0 刷新即消失）+ 个人中心二级菜单（消息中心/基本信息/修改密码/两步验证/第三方账号分区导航），见 [docs/message/sdd.md](./docs/message/sdd.md)） + **通知中心**（统一多渠道通知出口：飞书/钉钉/企微机器人 webhook、飞书/钉钉/企微应用机器人（App 凭证独立配置，按 user_oauth_connection 绑定反查平台标识对个人推送：飞书 union_id 优先/钉钉 unionId 换算 userid/企微 userid 直投）、Telegram Bot、Slack/Discord/Microsoft Teams、通用 webhook（自定义头+请求体模板）、SMTP 邮件（渠道自带连接配置；系统设置「邮箱验证」仅绑定渠道 id 与注册/找回密码/登录验证码通知模板 id（留空回退内置模板），系统邮件经绑定渠道发送）、阿里云/腾讯云短信（平台侧已审核模板 Code/Id + 变量参数发送，必须经通知模板触发）、Gotify/ntfy/Bark/Server酱/PushPlus、站内推送（复用站内信，广播可直接勾选）；**SMTP/短信收件人发送时动态指定**（渠道配置只存连接/凭证，不再配置收件人与邮件主题前缀；广播带 emailRecipients/phoneNumbers、试发带 recipients，缺收件人该渠道投递失败留痕；发送记录 recipients 快照列记录实际邮箱/手机号；登录安全告警收件人走设置 LS_NOTIFY_EMAILS）；**通知模板**（notification_template：文本+变量定义 {{key}} 双花括号白名单插值（与云邮件平台提审形态一致）+内容类型 text/html（SMTP 发带样式邮件、其它渠道降级去标签）+**内置验证码邮件种子模板三条（注册/登录/找回密码，HTML，按 code 回退解析、不可删除可编辑，存量库补种 asserts/notification-template.sql）**+阿里云/腾讯云平台模板映射回填，领域服务 NotifyAsync/SendNowAsync 入口与广播经 NotificationTemplateApplier 渲染一次幂等、渲染后清空 TemplateId，业务方可不带模板直发）；`INotificationService` 领域服务屏蔽外部接口格式——业务方只给 Title/Content/Level/Source（可选 TemplateId+变量值）；NotifyAsync=Hangfire 异步投递失败重试 3 次、SendNowAsync=同步；**站内信托底**（TargetUserIds 定向站内用户时站内信先行必达、与渠道解析/投递结果解耦——渠道未选/停用/失败/应用机器人零绑定均不影响，空目标全员型不自动发；渠道试发走领域服务：已保存渠道落发送记录留痕、保存前试发不落）；广播支持渠道多选与站内信组合（可用模板）；每渠道发送记录留痕（notification_log，pending/success/failed+耗时+失败原因）；管理页「通知中心」三 Tab（渠道管理：按类型动态表单+保存前试发；通知模板：变量定义+渲染测试；发送记录：过滤分页），仅管理员；见 [docs/notification/sdd.md](./docs/notification/sdd.md)） + **登录安全风控**（登录日志全量落库 + 六规则风险判定：R1 IP 爆破/R2 撞库喷洒自动封禁、R3 新 IP/R4 异地/R5 疾速旅行、R6 管理员新环境升级；处置=风险事件+站内信固定推送受影响用户+高风险事件推通知中心渠道（设置页渠道多选 + 告警收件人邮箱 LS_NOTIFY_EMAILS，通知内容内置多语言默认中文、经 SMTP 渠道即告警邮件）+IP 自动/手动封禁，Hangfire 异步通知不阻塞登录；IP2Region xdb v3 离线归属地（configs/ip2region.xdb，缺失自动降级）+ ForwardedHeaders 反代穿透（MoAI:ForwardedHeaders 配置）；管理页「日志事件」Tabs（登录日志/风险事件/IP 封禁）+ 侧边栏菜单未读角标（60s 轮询，badge store 通用可扩展）；见 [docs/loginsafety/sdd.md](./docs/loginsafety/sdd.md)）。**团队模型网关已下线**：模型仅供建模应用（对话/工作流/知识库等）内部调用，不再对外提供直连模型网关（后续由独立子系统承接），见 [docs/gateway/sdd.md](./docs/gateway/sdd.md)。进度见 [rounds-log.md](./docs/rounds-log.md)。

## 仓库结构

```
src/MoAI/         组合宿主（Program.cs、MainModule、OpenApiModule）
src/{auth,account,settings,oauthconnect,storage,common,infra,database,hangfire}/   平台底座
src/{classify,team,teamplugin,variable,wiki,app,publication}/                      团队协作
src/teamagent/                                                                     团队智能体（Phase 1 管理面：主智能体 + ≤20 个命名子智能体的编排定义资产，各带提示词/插件与知识库依赖声明（绑定预检清单+运行时白名单双角色）；系统内置规划师/构建师团队创建自动种入（不可删除可编辑）；团队页「智能体」分区成员只读 Admin+ 管理；Phase 2 应用绑定与 subagent_{key} 运行时派发已落地（D52/D53：绑定独立于子代理协作、moai.subagent 过程实时事件），设计真源 docs/app/sdd.md，见 docs/team-agent/sdd.md）
src/{feishu,dingtalk,weixinwork}/                                                       外部渠道（飞书/钉钉/企业微信机器人接入）
src/subsystem/                                                                      子系统接入 + 应用接入统一表 + 系统资源协作面
src/org/                                                                           组织（通讯录层级·授权维度：组织树/path 物化路径/用户组织分配 + 统一组织授权表）
src/message/                                                                        站内信 + SignalR 事件推送（/hub/moai 只推事件不推数据；message 表每收件人一行；RabbitMQ message.push 异步 + Redis backplane 多实例；IMessageService/IEventPushService 领域服务，见 docs/message/sdd.md）
src/notification/                                                                   通知中心（统一多渠道通知出口：INotificationService 领域服务 + 七类渠道发送器策略 + Hangfire 异步投递与发送记录留痕，见 docs/notification/sdd.md）
src/{aichannel,aimodel,aiplugin,skill}/                                       AI 业务层（团队模型网关已下线，见 docs/gateway/sdd.md）
src/statistics/                                                                     资源使用统计（**全站使用量统计唯一入口**：领域服务/轮次收集器 → Redis 原子增减 → 每分钟批量落库 → 写入器写资源自身表 + 统一日表/总量表；粒度按天/按小时管理员可配；覆盖 plugin/app/skill/wiki/ai-model/prompt 六类，见 docs/statistics/sop.md）
src/{ai,admin,plugin}/                                                             在建
ui/               前端（React 19 + TS + Vite + antd 5 + zustand + Kiota）
clients/          对外交付的客户端库（子系统协作面 Kiota 生成 .NET 客户端，见 clients/MoAI.Subsystem.Client/README.md）
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
- **使用量统计一律走统计模块**（[docs/statistics/sop.md](./docs/statistics/sop.md)）：无作用域场景用 `IResourceUsageService.RecordAsync/RemoveAsync`；agent 对话内工具类调用用 `IResourceUsageCollector.Record`（作用域结束自动批量推送）；**禁止直写资源 use_count 字段或自建 Redis 计数**

**前端**（[真源](./ui/docs/frontend-conventions.md)）
- 禁止直接 import antd `Table`/`Form` 等被封装组件，一律用 `@/design-system`
- 颜色/间距取 token，禁 `#hex` 硬编码；危险操作必须 `Popconfirm`
- 文案全走 `t()`，zh-CN 与 en-US 同步改
- `src/api/client/` 是 Kiota 生成物**禁手改**；手写封装放 `src/api/*.ts`，页面只调封装层
- Kiota 锁 `1.0.0-preview.93`，勿用 `^` 升级
- `Page` 不重复渲染大标题、不加 `maxWidth`；操作栏左对齐；Modal 一律 `maskClosable={false}`
- 时间用 `formatDateTime()`；提示用 `App.useApp()` 的 message，不用静态 `message`
- 实体列表表格必有「创建/更新」审计列（`AuditCell` 两行单元格），操作列按钮「图标+文字」（`type="link" size="small"`），禁止纯图标

**前后端对接**（[真源](./docs/api_interface.md)）
- 流程：写后端 Controller → `cd src/MoAI && dotnet run` → `cd ui && npm run syncapi`
- 接口文档由 NSwag 自动生成：`/openapi/v1.json` 与 Scalar 仅 Development 暴露；外部接口 `/openapi/teamres.json` 与子系统协作面 `/openapi/subsys.json`（/api/subsys/*，机器 token 鉴权）全环境暴露（部署后仅保留这两份，其余 openapi/scalar 路径 404）；勿手写接口文档
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
- 子系统 .NET 客户端再生成：`node clients/sync-api.mjs`（后端运行中；只重写 `clients/MoAI.Subsystem.Client/Generated/`，手写包装 DI/配置/token 管理不参与生成），用法见 [clients/MoAI.Subsystem.Client/README.md](./clients/MoAI.Subsystem.Client/README.md)
- 一体部署：`bash deploy/deploy-compose.sh`（postgres 5432、redis 6379、rabbitmq 5672/15672、**rustfs 9000/9001**、opensandbox-server 18123、moai 8080）；配置以 `.env` 为来源自动生成 `configs/system.json` 后挂载注入。详见 [docs/deployment/docker.md](./docs/deployment/docker.md)。图数据库（Memgraph）不内置，按需接入
- 种子账号：admin / abcd123456（root）
- i18n：`dotnet run --project tool/I18nScanner` 扫描 BusinessException 消息生成 `src/MoAI/i18n/{zh-CN,en-US}.json`（已有翻译保留），后端拦截器按 Accept-Language 翻译返回，见 [tool/I18nScanner/README.md](./tool/I18nScanner/README.md)
- git 需代理：`git -c http.proxy=http://127.0.0.1:7897 -c https.proxy=http://127.0.0.1:7897 fetch`

## 验证命令（提交前全绿）

```bash
dotnet build src/MoAI/MoAI.csproj                 # 0 error
dotnet test tests/MoAI.Subsystem.Client.Tests     # 子系统 .NET 客户端库 8 用例（内存桩，无需后端）
cd ui && npm run typecheck && npm run lint && npm run test
# E2E（需后端运行中）
node local-dev/user-management-e2e.mjs   # UM 37
node local-dev/team-e2e.mjs              # TM 47
node local-dev/wiki-e2e.mjs              # WK 47（CRUD 门禁/统计字段 + 上传大小上限 @WK-S26 + 允许上传文件类型设置：默认回显/收窄 400 带允许清单/恢复放行 @WK-S43 + 文档内嵌图片附件：登记/下载/页码/重提取替换/跨文档共享保护 @WK-S44~S45；桩 docx/pdf 脚本自带）
node local-dev/wiki-workflow-e2e.mjs     # WK 27（默认工作流配置 + 多选批量工作流：切割/生成元数据/向量化三步自由组合与单步执行、错误隔离与参数校验；依赖模型渠道的场景在环境无可用模型时自动跳过）
node local-dev/wiki-recall-e2e.mjs      # WK 47（召回测试：参数校验/团队门禁 + 向量召回/文档范围过滤/相似度阈值 + AI 优化问题与生成回答 + 重排序接入与渠道故障降级 + 相邻片段上下文去重 @WK-S42；内置本地 OpenAI 兼容桩模型，无需真实模型渠道）
node local-dev/wiki-source-e2e.mjs      # WS 66（外部源：权限与校验 + 爬虫真实抓取全链路 + 增量哈希比对 + 父页无变化仍发现新页面 + 强制全量 + 文档分页筛选 + 更新挡板/cron 语义 + 停用后同步 409 + 飞书源假凭证创建兜底 + 删除不删已入库文档；自带本地站点桩，无需外网）
node local-dev/wiki-mcp-e2e.mjs         # WM 34（知识库 MCP 服务器：接入 key 鉴权（moai-ac- 全量 / 本库 moai-tr- 资源 key，越库资源 key 403 @WM-S8）/wikiId 归属门禁 + 三只读工具（知识库列表/文件搜索/向量召回）+ 切片序号与相邻上下文去重 @WM-S7 + 无状态协议行为；内置本地桩 embedding）
node local-dev/wiki-external-e2e.mjs    # WX 58（知识库外部开放接口：上传/提取/切割/AI 切割/向量化/删除/重命名/过滤 + 原文件下载 @WX-08 + 向量召回 recall/list @WX-09 + 切片元数据生成 @WX-10 + 允许上传文件类型设置与放开 .sh 纯文本兜底提取 @WX-11 + moai-ac- 去范围化读写全放行 + wiki 资源 key 包围圈（开 MCP 自动生成/复用、本库放行/越库 403/团队级 list 过滤/重置吊销）@WX-12；内置桩 embedding + 对话元数据桩，无需真实模型渠道）
node local-dev/kg-text2cypher-e2e.mjs    # KT（知识图谱 Text2Cypher：实例绑定校验/schema 自描述/只读守卫/$kgId 隔离/行数截断/接入图；依赖 Memgraph，需含 kg_cypher_query 的新构建）
node local-dev/kg-search-e2e.mjs         # KGS 46（知识图谱图检索：向量化同步/语义检索/绑定校验/工作流节点/召回测试；本地桩渠道，需含 SP-A 的新构建）
node local-dev/kg-external-e2e.mjs       # KX 105（知识图谱外部接口：应用 token/团队隔离/类型节点边 CRUD/批量整批拒绝/moai-ac- 去范围化读写全放行/kg 资源 key 包围圈（开 MCP 自动生成/复用）@KX-15/批量导入（类型名引用+业务 key 幂等 upsert+逐条失败报告）/validateOnly 预检/按 key 同步闭环（keys/list+edges/batch-delete）；依赖 Memgraph）
node local-dev/kg-mcp-e2e.mjs            # KGM 36（知识图谱 MCP：接入 key 鉴权（moai-ac- 全量 / 本图 moai-tr- 资源 key，越域资源 key 403 @KGM-S8）/工具域隔离（wiki 端点不掺 KG 工具）/图谱列表/schema/节点搜索/向量召回（本地桩 embedding）/协议行为；依赖 Memgraph）
node local-dev/kg-import-e2e.mjs         # KG 20（知识图谱导入：AI 导入桩模型全链路（KG-S26）+ JSON 结构化导入（KG-S29：类型名引用/业务 key upsert/预检/逐条报告/幂等/超限/疑似重复检测）；依赖 Memgraph）
node local-dev/variable-e2e.mjs          # VR 30（变量：{key} SmartFormat 插值 + 私密解密 + 未匹配/JSON 花括号字面保留 + 增删改查权限）
node local-dev/team-timer-smoke.mjs      # TIMER 28（团队自动化：CRUD/门禁 + cron 校验 + Hangfire 下次调度回显 + 立即执行成功落历史 + 失败重试终态 + 停用移除调度 + 删除保留历史 + 列表执行状态 lastExecutionStatus + 执行落会话 userType=timer 管理员日志页可见/本人列表排除 @TIMER-S14；自带桩模型，零真实依赖）
node local-dev/team-plugin-e2e.mjs       # TP 68（团队插件：MCP/OpenAPI 导入刷新删除权限 + 团队变量插值 MCP 桩验证 + 落库保留占位符 + OpenAPI header/query 保存回显 + 系统插件 key 全局保留与授权团队可用 + 插件使用统计：系统端全团队汇总/TOP 附团队名 + 团队端本团队口径 @TP-S37~S38 + 团队自有插件语义配置 TSM @DYN-S154~S158）
node local-dev/app-e2e.mjs               # AP 244（含 AP-60 审批策略 a~q：六类绑定资源白名单校验/自动放通/知识库检索多源全量判定 + AP-61 知识库工具链路：search_knowledge_base 片段序号/上下文 + get_knowledge_base_chunk 按序号补取片段 + AP-97 版本号只在发布/上架时设置且只能递增不能回退 + AP-98/99 会话上下文用量查询：门禁/零值回显/对话后热态累计与生效模型回显/缓存命中捕获与使用分析聚合，AP-99d 含约 120s 审计落库轮询、AP-99e 消息总数经统计模块落库 @AP-S141、AP-99f 多模型 byModel 默认标记与趋势各模型 token 序列 @AP-S144~S145 + AP-100~104 上下文压缩：默认值 auto 模式与比例常量回显/auto·custom 保存回读/模式枚举与 custom 数值校验 400/auto 忽略历史绝对值 @AP-S115~S118 + AP-105 多模型：可选模型列表保存校验与归一（含默认/≤20/团队可用/老格式归一）+ X-Moai-Model-Id 对话切换生效与快照外·非法值回退默认 + 发布快照携带 modelIds 与 userconfig 生效口径回显 @AP-S138~S140）
node local-dev/app-usecount-e2e.mjs      # AU 23（应用使用次数计数：会话创建推 Redis 计数每分钟批量落库 + 删除会话不回退 + 累计/存活双口径 + 调试会话不计入 + 使用分析趋势端点：默认 30 天升序补 0/days=7 裁剪/0 与 91 参数 400/门禁 401·404，今日使用次数与消息条数回读、modelTokenSeries 空序列 @AP-S143/S145；含约 75s 计数周期等待）
node local-dev/statistics-usage-e2e.mjs  # ST 32（统一资源使用统计：门禁 403/401 + 会话/提示词专家计数入 Redis 缓冲 + 每分钟批量落库统一日表/总量表（upsert 累加）+ names/daily/total 查询过滤分页 + startDate>endDate 400 + 写入器仍写资源自身表 + 统计粒度按天/按小时切换（hour 模式落当前 UTC 小时桶、非法值 400）+ 插件运行触发 plugin-usage + 趋势接口按日聚合/参数校验；含约 150s 批量落库轮询）
node local-dev/chat-attachment-e2e.mjs   # CA 12（对话附件：pre_upload_chat_file 直传 + chat-attachment/extract 提取 + 白名单/越权防护）
node local-dev/sandbox-limits-e2e.mjs    # SB 42（沙箱资源上限/出站网络策略/命令过滤：系统设置三项上限 + 网络管控模式与内网网段（@SET-S93）+ 平台级禁用命令清单（@SET-S94）+ 应用配置强校验/未启用放行/回读 + 出站规则校验与管控拦截（@AP-S125~S126）+ 命令过滤配置校验与回读（@AP-S128））
node local-dev/settings-logo-e2e.mjs    # SET 28（网站 Logo：root 上传/恢复默认 + 匿名 serverinfo 暴露 logoPath + 门禁与伪造 objectKey 防护；网站名称：root 保存/超长 400/清空回退默认；登录页背景图：上传/生效/恢复 + serverinfo 暴露 loginBackgroundPath + 门禁与伪造防护）
node local-dev/settings-smtp-e2e.mjs     # SET 36（邮箱验证与 SMTP 服务绑定：渠道连接配置在通知中心校验/绑定保存回读与非法值 400/门禁/serverinfo 公开门禁联动（开关×绑定×渠道可用性）/渠道自带配置真实发信与 AUTH 认证全链路，试发收件人由请求显式指定 @SET-66d；内置本地 SMTP 桩，零真实依赖）
node local-dev/auth-email-code-e2e.mjs   # AUTH 39（注册/找回密码邮箱验证码：serverinfo 双开关回显/邮箱验证未启用时拒绝/发码 60s 冷却/验证码一次性与错 5 次作废/默认与自定义模板 SmartFormat 插值/重置密码旧号失效全链路 + 验证码模板设置保存校验；内置本地 SMTP 桩，脚本经通知中心 SMTP 渠道绑定系统设置，零真实依赖）
node local-dev/auth-2fa-e2e.mjs          # AUTH 67（两步验证 TOTP：设置校验含新键 SET-92/绑定 setup-enable-disable 全流程/登录挑战一次性票据与防重放/强制 2FA 受限 token 白名单与旧 token 立即恢复/root 重置与目标保护 + 登录一体化：challenge_policy 回显/login_code/send 按用户名预发码（防枚举静默+冷却 429）/账号密码邮箱码一体直颁令牌 @AUTH-S43~S46/TOTP 优先；内置 SMTP 桩（经通知中心 SMTP 渠道绑定系统设置）与纯 node RFC 6238 实现 + RESP 直连 Redis 读图形验证码答案（E2E_REDIS 覆盖地址），快照恢复设置）
node local-dev/auth-captcha-e2e.mjs      # AUTH 40（图形验证码：captcha/new 自绘 SVG 结构与 20 次/分/IP 限流 429/同码错 3 次作废/不区分大小写/登录一码一消费/发码只验不消费/三模式 challenge_policy 回显（登录+注册+找回三面）/图形码错误不计登录失败 @AUTH-S50/分级递进阈值 fail<3 仅图形码 fail≥3 加邮箱码 @AUTH-S51/注册找回发码面首发免费再发过码 @AUTH-S52；前置 E2E_REDIS 指向 Redis，内置 SMTP 桩）
node local-dev/loginsafety-e2e.mjs       # LS 39（登录安全风控：门禁 403/401 + 登录日志全量落库 @LS-S4 + ForwardedHeaders 真实 IP 与 IP2Region 归属地（北京/海外）@LS-S6~S7 + R1 爆破封禁 403/事件/auto:R1/解封恢复 @LS-S8~S12 + 高风险推送 webhook 渠道（内置中文模板）@LS-S10b + R2 撞库喷洒 @LS-S13~S15 + R3 新 IP 中风险/R5 疾速旅行高风险 @LS-S16~S19 + 用户站内信提醒（内置模板）@LS-S19b~S19c + SMTP 渠道告警邮件（收件人=LS_NOTIFY_EMAILS，MIME 解码断言标题）@LS-S20~S20b + 标记已读联动未读角标 @LS-S21 + 手动封禁/解封 @LS-S22~S23 + 总开关关闭仅记录 @LS-S24 + 设置校验 400（含渠道 ID 非法 @LS-S25、告警收件人邮箱非法 @LS-S25e）+ 本人登录记录 @LS-S26；需 ForwardedHeaders 开启的后端（脚本头部含启动命令），内置本地 SMTP 桩与 webhook 桩）
node local-dev/workflow-e2e.mjs          # WF 130（流程应用：草稿/发布/调试执行/条件分支与脚本/多条件/知识库检索/问题分类/HTTP 请求节点/运行历史/系统设置·开场白/发布应用对话与 sys 系统变量/开始节点固定 question 契约与旧编排 query 镜像/Agent 应用节点与循环嵌套防护/核心节点不变量/对话实时过程：CUSTOM 节点事件 + AI 正文流式/图片理解节点：桩视觉模型多模态全链路·非视觉模型拒绝·来源防护；内置桩视觉模型零依赖，存量依赖真实模型的用例在环境无模型时失败属预期）
node local-dev/publication-e2e.mjs       # PB 95（上架审核：申请/审批/撤回，is_public 审批制；上架自动取最新团队发布版本，携带版本需递增；上架前置：未发布应用不可申请 @PB-S14；申请可视范围（全员/组织节点 self/up/down）审批通过物化 org_grant、广场与使用按范围过滤、下架/停用清授权 @PB-S15~S22；应用收藏与广场收藏置顶：门禁同口径/幂等/置顶排序/用户隔离/下架后仍可取消 @AP-S132~S134）
node local-dev/prompt-e2e.mjs            # PT 46（提示词：个人/团队 CRUD + 上架审批 + 市场 + 编辑器/头像）
node local-dev/skill-userconfig-e2e.mjs  # SKL 41（技能三级归属权限 + 应用默认技能与可选专家绑定：管理员绑定技能/专家、用户侧候选池=绑定∪市场公开∪本人个人不含团队全量、保存与会话校验同口径、上架公开后进入候选池 @AP-S160~S162）
node local-dev/team-agent-e2e.mjs        # AG 54（团队智能体 Phase 1 管理面：门禁 401/403/404 + 创建参数校验（key 规则/子标识规则/子名空/组内重名/超 20 个）+ 依赖校验（插件/知识库/知识图谱不可用 400、伪造头像 404）+ 创建与列表/详情回读（key+子智能体 key/中文名+依赖 JSON）+ 团队内 name/key 双唯一 409 + 关键字筛选 + 更新整替换与启停端点直切 + 头像防护 + 软删与同名同 key 重建 @AG-S1~S9 + 内置智能体（规划师/构建师 key=plan/build 团队创建自动种入、删除 400、编辑保留、补种端点门禁与幂等、旧名字行领养）@AG-S10）
node local-dev/team-agent-app-e2e.mjs    # TAA 40（Agent 应用绑定团队智能体：绑定保存校验（默认不在列表/伪造/停用/外部 400；绑定不开协作 200——D53 解耦）+ 主依赖缺失不阻断（弱依赖，声明项随选项回显）+ 回读不清空 + userconfig 双智能体与默认 + 发布快照内嵌定义冻结（发布后改智能体仍按旧版执行）+ 默认主提示词注入与子智能体名册（[可派发子智能体]）+ 具名 subagent_{key} 工具披露 + X-Moai-Team-Agent-Id 切换与非法头回退 + 具名派发全链路（子提示词注入/声明即边界白名单/深度恒 1/结论回传）+ moai.subagent 过程实时事件序列（started/text/completed/耗时/实时性）+ 绑定不开协作运行时语义（[团队智能体] 注入+禁止派发文案+零派发工具+零过程事件）+ 调试会话按草稿实时定义 @AP-S150~S159；内置桩模型，零 SKIP）
node local-dev/skill-usecount-e2e.mjs    # SKC 8（技能使用计数：装载工具调用写 skill-usage Redis 计数、统计模块批量落库 use_count + options/userconfig 双接口回显；本地桩模型，含约 100s 落库轮询）
node local-dev/skill-market-e2e.mjs      # SM 28（技能市场：市场/详情/下载可见性 + 上架审批 + 删除联动）
node local-dev/teamres-key-e2e.mjs       # TR 39（团队外部接入：moai-ac- 去范围化全量访问 /api/teamres（不传/传 scopes 均 200、列表无 scopes 回显、wiki 读写+KG+token 兑换+用户 token 无 app_chat）+ 统一团队资源 key teamres_key（key 唯一生成时机=开启资源功能：未开启建 key 400、PUT mcp-config/feature 开启自动生成并在 GET 回显、get-or-create 幂等/一资源一把（不同应用 key 不同）/归属 404/包围圈：wiki key 本库 REST+MCP 放行越库 403 与 list 过滤、app key 绑定应用仅其 ACP/A2A（打其它应用 403）/越圈 403/关闭功能后 key 403 feature_disabled 且 moai-ac- 同拒、重开复用原 key/重置旧行 id 不变旧 key 401/不可换 token/伪造与旧 moai- 前缀 401/跨团队 404；key 无启停状态且不可删除，TR-S26/S28 已退役，吊销=重置或关闭开关）；TA 系列整体退役不复用）
node local-dev/ai-channel-auth-e2e.mjs    # AC 23（渠道级批量授权：授权视图统计+并集回显/全量替换所有私有模型/公开模型不受影响/模型列表 authorizedTeamNames 回显/团队可用性联动（/ai/model/available）/清空/admin 门禁/团队渠道 400）
node local-dev/team-aichannel-e2e.mjs     # TC 38（团队自有模型渠道：门禁/渠道模型 CRUD/同步导入桩/聚合可用模型 isTeamChannel 标记（/ai/model/available）/模型网关下线断言/scopes 字段退役被忽略创建 200 @TC-S5/团队隔离/额度端点下线 + 渠道 tokens 统计字段与审计人名回显 @TC-S8；内置 OpenAI 兼容桩，零真实依赖）
node local-dev/subsystem-e2e.mjs          # SUB 101（系统级接入合并模型：root 门禁/子系统自填 appid+密钥（AES 可逆存储仅回掩码，reveal-secret RSA+AES 信封随时回显原文）/统一 /api/subsys/token 换取+refresh 轮换 @SUB-S45/心跳 v2 按实例 ZSET（缺实例标识 400/单双实例在线实例数 @SUB-S61~S62）/停用即拒/密钥重置/团队授权全员与指定/删除释放 appid + 委托 SSO 全链路与门禁 + 组织授权（org_grant：方向 self/up/down/团队∪组织并集 @SUB-S46~S52）+ 合并模型 @SUB-S53~S60：应用接入自动 moai-sa- appid/同表回显不漏原文/直连轨仅限应用接入/资源面全量（scope 分档下线）/旧端点下线/子系统专属端点 404/应用接入心跳；零外部依赖）
node local-dev/subsys-demo-seed.mjs       # DEMO（清空并重建演示数据：子系统×2（双回调/指定团队）+应用接入×2+组织授权，末尾功能验证：token/直连/心跳在线/信封回显/redirect_uri/限流 429；可重复执行）
node local-dev/demo-data-analysis-app.mjs # DEMO（商业数据分析演示：每次新建团队+团队智能体「商业数据分析组」（1主+5子：指标拆解师/趋势分析师/可视化顾问/行业情报员/报告撰写员）+Agent 应用（绑定智能体/开协作/开场白/快捷输入/发布）+真实 SSE 冒烟（派发+moai.subagent 事件）；环境无 conversation 模型时自动建本地桩保底；可重复执行）
node local-dev/system-access-app-e2e.mjs http://127.0.0.1:5000  # SAA 58（应用接入：管理门禁/创建一次性 appid+密钥/列表前缀回显/appid 直连与 access_token 双轨 @SAA-S17~S29/refresh 轮换/停用双轨 403/资源面全量访问+子系统 token 互通+心跳 @SAA-S60~S62/系统资源接口：用户 upsert 幂等推送+禁用+root 保护 @SAA-S34~S42、组织角色 @SAA-S43~S49、团队与成员治理 @SAA-S50~S56；scope 分档用例已退役 @SAA-S3/S18/S58；零外部依赖，需后端运行中）
node local-dev/org-e2e.mjs                # ORG 53（组织：管理员门禁/组织树创建与 path 物化路径计算/同父重名 409/子树整体迁移重写 path/防环/删除保护与级联解绑/用户组织分配·去重·整体覆盖 + 默认组织端点下线断言（ORG-33~38 退役不复用）+ 同级排序 sort_order（新建追加末尾/置顶/插位/跨级移动带排序/参照非法 400）+ 统一组织授权 ORG-39~53：self/up/down 方向判定/并集/整体覆盖/树迁移重判/删除组织级联清授权；零外部依赖）
node local-dev/message-e2e.mjs           # MSG 9（站内信+事件推送：Hub 鉴权 401/access_token 连接/接口门禁/空态与参数校验/MQ 管理口发布→消费者→onEvent 送达/目标隔离/同用户多连接；MQ 场景管理口不可达时 SKIP；发送落库链路见 tests/MoAI.Message.Tests 13 用例）
node local-dev/notification-e2e.mjs      # NT 126（通知中心：门禁 401/403 + 渠道 CRUD 与配置校验（飞书/钉钉/企微/飞书/钉钉/企微应用机器人/Telegram/Slack/Discord/Teams/通用 webhook/SMTP/Gotify/ntfy/Bark/Server酱/PushPlus/阿里云短信/腾讯云短信/站内推送）+ 渠道测试同步试发（飞书/钉钉桩内验签、企微 markdown、Telegram sendMessage、Slack/Discord(204)/Teams、Gotify token 参数、ntfy 根路径 JSON、Bark 设备键、Server酱 form、PushPlus token+模板、通用 webhook 默认结构与模板渲染、SMTP 桩捕获、站内推送 toAdmins）+ 广播异步投递 Hangfire 落发送记录（成功/失败终态带原因/快照/过滤分页）+ 停用不投递 + 站内信送达 + 删除留历史 + 广播目标组合（渠道多选/仅站内信/均不选 400）+ 通知模板（@NT-S23~S25：门禁/CRUD 校验/渲染预览与花括号字面保留/缺变量 400/删除保留历史）+ 短信渠道（@NT-S26~S28：配置校验/无模板直发失败/试发请求断言 POP 查询串与 TC3 头、TemplateParam JSON、TemplateParamSet 顺序/平台错误码归一）+ HTML 模板（@NT-S29：SMTP 发 text/html、webhook 降级去标签）+ 广播带模板（@NT-S30：标题正文可空/渲染文本落记录与站内信）+ SMTP/短信动态收件人（渠道创建不再要求收件人、广播含 SMTP/短信缺收件人与手机号格式非法 400 归入 @NT-S15 广播参数校验、发送记录 recipients 快照 @NT-S32；NT-10/NT-95 退役不复用）+ 应用机器人按绑定反查个人推送（OAuth 桩绑定链路 @NT-S17~S20：飞书 union_id 收件、钉钉 unionId→userid 换算+工作通知、企微 touser 直投、未绑定失败回显、跨企业换算失败 failed；需后端以 MoAI:Feishu/DingTalk/WeixinWork:Endpoint 覆盖到桩端口启动，未覆盖自动跳过该段）+ 站内信托底（@NT-S33~S34：TargetUserIds 定向站内信必达与渠道成败解耦、渠道试发走领域服务落发送记录含关联用户快照、试发人站内信副本必达、未绑定渠道试发失败但站内信仍达 NT-120~122）；内置 webhook/SMTP/OAuth 桩，零真实外部依赖；可重复执行）
node local-dev/aimodel-sync-e2e.mjs       # AM 14（模型同步挑选导入：sync 只探测不落库 + 候选项 recognized/exists/modelKind + import 默认停用 + 再 sync exists 标记；内置 /models 桩服务，零真实依赖）
node local-dev/model-catalog-e2e.mjs      # MC 16（模型目录在线同步：catalog/sync 门禁 401/403 + configs/models.json 优先内置目录且按时间戳自动重载 + configs 损坏回退不崩 + 真实下载 opencode 目录覆盖 configs 立即生效；下载场景需外网或 HTTPS_PROXY 可达 models.opencode.ai，网络不通时该场景失败属预期）
node local-dev/external-app-e2e.mjs       # EA 75（外部应用接入：三类外部 token/会话对话/访问点/沙箱技能限制/key 直连团队资源与直连会话（scopes 退役后 moai-ac- 全量放行 @EA-30）/悬浮组件 tokenProvider·onRequest 拦截器与全局 mount + 模型网关下线断言）
node local-dev/app-acp-e2e.mjs            # ACP 22（应用 ACP：接入 key 鉴权（moai-ac- 全量 / 本应用 moai-tr- 资源 key，需开启应用 ACP/A2A 单开关 feature_disabled 门禁）+ JSON-RPC initialize/session/* + Agent/Workflow 应用 ACP 对话 + cancel 中断 + 资源 key 删除吊销立即性 @ACP-S16~S17；内置桩模型与确定性流程编排，零 SKIP）
node local-dev/security-strategies-e2e.mjs  # SEC 23（应用安全五策略：输入检查点/工具参数预检拦截/回复哈希/事件审计/模型回复缓冲拦截/注入启发式；内置回显桩模型，零真实依赖）
node local-dev/app-a2a-e2e.mjs          # A2A 21（应用 A2A 协议：接入 key 鉴权（moai-ac- 全量 / 本应用 moai-tr- 资源 key，需开启应用 ACP/A2A 单开关）/Agent Card/message-send 与 stream（SSE 事件序列）/tasks-get·cancel/会话续聊与落库/用户 token/资源 key 重置吊销立即性 @A2A-S13）
node local-dev/subagent-e2e.mjs          # SA 10（子代理协作：spawn_subagent 委派全链路 + 深度固定 1 桩捕获断言 + 未开启不注册；本地桩模型）
node local-dev/agent-app-tool-e2e.mjs    # AP 21（Agent 应用绑定为工具：绑定校验/回显/null 不清空/互绑与传递环 400/含自身节点流程放行（流程侧守卫负责）/agent__ 无头单轮全链路按发布快照/history 透传/解绑下线；本地桩模型，零 SKIP）
node local-dev/agent-memory-e2e.mjs      # MEM 44（长期记忆：平台未开启时应用开启 400 + 工具显式写入 + flush→MQ 两段式自动提取 + 每轮注入 + 用户隔离/模式 + 删除清空 + 压缩折叠视图 + 统一模型锁定（有任意记忆换平台模型/维度 400、应用关闭重开不受影响、root 清空全部后解锁换模型恢复写入）；模型/维度统一系统设置、应用配置剥离模型字段 + 跨应用记忆系统开关（MEMORY_GLOBAL_ENABLED：未开总开关时开启 400/非法值 400/关闭时应用 allowGlobal 400/开启后放行）+ 检索条数退役（携带 retrievalTopK 保存剥离，AI 经 memory_search 自定默认 10 上限 30）+ 单用户记忆上限 10~500（默认 100 超限覆盖）；本地桩 embedding+chat，零 SKIP，收尾还原 MEMORY_* 设置）
node local-dev/memory-management-e2e.mjs # MEM 54（记忆管理：人工写入端点下线断言（新建/编辑/promote 404/405，记忆内容一律 AI 自动维护、人工仅删除与清空）+ 全局记忆仅 AI 工具写入 + 跨应用总览/用户隔离/列表 keyword 关键字检索 + 多级开关：平台 MEMORY_ENABLED 总开关/MEMORY_GLOBAL_ENABLED 跨应用开关（关闭时应用 allowGlobal 400、存量允许应用运行时不注入回显 false）/管理员 allowGlobalMemory/用户接入开关 + 全局跨应用注入与注入不丢上下文 + memory_save scope=global/forget 全局兜底 + 仅删除与清空 + MEMORY_* 统一设置门禁校验与模型锁定（应用配置剥离模型字段）/root 清空全部记忆（应用+全局）解锁；本地桩 embedding+chat，零 SKIP，收尾自清理；中断残留先跑 memory-global-reset.mjs）
node local-dev/memory-migration-e2e.mjs  # MG 44（长期记忆向量化模型迁移：应用与全局记忆造数均经 memory_save 工具轮（人工写入端点已下线，前置开启跨应用记忆开关）/MQ 异步用新模型/维度重建全部记忆向量（临时表+事务换名+设置末置切换+进度回写）/发起校验（无记忆·同参数·未知模型·维度越界 400、非 root 403）/迁移期间写入阻断（工具轮被拒不落库+删除 409）与并发发起 409/迁移后数据保留与新维度检索命中/死渠道失败自动回滚（设置与数据不变）/失败后重发成功；本地桩 embedding 按请求 dimensions 出向量，零 SKIP，收尾自清理）
node local-dev/dynamic-plugin-e2e.mjs    # DYN 169（实例管理 + 失败路径 + 内置模板注册 + 系统插件页团队隔离过滤 @DYN-S70 + 插件头像 S43~S45/创建携带 avatarPath S121~S123；真实 Key/真库用例环境门控 SKIP）
node local-dev/plugin-multi-tool-semantic-e2e.mjs  # DYN 30（多工具动态插件 @DYN-S127~S136：[AiPluginTool] 契约/实例工具清单/单工具示例接口/按 toolKey 独立调试/未知 toolKey 拒绝/存量单工具兼容/配置注释原文保留 + 插件语义配置 @DYN-S137~S144：使用须知/语义条目/工具元数据整体覆盖保存回显/调用策略校验 400/门禁；需后端运行中 + 存量库已应用 asserts/plugin_semantic.sql）
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
