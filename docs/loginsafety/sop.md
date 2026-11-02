# 登录安全风控 运维手册（SOP）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 回归执行见 [tdd.md](./tdd.md)

## 1. 部署与配置

1. **IP 归属地库**：`configs/ip2region.xdb`（随仓库分发，构建自动拷贝到输出目录 `configs/`）。缺失时服务正常启动，日志出现「ip2region.xdb 数据文件不存在」警告，R4/R5（异地/疾速旅行）自动停用，其余规则不受影响。更新数据：从 ip2region 官方仓库下载 structure 3.0 的 IPv4 xdb 覆盖后重启。
2. **反向代理穿透**：部署在 nginx 等反代之后时，配置 `MoAI:ForwardedHeaders:Enabled=true`（否则所有登录 IP 都是代理地址，风控失效）。可信代理 IP 固定时填 `MoAI:ForwardedHeaders:KnownProxies`；公网直连暴露端口时**切勿开启**（IP 可被伪造）。compose 一体部署默认未开启，见 [deployment/docker.md](../deployment/docker.md)。
3. **风险通知**：受影响用户固定收**站内信**提醒；高风险事件额外推「系统设置 → 登录安全」卡片勾选的通知中心渠道（`LS_NOTIFY_CHANNEL_IDS`，勾选 SMTP 渠道即告警邮件——SMTP 连接配置在通知中心渠道自带，与「邮箱验证」绑定无关）。通知内容内置多语言默认中文模板。
4. **阈值**：默认 IP 失败 20 次/10 分钟、撞库 5 用户名、封禁 30 分钟、新 IP 回看 90 天、旅行窗口 120 分钟；在「系统设置 → 登录安全」卡片调整。

## 2. 日常操作

- 风险处置：侧边栏「日志事件」（admin）→ 风险事件 Tab：未读行高亮，勾选/单条「标记已读」；角标数字 = 未读事件数，每 60s 刷新，处置后即时联动。
- IP 封禁管理：「日志事件」→ IP 封禁 Tab：查看生效中/已过期/已解封，支持提前解封与手动封禁（Popconfirm/Modal，maskClosable=false）。
- 登录溯源：登录日志 Tab 按用户名/IP/结果/风险等级筛选；归属地列为空通常是内网 IP 或 xdb 缺失。
- 用户自查：GET `/api/loginsafety/mylog`（本人成功登录记录）。

## 3. 排障表

| 症状 | 排查 |
|---|---|
| 所有登录 IP 都是同一个（代理）地址 | ForwardedHeaders 未开启（见上），或请求未经过配置的可信代理 |
| 异地/疾速旅行从不命中 | xdb 缺失或损坏（启动日志有 warning）；或用户历史登录均无省份（内网 IP） |
| 高风险告警未推渠道/邮件未发出 | 依次查：LS_NOTIFY_CHANNEL_IDS 是否勾选渠道 → 渠道是否启用（SMTP 渠道核对自身连接配置，见 notification/sop）→ Hangfire 是否有积压 |
| 封禁未生效（仍能登录） | 多实例间 Redis 键 `moai:ls:ipban:{ip}` 被清（DB 兜底 5 分钟内回填）；确认各实例连同一 Redis |
| 登录 403「当前 IP…临时封禁」误伤 | 管理员在 IP 封禁 Tab 解封；若为共享出口 IP（公司 NAT）集体失败所致，调高 LS_IP_FAIL_THRESHOLD |
| 登录日志无数据 | login_log 表缺失（旧库升级需 DatabasPostgresModule 补建自动执行，检查启动日志） |

## 4. 验收流程（人工走查）

1. admin 登录 → 左侧管理组出现「日志事件」菜单；制造一次新 IP 登录（换网络）后角标出现数字。
2. 进入「日志事件」：登录日志 Tab 有刚才的登录记录（IP/归属地/风险列）；风险事件 Tab 未读行高亮，「标记已读」后角标减少。
3. IP 封禁 Tab 手动封禁本机出口 IP → 登录被 403 → 解封后恢复。
4. 系统设置 → 登录安全卡片：改动阈值/开关/模板 → 保存 → 重登生效。
5. 回归：`node local-dev/loginsafety-e2e.mjs <baseUrl>` 全 PASS（见 [tdd.md](./tdd.md)）。

## 5. 与 auth 模块（2FA）的协作边界

登录风控管「环境风险」（IP/属地/爆破）；登录二次验证（TOTP/邮箱验证码）与强制 2FA 属 auth 模块（见 [../auth/sdd.md](../auth/sdd.md)）。密码校验成功即计入风控（此时登录尚未完全通过，2FA 挑战属附加步骤）；IP 封禁检查在 2FA 之前。
