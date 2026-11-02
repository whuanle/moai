# 登录安全风控（loginsafety）设计规格（SDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 上游：[../auth/sdd.md](../auth/sdd.md)、[../settings/sdd.md](../settings/sdd.md)、[../notification/sdd.md](../notification/sdd.md)、[../message/sdd.md](../message/sdd.md) ｜ 证据：[local-dev/loginsafety-e2e.mjs](../../local-dev/loginsafety-e2e.mjs)

## 1. 目标

对每一次登录尝试（成功/失败，账密与 OAuth）做全量日志与风险判定：识别新 IP/异地/疾速旅行/IP 爆破/撞库喷洒/管理员新环境登录，按等级处置——记录风险事件、给受影响用户发站内信（固定逻辑）、高风险事件推送到通知中心渠道、自动/手动封禁来源 IP。管理员在「日志事件」页（Tab：登录日志/风险事件/IP 封禁）处置，侧边栏菜单角标显示未读风险事件数。

## 2. 组件

```
src/loginsafety/                          缩写 LS，CQRS 三层
  MoAI.LoginSafety.Shared                 模型（LoginType/LoginRiskLevel/LoginRiskRule/LoginEventContext）
                                          + ILoginRiskService + 管理/自查 CQRS
  MoAI.LoginSafety.Core                   LoginRiskService（规则引擎）+ Ip2RegionService（xdb v3 离线归属地）
                                          + LoginSafetySettingsService + LoginRiskNotifyJob（Hangfire 站内信/渠道推送）
                                          + LoginAlertTemplate（SmartFormat 白名单插值）+ LoginAlertTemplates（内置双语模板）
  MoAI.LoginSafety.Api                    /api/loginsafety/**（管理员）+ /mylog（本人）
src/auth/MoAI.Auth.Core/Handlers          Login/OAuthLogin Handler 接入：认证前 CheckIpBanAsync，
                                          成功/失败各 EvaluateAsync（auth.Core 仅依赖 LoginSafety.Shared）
src/message/MoAI.Message.Shared           IMessageService.SendToUsersAsync（用户站内信提醒）
src/notification/MoAI.Notification.Shared INotificationService.NotifyAsync（高风险渠道推送，
                                          全站首个业务调用方；Source=loginsafety）
src/infra/MoAI.Infra.Shared               IClientContext（IP/UA，init-only）+ SetClientInfo 扩展；
                                          AutoAssignUserIdFilter（src/MoAI/Filters/）统一回填
src/database                              login_log / login_risk_event / login_ip_ban 三表
                                          （后加表：DatabasPostgresModule 幂等补建 + asserts/loginsafety.sql）
configs/ip2region.xdb                     ip2region 离线库（structure 3.0，IPv4，约 11MB）；缺失自动降级
```

## 3. 数据

- `login_log`：UserId/UserName（尝试值）/LoginType/IsSuccess/FailReason/Ip/Country/Province/City/Isp/UserAgent/RiskLevel/RiskReasons(json)/CreateTime；索引 user_id、ip、create_time。每次登录尝试必落一行（风控开关关闭也落）。
- `login_risk_event`：UserId/UserName/RuleCode/RiskLevel/Summary/Detail(json 含 email/ua/isp)/IsRead/Ip/Province/City。中/高风险各生成一条（一次登录最多一条，主规则取最高严重级 R5>R4>R3，管理员升级记 R6）。
- `login_ip_ban`：Ip/Reason/SourceRule(auto:R1|auto:R2|manual)/ExpireTime/IsLifted/LiftUserId/LiftTime。判定缓存 Redis `ls:ipban:{ip}`（裸 String，TTL=到期）；失败计数 `ls:ipfail:{ip}`（滑动窗口 INCR+EXPIRE）与用户名集合 `ls:ipusers:{ip}`（SADD+EXPIRE），成功登录清除。

## 4. 风险规则（阈值全部走设置 LS_*）

| 规则 | 条件 | 等级 | 处置 |
|---|---|---|---|
| R1 IP 爆破 | 窗口内同 IP 失败 ≥ LS_IP_FAIL_THRESHOLD（默认 20/10min） | 高 | 封禁 LS_IP_BAN_MINUTES + 事件 + 渠道推送（已配置时） |
| R2 撞库喷洒 | 窗口内同 IP 对 ≥ LS_DISTINCT_USER_THRESHOLD（默认 5）个用户名失败 | 高 | 同 R1 |
| R3 新 IP | 登录成功且近 LS_NEW_IP_DAYS（默认 90）天无此 IP 成功记录 | 中 | 事件 + 用户站内信 |
| R4 异地 | 地理库可用且省份 ≠ 近 30 天最近一次成功登录省份 | 中 | 同 R3 |
| R5 疾速旅行 | 与上次成功登录省份不同且间隔 < LS_TRAVEL_WINDOW_MINUTES（默认 120min） | 高 | 事件 + 用户站内信 + 渠道推送（已配置时） |
| R6 管理员新环境 | admin/root 命中 R3/R4/R5 | 高（升级） | 并入当次事件，同 R5 处置 |

处置动作（2026-11-02 起通知改版）：**中=事件+用户站内信；高=事件+用户站内信+渠道推送（LS_NOTIFY_CHANNEL_IDS 非空时）**。用户站内信为固定逻辑不可关（受影响用户本人，中/高风险登录成功必发）；渠道推送面向管理员侧消费，未配置渠道时跳过，停用/已删渠道由通知中心自动过滤。IP 封禁期登录一律 403（含正确密码）。

## 5. 关键决策

- **评估旁路化**：EvaluateAsync 内部 try/catch，任何内部异常记错误日志并返回无风险，绝不阻断登录；通知走 Hangfire 一次性任务，不阻塞登录。
- **真实 IP**：`MoAI:ForwardedHeaders:Enabled`（Program.cs，KnownProxies 可选）解析 X-Forwarded-For，关闭时行为与原状一致；`::ffff:a.b.c.d` 先映射回 IPv4 再解析归属地。
- **xdb v3 读取器自研**（无第三方包）：256B 定长头（version=3/ip_version=4 校验）+ 向量索引(256×256×8，基址 256) + 段索引二分（14B 项：起止IP4+长度2+指针4），数据原文「国家|省份|城市|ISP|国家码」。
- **判定先于落库**：成功登录的 R3/R4/R5 历史查询先于当前日志写入，避免本次记录污染「上次登录」口径。
- **IP 封禁缓存用裸 String**：StringSetAsync 写入、StringGetAsync 读存在性；GetAsync<string> 走 JSON 反序列化遇裸 `1` 会抛 JsonException。
- 通知为一次性任务由 Hangfire 重试（AutomaticRetry 3 次）；Detail 冗余 email 保留（历史字段，站内信按 risk_event.UserId 定向投递）。
- **通知走站内信+通知中心渠道（2026-11-02）**：原直连 SMTP 用户提醒邮件/管理员告警邮件（7 个 LS_ 设置键）全部下线；改 `IMessageService.SendToUsersAsync`（受影响用户，固定）+ `INotificationService.NotifyAsync(NotificationSendOptions.ChannelIds)`（高风险，可选，Level=Error，Source=loginsafety）。本模块是通知中心领域服务的首个外部业务调用方。
- **邮件模板内置多语言**：不再提供界面模板定制，`LoginAlertTemplates` 内置 zh-CN（默认）/en-US 两套标题与正文，经 SmartFormat 白名单插值（`LoginAlertTemplate`，策略同 auth EmailCodeTemplate）；语言解析预留参数（Hangfire 无请求上下文、用户表无语言字段，当前固定中文）。管理员需要邮件告警就在通知中心建 SMTP 渠道并勾选——邮件主题=标题（渠道 SubjectPrefix，空则【MoAI】）、正文=正文，渠道告警标题不再带站点名以避免与渠道前缀重复。
- **设置键收敛**：LS_* 保留 8 键（总开关 + 渠道 ID + 6 阈值）；新增 `LS_NOTIFY_CHANNEL_IDS`（逗号分隔 notification_channel.id，保存校验正整数、≤50 个、非法 400；读取宽容解析忽略非法段）；`LS_NOTIFY_USER_ENABLED/LS_NOTIFY_ADMIN_ENABLED/LS_ADMIN_ALERT_RECIPIENTS/LS_USER_ALERT_SUBJECT/BODY/LS_ADMIN_ALERT_SUBJECT/BODY` 7 键从 SettingDefinitions/校验移除，存量行留在 setting 表不再读取。

## 6. 已知问题 / 边界

- 地理库仅 IPv4；xdb 缺失时 R4/R5 自动跳过（R1/R2/R3 仍生效）。
- R4/R5 的「常驻省份」取近 30 天最近一次成功登录省份，非统计 majority（够用且省查询）。
- refresh token 不做风控（v1 范围外）；踢下线/二次验证由 auth 模块 2FA 体系承担（见 docs/auth）。
- 本机调试直连（无反代）大量失败登录会累计本机 IP 计数，10 分钟窗口自动过期；E2E 启动带本机解封自愈（见 sop）。
- 通知模板语言当前固定中文（异步任务无语言上下文）；后续若引入用户/系统语言字段，经 `LoginAlertTemplates.ResolveLanguage` 接入即得英文模板。
