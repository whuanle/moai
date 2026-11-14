# 在线插件市场（Plugin Marketplace）设计规格（SDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 上游：[aiplugin-static](../aiplugin-static/sdd.md)、[infra](../infra/sdd.md) ｜ 平台契约：MoPlatform `/api/client/plugin/*`（见 MoPlatform `docs/plugin/sdd.md` 与 `docs/api_interface.md`）

## 目标与边界

MoAI 管理员通过 MoPlatform 在线插件市场浏览并安装受平台发布的 .NET 插件包。MoAI 以持久安装指纹调用独立客户端 API，下载时固定版本并校验清单、包大小及 SHA-256；安装包落本地后加载为原生插件。包内的文字分类映射到 MoAI 本地同名插件分类，不存在时一并创建。包内可选 `semantic.json` v1 在安装时导入现有插件语义表（使用须知、always/onDemand 条目及 `run` 工具元数据），新建的 MoAI 插件自动带有作者预置的语义规则，无需管理员重新配置。在线包只允许安装一次，不提供覆盖和卸载；管理员可上/下架本地安装记录，但下架不删除包、不卸载当前进程已加载程序集。

## 组件

| 组件 | 职责 |
|---|---|
| `MoPlatformClientFingerprint` | 首次请求时生成 256 位随机十六进制指纹，持久化到 `configs/mo-platform-client.fingerprint` |
| `MoPlatformPluginClient` | 使用 `MoAI:MoPlatform:Endpoint`，为 `/api/client/plugin/list/detail/manifest/download` 请求添加 `X-MoAI-Client-Fingerprint`，并通过 portal 分类接口读取插件分类 |
| `PluginMarketplaceService` | 查询平台目录、分类和本地安装状态；目录支持关键字/平台分类筛选；获取详情与清单；校验 key、版本、框架、文件长度和 SHA-256；安全解压、映射或创建本地文字分类、导入 semantic.json 到现有语义表、持久化并加载插件包 |
| `PluginPackageStartupLoader` | 启动时扫描本地安装记录并逐个加载；单包失败隔离，不阻断宿主启动 |
| `/api/ai/plugin/online/*` | 管理员市场目录/分类查询、安装、本地已安装列表与上/下架入口 |

## 关键决策

- **D1 客户端接口隔离**：MoAI 只调用 MoPlatform 独立 `/api/client/plugin/*` 区域，不使用管理员 API、PAT 或用户登录；平台通过指纹/IP Redis 限频。
- **D2 指纹语义**：指纹是本地安装实例配额标识，不是认证凭据。平台返回下载地址后必须校验清单绑定的版本 id、文件大小和 SHA-256。
- **D3 包生命周期**：本地插件包写入 `configs/plugins/<pluginKey>`；重复安装、key 冲突或已有安装记录返回冲突，不在线覆盖；不提供卸载。已下载包在宿主重启时从数据库登记记录加载。
- **D4 失败隔离**：平台接口、清单、下载、解压或运行时加载失败不得污染已安装数据；启动加载按包隔离失败。
- **D5 包内语义作为安装初值**：`semantic.json` 随已验 SHA-256 的 ZIP 下载，MoAI 在创建本地插件记录时将 usageNotes、notes、toolMetas 一并写入已有语义表；不新增数据库表。缺少文件的旧包继续安装为空语义，避免破坏兼容。
- **D6 分类按文本跨平台映射**：从包内 `plugin.json.category` 读取文字分类；MoAI 在本地 `type=plugin` 分类中按名称复用或创建（无 emoji），并设置新插件的 `ClassifyId`，不依赖 MoPlatform 分类 id。
- **D7 在线目录分类与呈现**：管理页单页展示在线插件卡片；分类字典由 MoAI 服务端代理 MoPlatform `/api/portal/category/list?type=plugin`，筛选值透传客户端目录 `categoryId`；页面不展示已安装独立列表或刷新按钮，安装状态仅用于避免重复安装。
- **D8 批量安装**：管理员可在当前目录页勾选多个未安装插件；前端复用单插件安装 API 顺序安装，单项失败不阻断后续项，并汇总结果、保留失败项供重试。选择范围限定当前页，筛选或翻页时清空选择；不新增后端批量接口，以避免并发写包目录和插件运行时注册表。

## 已知边界

- 运行时装载 .NET 程序集意味着插件包具备宿主进程内代码执行能力；仅从管理员审批发布的可信 MoPlatform 来源安装。
- 平台客户端指纹可生成和伪造，不能替代密码学身份验证；防刷依赖 Redis 按指纹/IP 限频和短期预签名地址。
- 安装流程目前由管理员 API 提供，在线包不会自动向团队成员开放；已加载程序集无法通过数据库下架即时卸载。
