# 认证（Auth）操作手册（SOP）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md)（验收场景编号） ｜ [TDD](./tdd.md)（自动化映射） ｜ [SOP](./sop.md)

## 1. 日常运维

| 事项 | 操作 | 对应场景 |
|---|---|---|
| 解锁被锁用户 | `docker exec moai-redis redis-cli DEL "moai:login:fail:{userName}"`（键带库默认 `moai:` 前缀；不处理则 5 分钟自动解锁） | [@AUTH-S3](./bdd.md#auth-s3) |
| 撤销全部登录态 | 无黑名单机制。禁用账号即可下一请求拦截（[../user-management/sop.md](../user-management/sop.md)）；彻底吊销需轮换 RSA 密钥（删 `configs/rsa_private.key` 重启，**所有 token 失效**） | [@AUTH-S12](./bdd.md#auth-s12) |
| 排查登录失败 | 后端日志 `User login.` 事件；401 连发先查 Redis 失败计数再查密码 | [@AUTH-S2](./bdd.md#auth-s2) |
| 第三方登录不可用 | 用户管理→第三方登录页检查连接配置；oauth_login 500 多为第三方接口错误，查日志 `Get openid error` | [@AUTH-S15](./bdd.md#auth-s15) |
| 锁定机制自检 | 跑 [local-dev/auth-lockout-check.mjs](../../local-dev/auth-lockout-check.mjs)：5 错→403→删 key→恢复 | [@AUTH-S3](./bdd.md#auth-s3) |

## 2. 密钥与有效期

- RSA 私钥 `{AppPath}/configs/rsa_private.key`，首启自动生成 2048 位；公钥经 `/api/common/serverinfo` 下发，前端每次登录前现取（不缓存公钥）。
- access 30 分钟（**DEBUG 编译 7 天**——本地"token 一直不过期"即此原因）；refresh 7 天；前端每 60 秒自查并静默续期。
- 登录响应 `expiresIn` 是**毫秒时间戳**（now+30min），前端 `jwt.ts` 按毫秒解析。

## 3. 排障

| 现象 | 原因 | 处理 |
|---|---|---|
| 前端登录 401 但密码确认正确 | RSA 公钥过期/服务端换钥/多实例私钥不一致 | 刷新页面重拉 serverinfo；核对各实例 rsa_private.key |
| 登录 403 次数过多 | 5 次失败锁定 | 等 5 分钟或删 Redis key（第 1 节） |
| oauth 回调报「不合法的跳转地址」 | redirectUrl host ≠ WebUI host | 修正 SystemOptions.WebUI（注：该校验当前因 Controller 未回填 redirectUrl 不生效，见 [SDD 已知问题](./sdd.md)，[@AUTH-S14](./bdd.md#auth-s14)） |
| 第三方授权后提示绑定过期 | 临时绑定标识超 10 分钟 | 重新发起第三方登录（[@AUTH-S18](./bdd.md#auth-s18)） |

## 4. 验收流程（发布前）

1. 自动化：跑 [TDD 回归命令](./tdd.md)（E2E 34/34 + lockout 脚本 + build/typecheck/lint/test 全绿）。
2. 手动走查：登录/注册/OAuth 回调页核对 [@AUTH-S20](./bdd.md#auth-s20) ~ [@AUTH-S23](./bdd.md#auth-s23)；curl 复核 [@AUTH-S14](./bdd.md#auth-s14) 缺陷现状。
3. 历史记录见下「历史验收存档」。

## 5. 历史验收存档（L3 证据，保留原始记录）

- **2026-09-01 as-built 自检**：E2E 34/34（`node local-dev/user-management-e2e.mjs`，含注册/登录/刷新正负向）；lockout 脚本（本轮新建）5 错→403、删 key→恢复 200；`dotnet build` 0 错误。
- **2026-09-01 核对会话补充自检**：admin + node crypto PKCS1 加密登录 200 / 错误密码 401；弱密码 "1234" → 400；重复用户名 → 409；有效 refreshToken → 200 换新对；accessToken 冒充 → 401。**推翻早前记录**：`oauth_prividers?redirectUrl=http://evil.com` 实测 **200（非 400）**，确认 Controller 空参构造死代码缺陷；无参 200。
- **2026-09-01 轮 11**：auth 前端链路独立验证（typecheck/lint/test 绿）。
- **2026-09-02 第三轮 OAuth 全链路**：本地 mock OIDC Provider 下 OAuth 12/12（创建/授权地址/302 回跳/待绑定/注册/直通/绑定/解绑），修复 OAuthRegisterCommandHandler 占位手机号唯一索引冲突；详见 [../user-management/sop.md](../user-management/sop.md) 存档。

## 6. 变更记录

| 日期 | 变更 |
|---|---|
| 2026-09-01 | 初版（轮 2，as-built）；两轮自检（见存档） |
| 2026-09-02 | 随第三轮验收更新 OAuth 证据 |
| 2026-09-01 | 按 [DOC-STANDARD](../DOC-STANDARD.md) 重构：场景编号化（@AUTH-S1~S23）、四件互链、职责瘦身 |
