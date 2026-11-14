# AI 渠道与模型目录验证映射（TDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 证据：[模型目录 E2E](../../local-dev/model-catalog-e2e.mjs)

| 场景 | 验证物 | 结果（日期） |
|---|---|---|
| @MC-S2 | `local-dev/model-catalog-e2e.mjs` 在线同步；`dotnet build src/MoAI/MoAI.csproj --no-restore -c Release` | Release build PASS 0 error（2026-11-14）；在线 E2E 待本地 MoPlatform 切换到 S3 签名 URL 并确认 S3 从 MoAI 可达后执行 |
| @MC-S6 | 签名 URL 超时/403/404/5xx 后切换网络路径并重取 latest；SHA-256 与 size 使用同一指针；日志不含签名参数 | MoAI Release build PASS 0 error（2026-11-14）；网络故障注入待执行 |
| @MC-S3 | `local-dev/model-catalog-e2e.mjs` 无本地目录基线 | 已加入脚本，待后端 E2E 执行 |
| @MC-S4 | `local-dev/model-catalog-e2e.mjs` 损坏文件降级 | 已加入脚本，待后端 E2E 执行 |
| @MC-S5 | `local-dev/model-catalog-e2e.mjs` schema v1 元数据映射 | 已加入脚本，待后端 E2E 执行 |

在线目录同步依赖 MoPlatform 已发布导出；本地 schema 解析、门禁与文件变化场景由同一 E2E 脚本覆盖。
