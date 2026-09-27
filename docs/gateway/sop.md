# 团队网关模块运维（SOP）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 模块源码：[src/gateway/README.md](../../src/gateway/README.md)

## 日常操作

1. **建接入**：团队管理 →「应用接入」→ 新建接入，勾选功能范围（模型网关需勾 `model`；不传范围默认外部资源全量+对话）。
2. **调整范围**：范围决定接入能力——改范围后已签发外部 token 在下次刷新生效（见 [@GW-S6](./bdd.md#gw-s6)）；网关调用即时生效（access 即校验）。
3. **吊销**：删除接入（网关即时 401；外部 token 刷新即失败）。
4. 团队接入 key（`moai-`）已下线：不再有密钥管理入口，存量 `moai-` key 一律 401，第三方统一改用应用接入 key。

## 排障表

| 现象 | 结论与处置 |
|---|---|
| 网关返回 403 `insufficient_scope` | 接入未勾选 `model` 范围，改接入范围（[@GW-S3](./bdd.md#gw-s3)） |
| 网关返回 403 `permission_denied` | 路由 teamId 与接入归属团队不一致 |
| 网关/外部接口返回 401 且凭证以 `moai-`（非 `moai-ac-`）开头 | 团队接入 key 已下线，改用应用接入 key |
| 外部接口 403「未勾选知识库读/写权限」 | token 来源接入缺 `wiki_read`/`wiki_write`，改接入范围后刷新 token |
| 外部接口 403「未勾选知识图谱读/写权限」 | token 来源接入缺 `kg_read`/`kg_write`，改接入范围后刷新 token（[@GW-S9](./bdd.md#gw-s9)） |
| 刷新返回 401「应用接入已被删除」 | 接入已删除（吊销），重建接入重取 token（[@GW-S6](./bdd.md#gw-s6)） |

## 相关文档

- 外部接口范围体系细节（scope 位表/端点清单/缓存/新增资源组清单/踩坑）：[外部接口与范围体系细节总账](./external-api-scope-system.md)

## 验收流程

- 提交前：`dotnet build src/MoAI/MoAI.csproj` 0 error；`node local-dev/team-apikey-scope-e2e.mjs`、`node local-dev/gateway-e2e.mjs` 全绿（需后端运行中）。
