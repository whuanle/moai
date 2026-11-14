# AI 渠道与模型目录设计规格（SDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 上游：[infra 配置](../infra/sdd.md) ｜ 证据：[模型目录 E2E](../../local-dev/model-catalog-e2e.mjs)

## 目标

MoAI 的渠道模型候选项按渠道供应商接口实时探测，模型元数据统一从 MoPlatform 管理的模型目录识别。MoAI 不再打包 OpenCode 的 `models.json`，也不自行维护多数据源聚合。

## 组件

| 组件 | 职责 |
|---|---|
| `SystemOptions.MoPlatform.Endpoint` | MoPlatform 服务根地址；默认 `https://anyai.wiki`，Development 配置为本地 `http://127.0.0.1:5100` |
| `POST /api/ai/model/catalog/sync` | 管理员触发拉取 MoPlatform latest 指针、下载其内容寻址文件、校验 size/SHA-256/schema v1，并原子保存至 `configs/models.json` |
| `AIModelCatalogService` | 读取 `configs/models.json`；解析 MoPlatform schema v1 并映射渠道、模型、模态、能力、上下文与价格；文件变化后自动重载 |

## 决策

- **D1 单一上游**：MoPlatform `/api/portal/catalog/models/latest` 是 MoAI 模型元数据唯一在线目录源；latest 返回的 `url` 是 10 分钟有效的 S3 预签名地址（旧版 `/static` 地址仍兼容），文件必须通过声明大小与 SHA-256 校验后才替换本地目录。
- **D2 分发格式**：消费 MoPlatform `schemaVersion=1` 的 `channels[]/models[]` 格式；现存供应商分组格式仍可由 `configs/models.json` 读取，以便本地迁移期间手动覆盖。
- **D3 无打包回退**：无 `configs/models.json` 时元数据目录为空；本地文件损坏时记录错误并按空目录处理，不因模型识别失败阻断供应商模型探测。
- **D4 原子更新**：先下载并验证完整内容，再写临时文件并替换 `configs/models.json`；替换完成后失效内存缓存。
- **D5 短时效签名地址重试**：MoPlatform latest 返回的 S3 签名下载地址有效 10 分钟；连接失败、超时、HTTP 403/404 或 5xx 后切换网络路径前重新获取 latest 指针，并始终使用新指针对应的 size/SHA-256 校验下载内容。环境代理、系统默认代理和直连逐级回退；日志不得输出签名查询参数。

## 已知边界

- MoPlatform 尚未导出目录时，latest 接口返回 404，在线同步不会覆盖本地文件。
- 模型目录同步为管理员显式操作；MoAI 不负责调度 MoPlatform 的多来源采集。
