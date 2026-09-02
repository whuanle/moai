# infra 基础设施（INF）操作手册（SOP）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md)（验收场景编号） ｜ [TDD](./tdd.md)（命令证据） ｜ [SOP](./sop.md)

## 1. 系统配置

优先级（低→高）：环境变量/命令行 → appsettings.json → `{AppPath}/configs/system.json`（MAI_FILE 缺失时的默认）→ **MAI_FILE 指定文件（最高）**。修改配置需重启（无热加载）；容器部署无需 MAI_FILE（entrypoint 用环境变量渲染 system.json）；`MoAI:Port` 同时决定第二端口 Port+1（[@INF-S2](./bdd.md#inf-s2)）。

| 操作 | 步骤 | 对应场景 |
|---|---|---|
| 加一个配置项 | ① `SystemOptions.cs`（或 Storage/OTLP Options）加 public init 属性；② 配置文件 `MoAI` 节补键（建议 camelCase）；③ build 0 错误后重启验证。消费方注入 `SystemOptions` 单例，**勿**在领域模块自行 GetSection | [@INF-S1](./bdd.md#inf-s1)、[@INF-S7](./bdd.md#inf-s7) |
| 本地开发标准姿势 | `MAI_FILE=/Users/wen/project/maomi/local-dev/system.local.json ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/MoAI`（AGENTS.md：本地配置放仓库外） | [@INF-S2](./bdd.md#inf-s2) |

注意：`configs/system.json` 有不生效历史键（DBType/Wiki/Storage.LocalPath），加键前核对 [SDD 字段表](./sdd.md)；MAI_FILE 后缀须 .json/.yaml/.conf（[@INF-S3](./bdd.md#inf-s3)）。

## 2. RSA 密钥管理

| 事项 | 操作 |
|---|---|
| 查看公钥 | `curl -s http://127.0.0.1:5210/api/common/serverinfo`（[@INF-S10](./bdd.md#inf-s10)） |
| 位置 | `{AppPath}/configs/rsa_private.key`（2048 PKCS8 PEM，首启自动生成，[@INF-S8](./bdd.md#inf-s8)） |
| 备份/迁移 | 复制整个 `configs/`（含私钥），否则新实例公钥变化、旧 JWT 全失效 |
| 轮换 | 停进程 → 删私钥 → 启动（自动重生成）。**影响：全部 token 失效、所有用户重登**（[@INF-S11](./bdd.md#inf-s11)） |
| 多实例 | 必须共享同一份私钥（挂同一卷），否则登录随机失败 |

## 3. 外部 HTTP 客户端接入

新增第三方接口：① `MoAI.Infra.ExternalHttp/<Provider>/` 建 Refit 接口与 Models；② 模块中 `AddRefitClient`（固定域名设 BaseAddress，动态地址仿 `IOAuthClientFactory` 建工厂+命名 HttpClient，[@INF-S18](./bdd.md#inf-s18)）；③ 挂 `ExternalHttpMessageHandler` 与 `SetHandlerLifetime(30s)`；④ 领域模块构造注入使用，JSON 约定由统一 RefitSettings 保证（[@INF-S16](./bdd.md#inf-s16)）。排障看出站日志 `HttpLog`（二进制打码，[@INF-S17](./bdd.md#inf-s17)）。

## 4. 模型验证与用户上下文接入

- 新命令实现 `IModelValidator<T>` 并在 `static Validate` 写规则——不要手写 AbstractValidator 子类，TypeFilter 自动注册（[@INF-S12](./bdd.md#inf-s12)，详见 [../cqrs-conventions.md](../cqrs-conventions.md)）。
- 需要操作者身份的 Command 实现 `IUserIdContext`，Controller 用 `_userContextProvider.SetUserContext(cmd)` 注入（[@INF-S14](./bdd.md#inf-s14)）。
- **已知坑**：路由参数回填发生在自动验证之后，对路由 id 加 NotEmpty/GreaterThan 规则会恒 400——路由参数不要加校验规则（[../user-management/sop.md](../user-management/sop.md) 存档、[../oauthconnect/sdd.md](../oauthconnect/sdd.md) 修复史互证）。

## 5. 消息队列

连接串 `MoAI:RabbitMQ`；队列由 AutoQueueDeclare 自动声明（[@INF-S22](./bdd.md#inf-s22)）。本地：`docker compose up -d rabbitmq`（容器 moai-rabbitmq，管理台 15672 guest/guest）。排障顺序：连接串 → 容器健康 → 管理台 Queues → 应用日志。

## 6. 排障

| 现象 | 原因 | 处理 |
|---|---|---|
| 启动报 "The system configuration cannot be loaded" | 配置缺 `MoAI` 节 | 检查 MAI_FILE 指向与内容（[@INF-S6](./bdd.md#inf-s6)） |
| Release 启动报 "The current file type cannot be imported" | MAI_FILE 非 json/yaml/conf | 改扩展名（[@INF-S5](./bdd.md#inf-s5)） |
| 改了 system.json 行为没变 | MAI_FILE 指向另一文件覆盖（DEBUG 下坏后缀还不报错） | `echo $MAI_FILE` 核对（[@INF-S4](./bdd.md#inf-s4)） |
| 前端登录报密码解密失败 | 私钥被轮换/多实例不一致，前端公钥过期 | 刷新页面重取 serverinfo（[@INF-S11](./bdd.md#inf-s11)） |
| 外部接口调用无日志 | 客户端未挂拦截器 | 按第 3 节补注册 |
| 登录后某些请求 401 | DEBUG token 7 天 / Release 30 分钟，跨环境误判 | 见 [../auth/sop.md](../auth/sop.md) |

## 7. 验收流程（变更后）

跑 [TDD 回归命令](./tdd.md)（build 0 错误；serverinfo 的 serviceUrl/rsaPublic 正确；模块图 grep 一致）；配置类变更另按 [@INF-S1](./bdd.md#inf-s1)~[@INF-S7](./bdd.md#inf-s7) 走查后缀路由与回落分支。

## 8. 历史验收存档（L3 证据，保留原始记录）

- **2026-09-01（轮 9，as-built）**：`dotnet build src/MoAI/MoAI.csproj` 0 错误（58 个存量 NU1507 警告，3.39s）；`/api/common/serverinfo` 200 且 `serviceUrl=http://127.0.0.1:5210` 与 MAI_FILE（local-dev/system.local.json）一致——仓库 configs/system.json 为 5000，证明覆盖关系；rsaPublic 返回 2048 位 SPKI 公钥；模块图 grep 与 SDD 一致；T1~T17 回归项全过（yaml/ini 包引用、TypeFilter、RefitSettings、PBKDF2 等均走查核对）。
- 互证记录：HTTP 层行为（配置加载结果、公钥下发、异常转译）在 auth / user-management 轮 34/34 与深度 API 68/68 中实测；OAuth 动态客户端在 oauthconnect 轮本地 OIDC 模拟 12/12 中实测。

## 9. 变更记录

| 日期 | 变更 |
|---|---|
| 2026-09-01 | 初版（轮 9，as-built 回溯整理） |
| 2026-09-01 | 按 [DOC-STANDARD](../DOC-STANDARD.md) 重构四件套（场景编号化、四件互链） |
