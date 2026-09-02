# infra 基础设施（后端，INF）设计规格（SDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md)（@INF-Sxx） ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 下游：全部领域模块（[../auth](../auth/sdd.md)、[../storage](../storage/sdd.md)、[../hangfire](../hangfire/sdd.md) 等） ｜ 证据：构建/配置命令（见 [TDD](./tdd.md)）
> 规范：[../DOC-STANDARD.md](../DOC-STANDARD.md)、[../cqrs-conventions.md](../cqrs-conventions.md)。行为场景见 BDD，本文不重复。

## 目标与职责

全部后端领域模块共享的基础设施层：

1. **配置加载**：环境变量 `MAI_FILE` 指定系统配置文件（json/yaml/ini(.conf)），未指定回落 `{AppPath}/configs/system.json`；绑定 `MoAI` 节到强类型 `SystemOptions`。
2. **跨模块抽象**：模型验证（`IModelValidator<T>`+`AutoValidator<T>`）、用户上下文（`IUserIdContext`/`IUserContextProvider`）、分页/响应模型、`BusinessException`（int StatusCode + Argments）、PBKDF2/RSA/AES 助手、雪花 ID。
3. **消息队列**：Maomi.MQ.RabbitMQ 统一装配（事件总线与消费者扫描）。
4. **外部 HTTP**：Refit 声明式客户端 + 统一 JSON 序列化 + 统一日志/链路拦截器（`ExternalHttpMessageHandler`）。

## 组件

```
src/infra/
├── MoAI.Infra.Shared/              零依赖抽象层：AppConst(AppPath/ActivitySource "MoAI")、Models、Services
│                                   （IModelValidator/IRsaProvider/IAESProvider/IIdProvider/IClientInfoProvider）、
│                                   Helpers（PBKDF2/Rsa/Hash/PropertySet(init 反射赋值)）、BusinessException、JSON 转换器
├── MoAI.Infra.Configuration.Shared/ SystemOptions/SystemOptionStorage/OpenTelemetryOptions、
│                                   InfraConfigurationModule（首启生成 2048 RSA 并注册 IRsaProvider）、RsaProvider
├── MoAI.Infra.Core/                InfraCoreModule：AddMaomiMQ + AES + IIdProvider + TypeFilter 自动注册验证器
├── MoAI.Infra.ExternalHttp/        9 个 Refit 客户端注册 + ExternalHttpMessageHandler + OAuth/ 动态客户端
│                                   （Feishu×3/WeixinWork/DingTalk/Doc2x/BoCha/Put/Paddleocr）
└── MoAI.Infra.Api/                 ExternalApiAttribute（预留，当前无使用方）
```

依赖方向：Shared ← Configuration ← ExternalHttp ← Core（聚合）。全部 `net10.0`、`LangVersion 12`（static abstract 接口成员）。模块图：`MainModule → InfraCoreModule → {InfraConfigurationModule, InfraExternalHttpModule}`。

## SystemOptions（MoAI 节，真实字段）

| 键 | 类型/默认 | 说明 |
|---|---|---|
| Debug / Name | bool / string="MoAI" | 调试开关；Name 兼作 MQ AppName |
| AES | string | 密钥（不足 32 字节补空格截断） |
| Port | int | 主端口；**Port+1 为第二端口**（内部/外部系统接口） |
| Server / WebUI | string | 后端/前端对外地址（JWT Issuer/Audience、OAuth 回跳校验） |
| Database / Redis / RabbitMQ | string | 连接串 |
| Storage | SystemOptionStorage（required） | S3 兼容（见 [../storage/sdd.md](../storage/sdd.md)） |
| MaxUploadFileSize | int=104857600 | 上传上限（100MB） |
| OTLP | OpenTelemetryOptions | Trace/Metrics 地址 + Protocol |

绑定容错：**未知键直接忽略**——仓库 `configs/system.json` 的 `DBType`、`Wiki:*`、`Storage.LocalPath` 不在强类型上，不生效（[@INF-S7](./bdd.md#inf-s7)）。

## 关键决策与机制

1. **装配链路**（`builder.UseMoAI()`）：读 MAI_FILE → 按后缀路由（json=`AddJsonFile(optional:true)` / yaml=`AddYamlFile`（NetEscapades 3.1.0）/ conf=`AddIniFile`）→ 配置源在默认源之后，**优先级最高**（[@INF-S2](./bdd.md#inf-s2)）；`GetSection("MoAI").Get<SystemOptions>()` 失败抛 `FormatException("The system configuration cannot be loaded.")`（[@INF-S6](./bdd.md#inf-s6)）；Serilog（ReadFrom.Services+Configuration）。
2. **RSA 密钥**：首启 `configs/rsa_private.key` 不存在 → `RSA.Create(2048)` 导出 PKCS8 PEM 落盘并注册单例；已存在直接读（[@INF-S8](./bdd.md#inf-s8)/[@INF-S9](./bdd.md#inf-s9)）。`GetPublicKey()`=Base64(SPKI) 供 `/api/common/serverinfo` 下发；Encrypt/Decrypt 默认 PKCS1（与前端 JSEncrypt 对齐）；`GetRsaSecurityKey()` 供 JWT RS256。**同一密钥兼管密码解密与 JWT 签名：删私钥重启 = 全部 token 失效**（[@INF-S11](./bdd.md#inf-s11)）。
3. **模型验证自动注册**（TypeFilter）：实现 `IModelValidator<T>`（泛型参数须为自身，防继承误注册）→ 注册 `IValidator<T>`=AutoValidator<T> + 自身 Scoped；SharpGrip 在 Controller 绑定时触发（[@INF-S12](./bdd.md#inf-s12)/[@INF-S13](./bdd.md#inf-s13)）。
4. **Maomi.MQ**：WorkId=1、AutoQueueDeclare=true、AppName=Name、ConsumerDispatchConcurrency=100、ClientProvidedName="moai"，Rabbit Uri 来自 `MoAI:RabbitMQ`（[@INF-S22](./bdd.md#inf-s22)）。
5. **外部 HTTP**：统一 RefitSettings（SystemTextJson、camelCase、忽略 null、允许多余逗号与注释、Buffered）；全部挂 `ExternalHttpMessageHandler`（日志+Activity 打标，二进制 `[Binary Content]` 打码）+ `SetHandlerLifetime(30s)`（Put/Paddleocr 60s）；固定 BaseAddress 见组件清单；OAuth 因 IdP 端点动态走 `IOAuthClientFactory` 运行时 `RestService.For`（命名 HttpClient `MoAI.OAuth` 复用连接池）（[@INF-S16](./bdd.md#inf-s16)~[@INF-S18](./bdd.md#inf-s18)）。
6. **其余基座**：Yitter 雪花 ID（SeqBitLength=10、workId=0、GeneratorKey 16 位 hex）；AES（CBC、密钥补齐 32 字节、密文=IV(16B)+密文 Base64）；PBKDF2（10000 迭代 SHA256、salt/输出 128B、VerifyHash 非法 Base64 返回 false）；`UserContext.SetUserContext` 经 PropertySetHelper（表达式树+缓存）写 init-only 属性（[@INF-S14](./bdd.md#inf-s14)）；Kestrel 双端口 + `MaxRequestBodySize=1GB`。

## 已知问题

- **DEBUG 下不支持的 MAI_FILE 后缀静默忽略**（仅 `#if !DEBUG` 抛 `ArgumentException`）；MAI_FILE 指向不存在文件时无论编译模式都**静默回落**默认配置——本地易出现"配置没生效"错觉（[@INF-S4](./bdd.md#inf-s4)/[@INF-S5](./bdd.md#inf-s5)）。
- `InitConfigurationDirectory` 逻辑全被注释（死代码）；`configs_template/logger.json` 仅部署参考，不自动复制。
- `ExternalApiAttribute` 全仓无使用方（预留未接线）。
- `ConfigureRsaPrivate` 两分支路径拼接方式不一致（恒等，无害冗余）。
- `MoAI.Infra.Shared.csproj` 用 `Microsoft.NET.Sdk.Web`（类库带 Web 产物，历史遗留）。
- `configs/system.json` 存在强类型不识别的历史键（DBType/Wiki/Storage.LocalPath），示例与模型漂移。
