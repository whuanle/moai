# OAuth 连接器（OauthConnect，OC）操作手册（SOP）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md)（验收场景编号） ｜ [TDD](./tdd.md)（自动化映射） ｜ [SOP](./sop.md)

## 1. 日常操作

| 操作 | 步骤 | 对应场景 |
|---|---|---|
| 接入第三方登录渠道 | ① 在提供方（飞书/钉钉/自建 OIDC）创建应用，回调地址配 `{WebUI}/oauth_login`；② admin 登录前端 →「OAuth 连接器」→「新建」；③ 填名称（≤50、全局唯一）/提供商/Key/Secret/图标；④ 保存后在登录页确认新渠道按钮 | [@OC-S5](./bdd.md#oc-s5)~[@OC-S7](./bdd.md#oc-s7)、[@OC-S18](./bdd.md#oc-s18) |
| Custom 渠道 | 「发现端点」必填（OIDC discovery URL，须可被**后端**访问）；保存时后端实时拉取解析授权地址 | [@OC-S7](./bdd.md#oc-s7) |
| 编辑连接器 | 提供商不可改；Secret 留空=保持不变；Custom 换发现端点后授权地址自动重解析 | [@OC-S13](./bdd.md#oc-s13)、[@OC-S23](./bdd.md#oc-s23) |
| 删除连接器 | 二次确认后软删除；**渠道立即从登录页消失**，已绑定用户的历史记录保留 | [@OC-S16](./bdd.md#oc-s16)、[@OC-S19](./bdd.md#oc-s19) |

## 2. 排障

| 现象 | 原因 | 处理 |
|---|---|---|
| 创建 Custom 连接器 500 | 发现端点不可达/非法规格（缺陷：未转业务异常） | 确认 wellKnown 可被后端访问（注意内网/代理）且为标准 discovery 文档；见 [@OC-S11](./bdd.md#oc-s11) |
| 保存 400「认证名称已存在」 | 名称与其他连接器重复（含软删除记录） | 更换名称，见 [@OC-S8](./bdd.md#oc-s8) |
| 登录页不显示某渠道 | 连接器已被软删除 | 重新创建，见 [@OC-S19](./bdd.md#oc-s19) |
| 第三方登录提示"第三方接口错误" | 提供方接口异常或 Key/Secret 失效 | 核对凭据；查后端日志 Get openid error |
| 跳转地址 400「不合法的跳转地址」 | redirectUri 与 `SystemOptions.WebUI` 不同 Host | 检查 WebUI 配置（见 [../infra/sop.md](../infra/sop.md)） |
| 接口 403 | 当前登录人不是管理员 | 换 admin 账号，见 [@OC-S1](./bdd.md#oc-s1) |
| PUT 固定 400（历史） | **已修复**（2026-09-02 终审实测 200） | 若复现回查 `UpdateOAuthConnectionCommand.Validate`，见 [@OC-S12](./bdd.md#oc-s12) |

## 3. 验收流程（发布前）

1. 自动化：跑 [TDD 回归命令](./tdd.md)（audit-345.mjs，OC 相关 6 条）。
2. HTTP 手动走查：member token 访问四个端点应 403、匿名应 401（[@OC-S1](./bdd.md#oc-s1)/[@OC-S2](./bdd.md#oc-s2)）；创建 Custom 缺 wellKnown 应 400（[@OC-S9](./bdd.md#oc-s9)）；更新/删除全零 Guid 应 400（[@OC-S15](./bdd.md#oc-s15)/[@OC-S17](./bdd.md#oc-s17)）。
3. 浏览器走查：admin 进入 /oauthconnect 核对 [@OC-S21](./bdd.md#oc-s21)~[@OC-S23](./bdd.md#oc-s23)；member 直访被重定向（[@OC-S24](./bdd.md#oc-s24)）。

## 4. 历史验收存档（L3 证据，保留原始记录）

- **2026-09-01 初版（回溯整理）**：功能此前已实现；旧 sop 内联 curl 验收脚本（列表/建飞书/重名/缺 wellKnown/删除/渠道联动/member 403）全部通过。
- **2026-09-01 第二轮·全系统深度测试**：深度 API 68/68；发现并修复「oauthconnect PUT 恒 400」「各 BusinessException 补 StatusCode（此前裸 500）」。
- **2026-09-02 第三轮·OAuth 全链路**：本地模拟 OIDC Provider 完成 OAuth 12/12（创建/授权地址/302 回跳/待绑定/注册/直通/绑定/解绑）+ 浏览器全流程（含 oauthconnect 编辑页）。
- **2026-09-02 终审抽检**：[audit-345.mjs](../../local-dev/audit-345.mjs) 14/14，其中 OC 6 条全过；确认 PUT 缺陷修复（实测 200）。

## 5. 变更记录

| 日期 | 变更 |
|---|---|
| 2026-09-01 | 初版（回溯整理）；两轮验收（见存档） |
| 2026-09-02 | PUT 恒 400 缺陷修复并实测通过；按 [DOC-STANDARD](../DOC-STANDARD.md) 重构四件套 |
