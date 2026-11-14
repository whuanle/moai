# 在线插件市场验证映射（TDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 证据：`tests/MoAI.Infra.Tests`、`dotnet build src/MoAI/MoAI.csproj`

| 场景 | 验证物 | 结果（日期） |
|---|---|---|
| @PLGM-S1 | `tests/MoAI.Infra.Tests/MoPlatformClientFingerprintTests#LoadOrCreate_ShouldPersistAndReuseFingerprint / LoadOrCreate_ShouldReplaceMalformedFingerprint` | PASS 2/2（2026-11-14） |
| @PLGM-S2 | MoPlatform `local-dev/plugin-e2e.mjs#PLUG-S50`；MoAI `PluginMarketplaceService.QueryCatalogAsync` 代码走查 | 平台 e2e 已加入待执行；MoAI 代码走查 |
| @PLGM-S3 | MoPlatform `local-dev/plugin-e2e.mjs#PLUG-S51`；MoAI `PluginMarketplaceService.InstallAsync` 代码走查（版本/大小/hash/安全解压/登记） | 平台 e2e 已加入待执行；MoAI 代码走查 |
| @PLGM-S4 | `PluginMarketplaceService.InstallAsync` key/重复检查代码走查 | 待补充服务单测 |
| @PLGM-S5 | `PluginMarketplaceService.SetEnabledAsync` 与 `PluginPackageStartupLoader.StartAsync` 代码走查 | 待补充服务单测 |
| @PLGM-S6 | `PluginPackageSemanticTests#ExtractAsync_ReadsSemanticBundle / ExtractAsync_RejectsInvalidSemanticMode`；`PluginMarketplaceService.InstallAsync` 同事务写入分类、UsageNotes/PluginSemanticNotes/PluginToolMetas 代码走查 | MoAI.AIPlugin.Static.Tests 9/9；语义包解析/校验 PASS；安装服务集成测试待补充（2026-11-14） |
| @PLGM-S7 | `ui/src/pages/plugins/__tests__/PluginMarketplacePanel.test.tsx`（MoPlatform 分类加载与筛选、卡片、隐藏已安装页签和刷新）；MoAI Custom 模块与宿主编译 | PASS 3/3、typecheck 通过；模块构建 0 警告，宿主构建 0 错误（2026-11-14） |
| @PLGM-S8 | `ui/src/pages/plugins/__tests__/PluginMarketplacePanel.test.tsx`（选择本页未安装项、顺序安装、失败隔离与失败项保留） | PASS 1/1（2026-11-14） |

构建与测试命令见仓库根 [AGENTS.md](../../AGENTS.md)。MoPlatform 完整在线插件 e2e 会写入运行数据库/对象存储，只有在确认测试环境后执行。
