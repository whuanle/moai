# MoAI 文档地图（L0 导航）

> 分层与写作规范见 [DOC-STANDARD.md](./DOC-STANDARD.md)（**写任何文档前必读**）。本页只做索引，不承载内容。

## 分层总览

| 层 | 内容 | 入口 |
|---|---|---|
| L0 导航 | 本页 | — |
| L1 全局规范 | 后端 CQRS / 文档标准 / 前端约定 / 设计系统 | 见下「全局规范」 |
| L2 领域四件套 | 21 个模块 × {sdd,bdd,tdd,sop} | 见下「模块地图」 |
| L3 证据 | 可执行验收脚本 / 单测 / 回归命令 | 见下「回归入口」 |

## 全局规范（L1）

- [DOC-STANDARD.md](./DOC-STANDARD.md) — 文档分层、四件套契约、Gherkin（小黄瓜）规范、相互引用规则
- [cqrs-conventions.md](./cqrs-conventions.md) — 后端 CQRS 三层与 `IModelValidator` 硬约束
- [aiplugin-authoring.md](./aiplugin-authoring.md) — 静态/动态插件编写规范（发现机制、运行时接口、配置校验、流程）
- [settings.md](./settings.md) — setting 表机制（接口层权限模式范例）
- [storage-file-layout.md](./storage-file-layout.md) — 对象存储路径布局
- [ui/docs/frontend-conventions.md](../ui/docs/frontend-conventions.md) — 前端架构与目录约定
- [ui/docs/design-system/](../ui/docs/design-system/README.md) — 设计系统规范

## 模块地图（L2，21 个四件套）

缩写列 = BDD 场景编号前缀（见各模块 bdd.md）。

### 后端领域（docs/）

| 模块 | 缩写 | 说明 |
|---|---|---|
| [auth](./auth/) | AUTH | 登录/注册/OAuth 登录/刷新，JWT+RSA |
| [account](./account/) | ACC | 账号自助：资料/改密/头像/OAuth 绑定 |
| [user-management](./user-management/) | UM | 管理员用户治理（列表/授权/禁用/重置密码）；[📘 HTML 操作指南](./user-management/manual.html)（含截图） |
| [settings](./settings/) | SET | 系统设置项（SettingDefinitions 注册表） |
| [oauthconnect](./oauthconnect/) | OC | 第三方登录连接器管理 |
| [classify](./classify/) | CLS | 分类管理（plugin/app/kb 三类，仅管理员） |
| [aiplugin-static](./aiplugin-static/) | STP | 静态插件（列表合并去重、运行抽屉、编辑写回） |
| [aiplugin-dynamic](./aiplugin-dynamic/) | DYN | 动态插件（实例创建/编辑/删除、模板+配置、运行实例） |
| [common](./common/) | COM | serverinfo / build_guid |
| [storage](./storage/) | STO | S3/MinIO 三段式上传与 /static 中转 |
| [hangfire](./hangfire/) | HF | 定时任务与 recurring job 扩展点 |
| [infra](./infra/) | INF | 配置加载/RSA/异常/模块框架 |
| [database-scaffold](./database-scaffold/) | DB | EF 模型、种子数据、PostgresScaffold 工具 |
| [deployment](./deployment/) | DEP | Docker/entrypoint/本地环境 |
| [team](./team/) | TM | 团队/成员/角色（Owner/Admin/Member）、解散与所有权转让 |
| [wiki](./wiki/) | WK | 团队知识库与文档（内容协作 Member 可写） |
| [variable](./variable/) | VR | 团队变量（普通/私密、分组、${key} 服务端替换；私密值仅管理员） |

### 前端（ui/docs/）

| 模块 | 缩写 | 说明 |
|---|---|---|
| [auth-flow](../ui/docs/auth-flow/) | FE-AUTH | 登录/注册/OAuth 回调/token 续期 |
| [api-layer](../ui/docs/api-layer/) | FE-API | Kiota 工厂/中间件/封装/syncapi |
| [theme](../ui/docs/theme/) | FE-TH | tokens/预设/主题切换链路 |
| [components-base](../ui/docs/components-base/) | FE-CB | Page/Card/StatCard/DataTable/QueryBar/PageToolbar |
| [components-form](../ui/docs/components-form/) | FE-CF | FormPage/DetailPage/Chat/Feedback |
| [layout-routing](../ui/docs/layout-routing/) | FE-LR | AppLayout/AppSider/路由表/守卫 |
| [store-i18n](../ui/docs/store-i18n/) | FE-SI | zustand persist/i18next |
| [page-account](../ui/docs/page-account/) | FE-PA | 账号设置页 /account |
| [page-admin](../ui/docs/page-admin/) | FE-PG | /settings、/oauthconnect 管理页 |
| [dashboard-testing](../ui/docs/dashboard-testing/) | FE-DT | Dashboard 页与测试基建 |
| [variable](../ui/docs/variable/) | FE-VR | /variable 变量页（掩码/权限渲染/私密编辑三原则） |

各模块入口统一为 `sdd.md`；四件互链见每个文件头部「关联」块。

## 回归入口（L3）

```bash
# 后端（需运行于 :5210，配置 MAI_FILE=local-dev/system.local.json）
node local-dev/user-management-e2e.mjs        # UM 34 场景
node local-dev/audit-345.mjs                  # ACC/SET/OC 14 场景
node local-dev/audit-storage.mjs              # STO 全链路 7 场景
node local-dev/auth-lockout-check.mjs         # AUTH 锁定 8 场景
# 前端
cd ui && npm run typecheck && npm run lint && npm run test
```

浏览器点击走查：见各模块 sop.md「验收流程」（引用 BDD 场景编号）。
UX 专项走查与截图：`local-dev/ux-review.mjs`（输出至 local-dev/shots-ux/）。
