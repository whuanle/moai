# 在线插件市场操作手册（SOP）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ API 契约：MoPlatform `/api/client/plugin/*`（MoPlatform 仓库 `docs/plugin/sdd.md`）

## 配置与指纹

MoPlatform 地址由 `MoAI:MoPlatform:Endpoint` 配置；本地 Development 使用 `http://127.0.0.1:5100`。MoAI 第一次请求插件客户端接口时，在应用配置目录生成 `mo-platform-client.fingerprint`，后续请求复用该值。多实例部署应共享该文件/配置卷，以免每个实例被当成独立配额主体。

该指纹只用于标识配额主体，不是密码或可靠认证凭据。MoPlatform client 插件区在 Redis 中对普通请求按指纹/IP 分别限制 60/240 次每分钟，下载地址签发限制 10/30 次每分钟。指纹不要由浏览器直接读取或返回给用户端。

## 管理流程

管理员打开在线插件目录后，页面通过 `/api/ai/plugin/online/categories` 获取 MoPlatform 插件分类，并以 `/api/ai/plugin/online/catalog` 的 `categoryId` 筛选卡片；点击单项安装或勾选当前页多个未安装插件后批量安装，均调用 `/api/ai/plugin/online/install`。批量操作按顺序逐项执行，单项失败不会阻断其余项，完成后汇总成功/失败数并保留失败项供重试。安装前系统按清单检查支持的 schema、key、版本、.NET 框架、包大小与摘要。包内文字分类会映射到 MoAI 本地同名插件分类，不存在时自动创建。ZIP 内有合法 `semantic.json` 时，安装会自动把 usageNotes、语义条目和工具元数据导入本地插件语义层；详情页与语义查询接口可立即读取，无需再手工录入。页面不提供已安装列表和刷新按钮；管理员仍可通过 `/api/ai/plugin/online/installed` 查询本地状态，并使用 `/api/ai/plugin/online/status` 上架/下架。

当前不支持在线覆盖和卸载；重复 key 安装返回冲突。下架只禁用记录和注册表工具，不删除包文件、不卸载当前进程已加载的程序集。

## 排障

| 现象 | 原因 | 处理 |
|---|---|---|
| client/plugin 返回 400 | 指纹缺失/格式无效，或安装清单字段不符合协议 | 检查 `configs/mo-platform-client.fingerprint` 和 MoPlatform manifest |
| client/plugin 返回 429 | 指纹/IP 超过 Redis 配额 | 等待限频窗口后重试；不要通过轮换指纹规避配额 |
| client/plugin 返回 503 | MoPlatform Redis 限频服务不可用 | 检查平台 Redis 健康状态 |
| 包摘要或大小校验失败 | 预签名下载不完整或响应与清单不一致 | 不要手动跳过校验；重新获取清单与下载地址后重试 |
| 安装返回冲突 | key 已安装、被本地插件占用，或包目录已存在 | 先通过安装列表和 `configs/plugins` 核对；系统不支持覆盖/卸载 |
| 重启后插件未加载 | 包目录缺失、插件元数据/程序集不兼容或数据库状态为下架 | 查看启动日志中的“在线插件包加载失败”与 key；修复包后走明确的管理员处理流程 |
