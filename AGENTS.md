# MoAI 开发指南（AGENTS.md）

> 面向 AI 编码助手与开发者的项目入口文档。所有规范文档见 [docs/README.md](./docs/README.md)，本文只做索引与硬约束摘要。

## 项目简介

MoAI 是开源 AI 应用平台（.NET 10 后端 + React 19 前端）。当前处于**平台底座阶段**：认证、账号、用户治理、设置、OAuth 连接器、文件存储、后台任务、**团队协作**（建团/成员/角色，2026-09-02 落地）已就绪；AI 业务层（聊天/知识库/插件/工作流，Semantic Kernel）尚未实现。

## 仓库结构

```
├── src/                  # 后端（模块化单体，唯一可执行项目 src/MoAI）
│   ├── MoAI/             # 组合宿主：Program.cs、模块注册、中间件
│   ├── auth/             # 认证：登录/注册/OAuth/刷新 Token（JWT）
│   ├── account/          # 账号自助 + 管理员用户治理（/usermanage）
│   ├── settings/         # 系统设置（setting 表 KV）
│   ├── oauthconnect/     # 第三方 OAuth 连接器 CRUD（管理员）
│   ├── storage/          # S3 兼容文件存储（MinIO），/static 中转
│   ├── team/             # 团队协作：建团/成员/角色（bool 软删除 + partial 唯一索引）
│   ├── database/         # EF Core + PostgreSQL(pgvector) + Redis 注册
│   ├── hangfire/         # 后台任务（Redis 存储，桥接 MediatR）
│   ├── common/           # serverinfo（RSA 公钥下发）等
│   └── infra/            # 配置加载（MAI_FILE）、Refit、MQ 抽象
├── ui/                   # 前端（React 19 + TS + Vite + antd 5 + zustand）
└── docs/                 # 全部规范与功能文档（见 docs/README.md）
```

## 硬约束（违反即返工）

**后端：**
- CQRS 三层：`*.Shared`（Command/Query 定义 + `IModelValidator<T>`）→ `*.Core`（Handler）→ `*.Api`（Controller）。详见 [docs/cqrs-conventions.md](./docs/cqrs-conventions.md)
- 角色门禁（admin/root 判断）只在 Controller 层；目标保护规则（不能操作 root/自己/其他 admin）在 Handler 层
- 写用户相关数据后必须 `RemoveUserStateAsync` 失效 Redis 用户态缓存
- 密码一律 RSA(PKCS1) 密文传输，服务端解密后校验强度，`PBKDF2Helper.ToHash` 落库

**前端（详见 [ui/docs/frontend-conventions.md](./ui/docs/frontend-conventions.md) 与 [ui/docs/design-system/](./ui/docs/design-system/README.md)）：**
- 禁止直接 import antd 的 Table/Form 等被设计系统封装的组件，用 `src/design-system/components/*`
- 颜色/间距必须用 token，禁止硬编码
- 危险操作必须 Popconfirm；文案全部走 i18n（zh-CN + en-US 同步改）
- API 客户端是 Kiota 生成的（`ui/src/api/client/`，勿手改），`npm run syncapi http://127.0.0.1:5210/openapi/v1.json` 重新生成；手写封装放 `ui/src/api/*.ts`

## 本地开发

- 后端：`src/MoAI` 下 `MAI_FILE=/Users/wen/project/maomi/local-dev/system.local.json ASPNETCORE_ENVIRONMENT=Development dotnet run`（端口 5210）
- 前端：`ui/` 下 `npm run dev`（端口 4000）
- 基础设施容器：moai-postgres(5432)、moai-redis(55379)、moai-rabbitmq(55672)、moai-minio(9000)
- 种子账号：admin / abcd123456（root）
- GitHub 直连会失败，git 操作带代理：`git -c http.proxy=http://127.0.0.1:7897 -c https.proxy=http://127.0.0.1:7897 fetch`

## 验证命令（提交前全绿）

```
dotnet build src/MoAI/MoAI.csproj          # 后端 0 error
cd ui && npm run typecheck && npm run lint && npm run test   # 前端全绿
node local-dev/user-management-e2e.mjs     # e2e（需后端 5210 运行中）
```

## 文档索引

- 文档地图（L0 导航）与 23 轮模块四件套（SDD/BDD/TDD/SOP）：[docs/README.md](./docs/README.md)
- **写任何文档前必读**：[docs/DOC-STANDARD.md](./docs/DOC-STANDARD.md)（分层 L0–L3、Gherkin 场景编号、互链规则）
- 轮次闭环台账（含每轮真实证据）：[docs/rounds-log.md](./docs/rounds-log.md)

## Skills

项目专属开发 skill（AI 助手触发），按职责分层，总入口：[agent-tools/skills/README.md](./agent-tools/skills/README.md)。

- **L1-orchestration/moai-feature** — 全栈新功能编排（后端 → syncapi → 前端 → 验证）
- **L2-code-standards/moai-cqrs-backend** — 后端 CQRS 三层细则（真源 `docs/cqrs-conventions.md`）
- **L2-code-standards/moai-frontend-ui** — 前端页面细则（真源 `ui/docs/frontend-conventions.md` + design-system）
- **L3-fix-standards/moai-cqrs-review** — 铁律审查与修复五步标准

全局副本：`~/.zcode/skills/moai-code-organization/`。新增 skill 按 `skills/README.md` 规则登记。Obsidian 镜像：`MoAI/SOP-端到端场景/98-框架代码开发Skill规范`。
