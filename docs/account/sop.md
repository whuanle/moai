# 账号自助（Account Self-Service）操作手册（SOP）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md)（验收场景编号） ｜ [TDD](./tdd.md)（自动化映射） ｜ [SOP](./sop.md)
> 管理员治理操作（禁用、重置他人密码等）见 [../user-management/sop.md](../user-management/sop.md)。

## 1. 能力与入口

| 能力 | 入口（前端 /account「账号设置」） | 对应场景 |
|---|---|---|
| 查看/修改资料 | 「基本资料」卡 | [@ACC-S1](./bdd.md#acc-s1)、[@ACC-S4](./bdd.md#acc-s4) |
| 自助改密 | 「修改密码」卡（8-20 位含字母数字，旧密码必填） | [@ACC-S7](./bdd.md#acc-s7) |
| 上传头像 | 「上传头像」按钮（≤5MB 图片） | [@ACC-S13](./bdd.md#acc-s13)、[@ACC-S26](./bdd.md#acc-s26) |
| 绑定/解绑第三方 | 「第三方账号」卡（弹窗授权） | [@ACC-S14](./bdd.md#acc-s14)、[@ACC-S22](./bdd.md#acc-s22) |

## 2. 排障

| 现象 | 原因 | 处理 |
|---|---|---|
| 改密提示「原密码错误」 | 旧密码填错或密文损坏 | 核对旧密码；忘记密码联系管理员重置（见 [../user-management/sop.md](../user-management/sop.md)），[@ACC-S8](./bdd.md#acc-s8) |
| 提示「第三方授权跳转登录已过期」 | 临时绑定标识超 10 分钟 | 关闭弹窗重新发起绑定，[@ACC-S20](./bdd.md#acc-s20) |
| 提示「第三方账号已被其它账号绑定」 | 该第三方身份已绑其它站内账号 | 换身份或先在对方账号解绑，[@ACC-S16](./bdd.md#acc-s16) |
| 提示「用户已绑定过其它账号」 | 同一认证方式下已绑不同身份 | 先解绑再绑定，[@ACC-S17](./bdd.md#acc-s17) |
| 改完资料页头没变 | 前端 store 滞后（后端缓存已失效） | 页面自动刷新用户信息；仍异常则手动刷新 |
| 上传头像无反应 | 文件非图片或超 5MB 被前端拦截 | 更换合规图片 |
| 提示头像文件不存在或未完成上传 | objectKey 未在 file 表登记（直传未完成/伪造） | 重试完整上传流程，[@ACC-S12](./bdd.md#acc-s12) |
| 请求 403「账号已被禁用」 | 账号被管理员禁用 | 联系管理员，[@ACC-S3](./bdd.md#acc-s3) |

## 3. 手动调试片段（密文构造）

新旧密码均需用服务器公钥加密（PKCS1）后提交；登录取 token 与加密助手：

```bash
BASE=http://127.0.0.1:5210
RSA=$(curl -s $BASE/api/common/serverinfo | python3 -c 'import sys,json;print(json.load(sys.stdin)["rsaPublic"])')
enc() { node -e "
const c=require('crypto');
const k=c.createPublicKey({key:Buffer.from('$RSA','base64'),format:'der',type:'spki'});
console.log(c.publicEncrypt({key:k,padding:c.constants.RSA_PKCS1_PADDING},Buffer.from('$1')).toString('base64'));"; }
TOKEN=$(curl -s -X POST $BASE/api/auth/login -H 'Content-Type: application/json' \
  -d "{\"userName\":\"admin\",\"password\":\"$(enc abcd123456)\"}" \
  | python3 -c 'import sys,json;print(json.load(sys.stdin)["accessToken"])')
```

## 4. 验收流程（发布前）

1. 自动化：`node local-dev/audit-345.mjs`（覆盖 [@ACC-S1](./bdd.md#acc-s1)/[@ACC-S2](./bdd.md#acc-s2)/[@ACC-S4](./bdd.md#acc-s4)/[@ACC-S7](./bdd.md#acc-s7)~[@ACC-S11](./bdd.md#acc-s11)/[@ACC-S20](./bdd.md#acc-s20)/[@ACC-S21](./bdd.md#acc-s21)/[@ACC-S23](./bdd.md#acc-s23)）。
2. 手动走查：浏览器进入 /account，核对 [@ACC-S24](./bdd.md#acc-s24) ~ [@ACC-S28](./bdd.md#acc-s28) 与 [@ACC-S13](./bdd.md#acc-s13)。
3. 记录写入下「历史验收存档」。

## 5. 历史验收存档（L3 证据，保留原始记录）

- **2026-09-01 回溯初验**：按 SOP 内置 curl 脚本（登录取 token → userinfo / 改资料复查 / 改密错误旧密 400 → 正确旧密 200 换新登录 / 弱密码 400 / 空对象键 400 / 绑定列表 / 解绑未绑定 404 / 过期临时标识 403 / 未登录 401）对 `127.0.0.1:5210` 核对路由、错误码与文案，与源码一致；该脚本后收敛为 [local-dev/audit-345.mjs](../../local-dev/audit-345.mjs)。RSA 密文异常分支同轮通过。
- **2026-09-01 第二轮深度回归**（存档 [../user-management/sop.md](../user-management/sop.md)）：68/68；本模块相关修复：禁用中间件 fail-open→按用户态 403；`update_userinfo` 超长昵称 500→模型校验；`avatar` 伪造 objectKey→file 表校验 404。
- **2026-09-02 第三轮**（存档同上）：OAuth 12/12（绑定/解绑/待绑定/直通）+ 浏览器全页面回归（账号设置页，成员视角）通过。
- 遗留观察：改密后其他会话存量 token 不吊销（见 [SDD 已知问题](./sdd.md)）。

## 6. 变更记录

| 日期 | 变更 |
|---|---|
| 2026-09-01 | 初版（回溯整理）；同日按 [DOC-STANDARD](../DOC-STANDARD.md) 重构：场景编号化（@ACC-S1~S28）、四件互链、职责瘦身 |
