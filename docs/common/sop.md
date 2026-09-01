# 公共领域（Common）操作手册（SOP）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md)（验收场景编号） ｜ [TDD](./tdd.md)（自动化映射） ｜ [SOP](./sop.md)

## 1. serverinfo 排查

| 现象 | 原因 | 处理 | 场景 |
|---|---|---|---|
| 前端登录报「密码解密失败」 | 多实例私钥不一致或前端缓存旧公钥 | 刷新页面；检查各实例 `configs/` 目录 | [@COM-S2](./bdd.md#com-s2) |
| serviceUrl/publicStoreUrl 不对 | 配置 `MoAI:Server` 有误 | 改配置（本地经 MAI_FILE 注入）重启 | [@COM-S1](./bdd.md#com-s1) |
| maxUploadFileSize 需调整 | 默认 100MB（字节） | 配置 `MoAI:MaxUploadFileSize`；实际上限与 Kestrel `MaxRequestBodySize`（1GB）取小 | [SDD 字段表](./sdd.md) |

## 2. RSA 密钥运维

- 私钥位置 `{应用基目录}/configs/rsa_private.key`（本地 `src/MoAI/bin/Debug/net10.0/configs/`，容器 `/app/configs/`）；**首启自动生成** 2048位 PKCS8 PEM，无需人工预置。
- 多实例部署：必须挂载同一份 key，否则各实例下发不同公钥、互验 token 失败。
- 轮换密钥：停服 → 删除/替换 key → 启动。**副作用**：所有已发 JWT 与前端缓存公钥同时失效，用户需刷新重登；auth 无黑名单，这是唯一全量吊销手段（见 [../auth/sop.md](../auth/sop.md)）。
- 一致性自检（[@COM-S3](./bdd.md#com-s3)）：

```bash
node -e "
const c=require('crypto'),fs=require('fs');
const pub=c.createPublicKey(fs.readFileSync('src/MoAI/bin/Debug/net10.0/configs/rsa_private.key','utf8'))
  .export({type:'spki',format:'der'}).toString('base64');
console.log(pub)"  # 应与 GET /api/common/serverinfo 的 rsaPublic 完全一致
```

## 3. build_guid 使用约定

- **需登录态**（匿名 401，[@COM-S4](./bdd.md#com-s4)）；GUID v7 时间有序，适合业务主键预生成；不承诺连续性，仅唯一性（[@COM-S5](./bdd.md#com-s5)）。
- 已登录用户无频控（暂无限流防护，备查）。

## 4. 验收流程（发布前）

1. 命令验证：跑 [TDD 回归命令](./tdd.md)（serverinfo 五字段、build_guid 401/200、build 0 错误）。
2. 手动走查：登录页网络面板核对 [@COM-S6](./bdd.md#com-s6)；[@COM-S7](./bdd.md#com-s7) 按缺陷现状记录核对。
3. 记录写入下「历史验收存档」。

## 5. 历史验收存档（L3 证据，保留原始记录）

- **2026-09-01（轮 6）自检**：T1 `curl /api/common/serverinfo` 200 五字段齐全——**publicStoreUrl 实测 `/static`（修正初稿 `/statics` 笔误）**；T3 build_guid 匿名 401、带 token 200（`01a05dbd-b583-7308-…` v7）两次值不同——**修正早前"匿名可访问"的误记，该接口非匿名**；T2 以 rsaPublic PKCS1 加密 admin 密码登录成功（取到 accessToken）；T4 `dotnet build` 0 错误（58 个既有警告非本模块引入）；T5 私钥派生 SPKI Base64 与 rsaPublic 392 字符完全一致。
- **2026-09-01（轮 1 关联记录）**：`node local-dev/user-management-e2e.mjs` 34/34 PASS（间接覆盖 serverinfo→登录加密链路）。

## 6. 变更记录

| 日期 | 变更 |
|---|---|
| 2026-09-01 | 初版（轮 6，as-built）；同轮补强：RSA 密钥运维章节、修正 publicStoreUrl 笔误、密钥三用说明 |
| 2026-09-01 | 按 [DOC-STANDARD](../DOC-STANDARD.md) 重构：场景编号化（@COM-S1~S7）、四件互链、职责瘦身 |
