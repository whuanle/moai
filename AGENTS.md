# MoAI 开发指南（AGENTS.md）

> 项目入口文档。只讲现状、硬约束、文档索引；规范细节一律以 `docs/`、`ui/docs/` 真源为准，本文不复制全文。
> 动后端前读 [cqrs-conventions.md](./docs/cqrs-conventions.md)，动前端前读 [frontend-conventions.md](./ui/docs/frontend-conventions.md) + [design-system](./ui/docs/design-system/README.md)，改已有模块先读其 `sdd.md`/`bdd.md`，**写任何文档前必读 [DOC-STANDARD.md](./docs/DOC-STANDARD.md)**。

## 项目简介

开源 AI 应用平台：.NET 10 模块化单体（Maomi.Core 模块框架 + EF Core + PostgreSQL/pgvector + Redis + MinIO + RabbitMQ + MediatR）+ React 19 前端。
已落地：认证账号与用户治理、设置、OAuth 连接器、文件存储、后台任务、分类、团队（成员/角色/转让）、团队插件授权、团队变量、知识库（含向量化）、AI 渠道与模型、AI 插件（静态/动态/自定义）、AI 网关、团队应用（Agent/流程应用的创建与基础信息）、提示词（个人/团队/市场上架审批）、技能（平台内置/团队/个人归属维护 + 市场上架审批 + 技能包下载 + 应用默认技能·用户对话内勾选，沙箱加载执行）。进度见 [rounds-log.md](./docs/rounds-log.md)。

## 仓库结构

```
src/MoAI/         组合宿主（Program.cs、MainModule、OpenApiModule）
src/{auth,account,settings,oauthconnect,storage,common,infra,database,hangfire}/   平台底座
src/{classify,team,teamplugin,variable,wiki,app,publication}/                      团队协作
src/{aichannel,aimodel,aiplugin,skill,gateway}/                                   AI 业务层
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
- 接口文档由 NSwag 自动生成，`/openapi/v1.json`，仅 Development 暴露；勿手写接口文档
- 前后对接务必使用 kiota，禁止自行拼接 http 请求

**文档**（[真源](./docs/DOC-STANDARD.md)）
- 改代码 → 更新 `bdd.md` 场景 → `tdd.md` 补映射并执行 → `sdd.md`/`sop.md` 同步
- 场景编号 `@<缩写>-S<n>` 永久不复用；分层不重复内容，跨层一律链接

## 文档索引

- [docs/README.md](./docs/README.md) — 文档地图（L0）与模块四件套索引
- [cqrs-conventions.md](./docs/cqrs-conventions.md) ｜ [api_interface.md](./docs/api_interface.md) ｜ [DOC-STANDARD.md](./docs/DOC-STANDARD.md)
- [aiplugin-authoring.md](./docs/aiplugin-authoring.md) ｜ [settings.md](./docs/settings.md) ｜ [storage-file-layout.md](./docs/storage-file-layout.md)
- [ui/AGENTS.md](./ui/AGENTS.md) — 前端专属入口（动 `ui/` 时读这份）
- [frontend-conventions.md](./ui/docs/frontend-conventions.md) ｜ [design-system](./ui/docs/design-system/README.md)
- [rounds-log.md](./docs/rounds-log.md) — 轮次闭环台账与证据

> ⚠️ `docs/README.md` 模块地图滞后于 `src/`：`aichannel`、`gateway`、`aimodel`、`ai`、`admin`、`plugin` 尚无四件套，改动以源码为准并补文档。

## 本地开发

- 后端：`cd src/MoAI && dotnet run`，默认 **5000**（同监听 5001），取自 `MoAI:Port`；`MAI_FILE=... ASPNETCORE_ENVIRONMENT=Development dotnet run` 可覆盖
- 文档：`http://127.0.0.1:5000/openapi/v1.json` ｜ Scalar：`/scalar/v1`
- 前端：`cd ui && npm run dev`（4000）
- 容器（docker compose）：postgres 5432、redis 6379、rabbitmq 5672/15672；**MinIO 不在 compose 中**，由 `MoAI:Storage:Endpoint` 指向外部实例
- 种子账号：admin / abcd123456（root）
- git 需代理：`git -c http.proxy=http://127.0.0.1:7897 -c https.proxy=http://127.0.0.1:7897 fetch`

## 验证命令（提交前全绿）

```bash
dotnet build src/MoAI/MoAI.csproj                 # 0 error
cd ui && npm run typecheck && npm run lint && npm run test
# E2E（需后端运行中）
node local-dev/user-management-e2e.mjs   # UM 37
node local-dev/team-e2e.mjs              # TM 47
node local-dev/wiki-e2e.mjs              # WK 32
node local-dev/wiki-workflow-e2e.mjs     # WK 27（默认工作流配置 + 多选批量工作流：切割/生成元数据/向量化三步自由组合与单步执行、错误隔离与参数校验；依赖模型渠道的场景在环境无可用模型时自动跳过）
node local-dev/variable-e2e.mjs          # VR 30（变量：{key} SmartFormat 插值 + 私密解密 + 未匹配/JSON 花括号字面保留 + 增删改查权限）
node local-dev/team-plugin-e2e.mjs       # TP 31（团队插件：MCP/OpenAPI 导入刷新删除权限 + 团队变量插值 MCP 桩验证 + 落库保留占位符 + OpenAPI header/query 保存回显）
node local-dev/app-e2e.mjs               # AP 29
node local-dev/chat-attachment-e2e.mjs   # CA 12（对话附件：pre_upload_chat_file 直传 + chat-attachment/extract 提取 + 白名单/越权防护）
node local-dev/sandbox-limits-e2e.mjs    # SB 21（沙箱上限：系统设置三项 + 格式校验 + 应用配置强校验/未启用放行/回读）
node local-dev/settings-logo-e2e.mjs    # SET 18（网站 Logo：root 上传/恢复默认 + 匿名 serverinfo 暴露 logoPath + 门禁与伪造 objectKey 防护；网站名称：root 保存/超长 400/清空回退默认）
node local-dev/workflow-e2e.mjs          # WF 118（流程应用：草稿/发布/调试执行/条件分支与脚本/多条件/知识库检索/问题分类/HTTP 请求节点/运行历史/系统设置·开场白/发布应用对话与 sys 系统变量/开始节点固定 question 契约与旧编排 query 镜像/Agent 应用节点与循环嵌套防护/核心节点不变量/对话实时过程：CUSTOM 节点事件 + AI 正文流式）
node local-dev/publication-e2e.mjs       # PB 34（上架审核：申请/审批/撤回，is_public 审批制）
node local-dev/prompt-e2e.mjs            # PT 46（提示词：个人/团队 CRUD + 上架审批 + 市场 + 编辑器/头像）
node local-dev/skill-userconfig-e2e.mjs  # SKL 25（技能三级归属权限 + 应用默认技能：管理员配置默认技能、用户技能勾选仅限默认范围、专家按个人/团队可用范围校验）
node local-dev/skill-market-e2e.mjs      # SM 28（技能市场：市场/详情/下载可见性 + 上架审批 + 删除联动）
node local-dev/dynamic-plugin-e2e.mjs    # DYN 102（实例管理 + 失败路径 + 内置模板注册 dynamic_greet/bocha_web_search/bocha_ai_search/feishu_web_hook_text/javascript_executor/postgres_query/mysql_query）
node local-dev/bocha-search-e2e.mjs      # DYN 22（博查成功路径与响应解析，自建桩服务，无需真实 Key）
node local-dev/paddleocr-e2e.mjs         # DYN 36~42（PaddleOCR 三模板成功路径 + 响应解析，自建桩服务，无需真实 PaddleOCR）
node local-dev/feishu-e2e.mjs            # FS 28（飞书应用连接 CRUD + 渠道绑定互斥，假凭证即可）
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
