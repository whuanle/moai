# 团队网关模块运维（SOP）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md)

## 日常操作

1. **业务系统全量接入**：团队管理 →「应用接入」→ 新建接入（名称/描述/授权外部应用，**无范围勾选**），凭 `moai-ac-` key 换 token（`POST /api/teamres/token`）或直接以 key 调 `/api/teamres` 任意团队外部接口。
2. **单资源接入**：在对应资源的功能页（应用工作台「ACP」「A2A」页、知识库/知识图谱「MCP」页）**开启对应对外功能开关**（开启时后端自动生成/复用该资源的 `moai-tr-` key——一资源一把，应用 ACP/A2A 共用其应用的一把；未开启功能不生成 key，手动建 key 400），复制「端点地址 + key」即可接入；资源 key 仅能访问绑定资源实例端点，不可换 token；关闭开关后端点与其资源 key 一律 403 feature_disabled，重新开启复用原 key。
3. **吊销**：删除应用接入（key 直连下一请求 401；外部 token 刷新即失败）；资源 key 支持重置（旧 key 立即失效、行 id 不变身份延续）、停用（401，可恢复）、删除（401）。
4. 团队接入 key（`moai-`）已下线：不再有密钥管理入口，存量 `moai-` key 一律 401，第三方统一改用应用接入 key 或资源 key。
5. 团队模型网关已下线（2026-10-29）：`/api/aigateway/*` 一律 404；模型仅供建模应用内部调用，模型网关能力后续由独立子系统承接。
6. scope 范围体系已退役（2026-11-04）：接入不再勾选任何范围，moai-ac- 全量访问；细粒度授权一律走资源 key 包围圈。

## 排障表

| 现象 | 结论与处置 |
|---|---|
| 调用 `/api/aigateway/*` 返回 404/405 | 团队模型网关已下线，端点已移除；模型访问改走应用对话/工作流，独立模型网关子系统后续另行提供 |
| 创建/更新接入请求带 `scopes` 字段返回 200 但无效果 | scopes 已退役被忽略；接入即全量，无需（也无法）配置范围 |
| 外部接口返回 401 且凭证以 `moai-`（非 `moai-ac-`/`moai-tr-`）开头 | 团队接入 key 已下线，改用应用接入 key 或资源 key |
| 资源 key 直连返回 401 | key 已重置/停用/删除；管理端重新复制最新 key 或重新启用（TR-S25/S26） |
| 资源 key 直连返回 403 `insufficient_scope` | 越出包围圈：该 key 只能访问绑定资源端点（wiki key 仅本库、app key 仅 ACP/A2A）；跨团队资源 404（[@GW-S11](./bdd.md#gw-s11)） |
| 资源 key 换 token 返回 400/401 | 资源 key 为纯直连凭证不可换 token（[@GW-S12](./bdd.md#gw-s12)）；换 token 仅认 moai-ac- 接入 key |
| 换 token 返回 401「密钥不匹配」 | 接入已设密钥，兑换必须携带 secret（`POST /api/teamres/token` 带 `secret`） |
| 刷新返回 401「应用接入已被删除」 | 接入已删除（吊销），重建接入重取 token（[@GW-S6](./bdd.md#gw-s6)） |

## 相关文档

- 外部接口凭证体系细节（凭证双轨/包围圈/端点清单/新增资源组清单/踩坑）：[外部接口与凭证体系细节总账](./external-api-scope-system.md)
- 资源 key 管理端与前端卡片：[../app/sdd.md](../app/sdd.md)「增量设计（2026-11-04：接入去范围化 + 统一团队资源 key）」与 [@TR-S10](../app/bdd.md#tr-s10) 起场景

## 验收流程

- 提交前：`dotnet build src/MoAI/MoAI.csproj` 0 error；`node local-dev/teamres-key-e2e.mjs` 全绿（需后端运行中）。
