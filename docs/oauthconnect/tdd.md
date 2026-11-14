# OAuth 连接器（OauthConnect，OC）验证映射（TDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md)（场景定义） ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md)
> 本文只做「场景 → 验证物 → 结果」映射；场景语义见 BDD 编号，设计依据见 SDD。

## 场景映射表

| 场景 | 验证物 | 结果（日期） |
|---|---|---|
| @OC-S3 | [local-dev/audit-345.mjs](../../local-dev/audit-345.mjs)（"5-connections GET 200"） | PASS（2026-09-02） |
| @OC-S4、@OC-S16 | 同上（"5-DELETE 200 且软删除生效"） | PASS（2026-09-02） |
| @OC-S5 | 同上（"5-create feishu 200" + authorizeUrl=飞书固定地址断言） | PASS（2026-09-02） |
| @OC-S8 | 同上（"5-重名创建被拒"） | PASS（2026-09-02） |
| @OC-S12 | 同上（"5-PUT 200"，缺陷修复后实测） | PASS（2026-09-02） |
| @OC-S7、@OC-S18 ~ @OC-S20 | @manual 本地模拟 OIDC Provider 全链路 12/12（记录见 [../user-management/sop.md](../user-management/sop.md) 历史验收存档·第三轮） | PASS（2026-09-02） |
| @OC-S1、@OC-S2、@OC-S6、@OC-S9 ~ @OC-S11、@OC-S13 ~ @OC-S15、@OC-S17 | @manual HTTP 走查（[SOP 第 3 节](./sop.md)） | PASS（2026-09-01，第二轮深度 API 68/68 互证） |
| @OC-S21 ~ @OC-S24 | @manual 浏览器走查（[SOP 第 3 节](./sop.md)） | PASS（2026-09-01/02，全页面回归） |
| @OC-S25 | [ImageSquareCropModal.test.tsx](../../ui/src/components/__tests__/ImageSquareCropModal.test.tsx)（组件冒烟 3/3 + `clampCropOffset` 全向拖动约束 3/3）+ @manual 浏览器走查（[SOP 第 3 节](./sop.md)） | PASS（2026-10-27）；交互走查待回归 |
| @OC-S26 ~ @OC-S28 | [local-dev/oauth-weixinwork-smoke.mjs](../../local-dev/oauth-weixinwork-smoke.mjs)（创建/AgentId 校验与回显/固定授权地址/登录页 URL 拼接/回调地址 redirectUri/更新同步/删除清理）+ @manual 浏览器走查（表单联动 [@OC-S22](./bdd.md#oc-s22)） | PASS 18/18（2026-10-27） |
| @OC-S29 ~ @OC-S30 | @manual（需真实飞书/企业微信凭证完成一次登录绑定，改动见 2026-10-28 证据摘要） | 待验收 |
| @OC-S31 | local-dev/internal-app-access-e2e.mjs（IA-S3a 创建隐藏连接器、IA-S3b/c 管理端 isVisible 回显、IA-S3d/e 登录页列表排除隐藏保留可见、IA-S14 个人中心绑定列表排除） | 见 2026-11-13 增量证据 |
| @OC-S32 | [OauthConnect.test.tsx](../../ui/src/pages/oauthconnect/__tests__/OauthConnect.test.tsx)（列表状态列切换并提交更新、编辑窗不再承担该开关） | PASS 5/5（2026-11-14） |

## 证据摘要（2026-10-28，渠道用户识别联动：union_id 列 + 企微原生登录路由）

- `user_oauth_connection` 增 `union_id`（varchar(100) 可空）：实体/配置/索引 + `DatabasPostgresModule` 幂等补列与索引（存量库启动自动补齐）；`OpenIdUserProfile` 增 `UnionId`；飞书取资料时带出（`FeishuUserInfo.UnionId`），注册/绑定三处落库点写入；登录匹配仍按 Sub，不变。
- `OAuthUserProfileService.GetProfileAsync` 接线 WeixinWork → 既有 `GetOpenIdFromWeixinWorkAsync`（Sub=userid；原为死代码，企微连接器登录此前落入 custom OIDC 分支实际不可用）。
- `dotnet build src/MoAI/MoAI.csproj -o .builds/cum` → 0 error。消费方（渠道对话反查绑定归属用户）见 [../feishu/sdd.md](../feishu/sdd.md)。

## 证据摘要（2026-11-13，隐藏连接器 is_visible）

- `oauth_connection` 增 `is_visible boolean NOT NULL DEFAULT true`：实体/EF 配置/创建/更新/列表回显全链路；`DatabasPostgresModule` 启动幂等补列，存量库脚本 [asserts/oauth-connection-visible.sql](../../asserts/oauth-connection-visible.sql)。
- 露出面过滤：`QueryAllOAuthPrividerCommandHandler`（登录页）与 `QueryUserBoundAccountsCommandHandler`（个人中心）仅取 `is_visible=true`；`ChannelUserMatchService` 与访问用户 token 换算不按可见性过滤（隐藏连接器仍参与身份映射）。
- 前端：连接器表单新增「登录页显示」开关（默认开），列表隐藏行带「隐藏」Tag，详情回显；Kiota 客户端已 syncapi 重生成。该列表标记于 2026-11-14 改为独立可切换状态列，见 [@OC-S32](./bdd.md#oc-s32)。
- 验证：`node local-dev/internal-app-access-e2e.mjs` → **PASS 56 / FAIL 0**（IA-S3a 创建隐藏连接器、IA-S3b/c 管理端回显、IA-S3d/e 登录页列表、IA-S3f 旧客户端更新未携带 isVisible 保持隐藏、IA-S14 个人中心不露出）；`npm run typecheck`/`lint` 0 error、UI vitest 809/809。

## 回归命令

```bash
node local-dev/audit-345.mjs   # 需后端运行于 :5210；脚本含轮3/4 断言，OC 相关 6 条
node local-dev/oauth-weixinwork-smoke.mjs   # 企业微信连接器冒烟 17 断言（@OC-S26~S28 + @AUTH-S13/@AUTH-S25 URL 拼接）
```

## 覆盖率说明

- 自动化 5 项（列表/软删除/建飞书/重名/PUT/删）覆盖核心写路径；创建 Custom、钉钉、401/403 门禁为手动（需可达的外部 OIDC 端点与 member 账号）。企业微信连接器 CRUD 与 URL 拼接已自动化（假凭证冒烟），真实扫码登录链路无法离线自动化（[@AUTH-S25](../auth/bdd.md#auth-s25) @manual）。
- 已知未覆盖：Custom 发现端点不可达的 500 路径（[@OC-S11](./bdd.md#oc-s11)，缺陷记录未修复，不自动化）。
