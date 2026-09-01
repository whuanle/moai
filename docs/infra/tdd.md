# infra 基础设施（INF）验证映射（TDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md)（场景定义） ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md)
> 本文只做「场景 → 验证物 → 结果」映射。infra 无独立单测工程：验证物为构建/配置命令、运行实例（本地 :5210，MAI_FILE 指向 local-dev/system.local.json）与代码走查；HTTP 行为在 auth/user-management 轮 34/34 E2E 互证。

## 场景映射表

| 场景 | 验证物 | 结果（日期） |
|---|---|---|
| @INF-S1 | 代码走查 `ImportSystemConfiguration` 回落分支 + 运行实例默认部署路径一致 | PASS（2026-09-01） |
| @INF-S2 | `curl -s http://127.0.0.1:5210/api/common/serverinfo` | PASS（2026-09-01，`serviceUrl=http://127.0.0.1:5210`，证明 MAI_FILE 覆盖仓库默认 5000） |
| @INF-S3 | 代码走查 `src/MoAI/MainExtensions.Configuration.cs` + `Directory.Packages.props`（NetEscapades.Yaml 3.1.0 / Configuration.Ini） | PASS（2026-09-01） |
| @INF-S4、@INF-S5 | 代码走查（`#if !DEBUG` 分支与文件存在性回落） | PASS（2026-09-01，缺陷记录如实登记） |
| @INF-S6 | 代码走查（`Get<T>` 失败抛 FormatException） | PASS（2026-09-01） |
| @INF-S7 | 绑定语义走查 + `configs/system.json` 历史键核对 | PASS（2026-09-01） |
| @INF-S8、@INF-S9 | 代码走查 `ConfigureRsaPrivate` + 运行实例公钥可获取 | PASS（2026-09-01） |
| @INF-S10 | 同 @INF-S2 响应体 `rsaPublic`（`MIIBIjANBgkqhkiG9w0BAQ…` SPKI/2048 位） | PASS（2026-09-01） |
| @INF-S11 | 设计推演（同一密钥兼管解密与 JWT 签名；未做破坏性实测） | 未执行（破坏性操作，见 SOP 第 2 节） |
| @INF-S12、@INF-S13 | 代码走查 `InfraCoreModule` TypeFilter（含泛型防继承判断） | PASS（2026-09-01） |
| @INF-S14、@INF-S15 | 代码走查 `PropertySetHelper` + auth/user-management 轮 E2E 互证 | PASS（2026-09-01） |
| @INF-S16、@INF-S17 | 代码走查 `InfraExternalHttpModule`（9 客户端/统一 RefitSettings/拦截器打码） | PASS（2026-09-01） |
| @INF-S18 | 代码走查 `IOAuthClientFactory` + oauthconnect 轮 OIDC 模拟 12/12 互证 | PASS（2026-09-01/02） |
| @INF-S19、@INF-S20 | 代码走查（Yitter/AESProvider） | PASS（2026-09-01） |
| @INF-S21 | 代码走查 `PBKDF2Helper`（10000 迭代/128B）+ auth 轮登录 E2E 互证 | PASS（2026-09-01） |
| @INF-S22 | 对照 `configs/system.json` 与运行实例（amqp://127.0.0.1:5672，容器 moai-rabbitmq healthy）+ `AddMaomiMQ` 参数走查 | PASS（2026-09-01） |
| 构建 | `dotnet build src/MoAI/MoAI.csproj`（仓库根） | PASS（2026-09-01，0 错误；58 个存量 NU1507 警告与 infra 无关，耗时 3.39s） |
| 模块图 | `grep -n "InjectModule" src/MoAI/MainModule.cs src/infra/MoAI.Infra.Core/InfraCoreModule.cs` | PASS（2026-09-01，MainModule→InfraCoreModule→Configuration+ExternalHttp） |

## 回归命令

```bash
dotnet build src/MoAI/MoAI.csproj                                # 期望 0 错误（NU1507 为存量）
curl -s http://127.0.0.1:5210/api/common/serverinfo | python3 -m json.tool
# 期望：name=MoAI、serviceUrl 与 MAI_FILE 一致、rsaPublic 非空
grep -n "InjectModule" src/MoAI/MainModule.cs src/infra/MoAI.Infra.Core/InfraCoreModule.cs
```

## 覆盖率说明

- 全部场景为 @manual（命令/走查类）；无独立单测工程，HTTP 侧行为依赖下游模块 E2E 互证。
- 已知未执行：密钥轮换破坏性实测（@INF-S11）；yaml/conf 双格式等价性实测（仅走查）。
