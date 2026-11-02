# 登录安全风控 验证映射（TDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 证据：[local-dev/loginsafety-e2e.mjs](../../local-dev/loginsafety-e2e.mjs)

前置：后端以 ForwardedHeaders 开启运行（E2E 头部注释含完整启动命令），`configs/ip2region.xdb` 在位；脚本自带本地 SMTP 桩与 HTTP webhook 桩，零真实外部依赖。

| 场景 | 验证物 | 结果（日期） |
|---|---|---|
| @LS-S1~S3 | local-dev/loginsafety-e2e.mjs（member 403 / 匿名 401 / member 设置 403） | PASS 38/38（2026-11-02） |
| @LS-S4~S5 | 同上（admin 成功记录 + 未读计数接口） | PASS（2026-11-02） |
| @LS-S6~S7 | 同上（X-Forwarded-For 落库 + 北京市/California 归属地解析） | PASS（2026-11-02） |
| @LS-S8~S12 | 同上（R1 爆破：401→403→R1 事件→auto:R1 记录→解封恢复） | PASS（2026-11-02） |
| @LS-S10b | 同上（R1 高风险事件推送 webhook 渠道，内置中文模板标题） | PASS（2026-11-02） |
| @LS-S13~S15 | 同上（R2 撞库：403→R2 事件→auto:R2 记录） | PASS（2026-11-02） |
| @LS-S16~S17 | 同上（新 IP 登录 riskLevel=2 + R3 中风险事件） | PASS（2026-11-02） |
| @LS-S18~S19 | 同上（异地登录 + R5 疾速旅行高风险事件） | PASS（2026-11-02） |
| @LS-S19b~S19c | 同上（用户站内信到达 /api/message/list + 内置模板标题与正文） | PASS（2026-11-02） |
| @LS-S20~S20b | 同上（SMTP 渠道按收件人发告警邮件 + MIME encoded-word 解码断言内置模板标题） | PASS（2026-11-02） |
| @LS-S21 | 同上（批量标记已读后未读数减少） | PASS（2026-11-02） |
| @LS-S22~S23 | 同上（手动封禁 403 → 解封恢复） | PASS（2026-11-02） |
| @LS-S24 | 同上（LS_ENABLED=false：日志照记、riskLevel=0、无新事件） | PASS（2026-11-02） |
| @LS-S25 | 同上（阈值 0/开关非布尔/天数越界/渠道 ID 非法 均 400） | PASS（2026-11-02） |
| @LS-S26 | 同上（/mylog 仅本人成功记录） | PASS（2026-11-02） |
| 后端构建 | `dotnet build src/MoAI/MoAI.csproj` | PASS 0 error（2026-11-02） |
| 前端构建 | ui：`npm run typecheck` / `lint`（0 error）/ `test` | PASS（2026-11-02） |
| 菜单角标/页面走查 | @manual（浏览器走查，见 sop.md 第 4 节） | 待走查（2026-11-02） |

回归命令：

```bash
dotnet build src/MoAI/MoAI.csproj
node local-dev/loginsafety-e2e.mjs http://127.0.0.1:5213   # 需 ForwardedHeaders 开启的后端
cd ui && npm run typecheck && npm run lint && npm run test
```
