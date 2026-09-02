# 公共领域（Common）设计规格（SDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 上游：[../infra/sdd.md](../infra/sdd.md)（RSA/配置基础设施） ｜ 下游：[../auth/sdd.md](../auth/sdd.md)、[../account/sdd.md](../account/sdd.md)（rsaPublic/serviceUrl 消费方） ｜ 证据：curl/node 自检命令见 [TDD](./tdd.md)
> 规范：[../DOC-STANDARD.md](../DOC-STANDARD.md)。行为场景见 BDD（@COM-Sxx），本文不重复。

## 目标

登录前置的服务器公开信息与全局唯一 id 生成。**serverinfo 匿名**（`[AllowAnonymous]`）；**build_guid 需登录**（无 AllowAnonymous，被全局路由约定自动加 `[Authorize]`）。代码 `src/common/`（CommonController，`[Route("/common")]`，路由统一加 `/api` 前缀）。

## 端点与字段语义

| 方法 | 路由 | 鉴权 | 说明 |
|---|---|---|---|
| GET | `/common/serverinfo` | 匿名 | name / serviceUrl / publicStoreUrl / rsaPublic / maxUploadFileSize |
| GET | `/common/build_guid` | 需登录 | 返回 `Guid.CreateVersion7()`（时间有序 UUID） |

| 字段 | 来源 | 消费方 |
|---|---|---|
| `rsaPublic` | `IRsaProvider.GetPublicKey()`：Base64(SPKI/DER) 公钥（非 PEM 文本，2048 位约 392 字符） | 前端登录/注册/改密前加密密码（`utils/rsa.ts` JSEncrypt PKCS1） |
| `serviceUrl` | `SystemOptions.Server` | 前端拼接接口/静态地址（`{serviceUrl}/static/{osskey}`） |
| `publicStoreUrl` | `new Uri(new Uri(Server), "static")`（`StaticRoutePrefix="/static"` 相对拼接） | 公有文件直链前缀（storage 静态中转，见 [../storage-file-layout.md](../storage-file-layout.md)） |
| `maxUploadFileSize` | `SystemOptions.MaxUploadFileSize`（默认 100MB=104857600 字节） | 预留；前端 store 未保留该字段，暂无消费方 |

## 组件

```
src/common/
├── MoAI.Common.Shared/  Query/Response 模型（QueryUserInfoCommandResponse 为遗留，见已知问题）
├── MoAI.Common.Core/    QueryServerInfoCommandHandler（注入 SystemOptions + IRsaProvider + DatabaseContext）
└── MoAI.Common.Api/     CommonController（仅注入 IMediator；serverinfo 匿名放行，build_guid 走 JWT 鉴权）
ui/src/                  store/app.ts（zustand persist 缓存 serverinfo）
```

## RSA 密钥机制（rsaPublic 的来源）

密钥对**首启自动生成**（实现在 infra，见 [../infra/sdd.md](../infra/sdd.md)）：

- 私钥路径 `{AppPath}/configs/rsa_private.key`（本地调试 `src/MoAI/bin/Debug/net10.0/configs/`，容器内 `/app/configs/`）；文件不存在时 `RSA.Create(2048)` 并 `ExportPkcs8PrivateKeyPem()` 写入，注册单例 `IRsaProvider`。
- 公钥导出 = `ExportSubjectPublicKeyInfo()` 的 Base64。
- **同一密钥三用**：本模块下发公钥、auth/account 密码解密、JWT RS256 签名。轮换私钥 = 所有已发 token 与前端缓存公钥同时失效（无黑名单时的唯一全量吊销手段）。
- 一致性：从运行时私钥文件派生的 SPKI Base64 与 `rsaPublic` 逐字节一致（[@COM-S3](./bdd.md#com-s3)）。

## 关键决策

1. serverinfo 是**前端启动依赖**：首次调用后缓存在 zustand（persist），登录/注册前复用；公钥随轮换而变，前端不清缓存（轮换需用户刷新）。
2. build_guid 用 GUID v7（毫秒时间戳前缀），供前端创建实体前预取 id；不承诺连续性，仅唯一性；当前无业务调用方。
3. 上传体积上限：服务端下发 `maxUploadFileSize`（字节），与 Kestrel `MaxRequestBodySize`（1GB）取较小值。

## 已知问题

- `QueryUserInfoCommandResponse.cs` 存在于 Shared/Response 但无对应端点（疑似遗留，userinfo 实际由 account 领域承担）。
- `QueryServerInfoCommandHandler` 注入 `DatabaseContext` 但 Handle 内未使用。
- `maxUploadFileSize` 单位为字节但注释写 "100MB" 字面量，配置改名易踩坑；且前端 store 未保留该字段，实际无人消费。
- 前端上传入口未统一接入体积预校验（[@COM-S7](./bdd.md#com-s7)，计划见 ui/docs 轮 12）。
- build_guid 已登录用户无频控（无限流防护，备查）。
