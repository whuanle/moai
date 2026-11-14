# AI 渠道与模型目录操作手册（SOP）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 证据：[模型目录 E2E](../../local-dev/model-catalog-e2e.mjs)

## 配置

MoPlatform 地址配置项为 `MoAI:MoPlatform:Endpoint`。默认值为 `https://anyai.wiki`；`appsettings.Development.json` 指向本机 `http://127.0.0.1:5100`。部署可通过系统配置覆盖。该地址是 MoPlatform 服务根地址，不要追加 latest API 路径。

## 同步验收

管理员调用模型渠道管理页的「同步模型目录」操作（`POST /api/ai/model/catalog/sync`）。成功后文件写入应用 `configs/models.json`，渠道模型候选识别立即生效，无需重启；响应中的渠道数与模型数应大于 0。

## 排障

| 现象 | 可能原因 | 处理 |
|---|---|---|
| latest 返回 404 | MoPlatform 尚未刷新/发布过目录，或 Endpoint 指向错误环境 | 在 MoPlatform 导出并检查 `GET /api/portal/catalog/models/latest`，确认 Endpoint 为服务根地址 |
| latest 下载地址为 `/static/...` 或下载返回 5xx | MoPlatform 仍运行旧版本，未签发 S3 预签名地址 | 部署包含签名下载改造的 MoPlatform；latest `url` 应指向 S3，MoAI 仍兼容旧 `/static` 地址 |
| 无法连接 latest 或 S3 下载地址 | 部署网络/DNS/代理不可达，或环境代理拦截对象存储访问 | 检查 `MoAI:MoPlatform:Endpoint`、MoAI 到 S3 Endpoint 的网络与 `HTTPS_PROXY`；MoAI 会尝试环境代理、系统默认代理和直连 |
| S3 签名下载超时/过期或 403 | 签名地址有效期为 10 分钟；网络路径切换/耗时导致过期，或对象存储 Endpoint 对 MoAI 不可达 | MoAI 会重取 latest 后再试；确认 MoPlatform 返回的 S3 Endpoint 可从 MoAI 运行环境访问。检查日志中的下载阶段，勿复制或记录完整签名 URL |
| size 或 SHA-256 校验失败 | latest 指针与下载内容不一致、缓存未更新或传输不完整 | 确认 CDN/对象存储内容与 latest 指针匹配后重试；失败不会覆盖现有目录 |
| 模型未识别 | 目录尚未同步、渠道 code/model id 不一致，或文件损坏 | 重新同步并检查 `configs/models.json` 的 schemaVersion、渠道 code 与模型 id |

## 验收

自动化验收场景见 [@MC-S2](./bdd.md#mc-s2)、[@MC-S3](./bdd.md#mc-s3)、[@MC-S4](./bdd.md#mc-s4)、[@MC-S5](./bdd.md#mc-s5)；签名 URL 刷新重试见 [@MC-S6](./bdd.md#mc-s6)。执行命令见仓库入口 [AGENTS.md](../../AGENTS.md)。
