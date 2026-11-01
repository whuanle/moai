# 通知中心（Notification，NT）验证映射（TDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md)（@NT-Sxx） ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 证据：[local-dev/notification-e2e.mjs](../../local-dev/notification-e2e.mjs)

回归命令（需后端运行中；内置 webhook/SMTP 桩，零真实外部依赖）：

```bash
node local-dev/notification-e2e.mjs   # NT 42
```

| 场景 | 验证物 | 结果（日期） |
|---|---|---|
| @NT-S1 | local-dev/notification-e2e.mjs#NT-01..04 | PASS 4/4（2026-11-02） |
| @NT-S2 | local-dev/notification-e2e.mjs#NT-05..11 | PASS 7/7（2026-11-02） |
| @NT-S3 | local-dev/notification-e2e.mjs#NT-12..13 | PASS 2/2（2026-11-02） |
| @NT-S4 | local-dev/notification-e2e.mjs#NT-14..18 | PASS 5/5（2026-11-02） |
| @NT-S5 | local-dev/notification-e2e.mjs#NT-19..24（桩内验签断言） | PASS 6/6（2026-11-02） |
| @NT-S6 | local-dev/notification-e2e.mjs#NT-25..28 | PASS 4/4（2026-11-02） |
| @NT-S7 | local-dev/notification-e2e.mjs#NT-29..30（SMTP 桩捕获） | PASS 2/2（2026-11-02） |
| @NT-S8 | local-dev/notification-e2e.mjs#NT-31 | PASS 1/1（2026-11-02） |
| @NT-S9 | local-dev/notification-e2e.mjs#NT-32..34（Hangfire 轮询终态） | PASS 3/3（2026-11-02） |
| @NT-S10 | local-dev/notification-e2e.mjs#NT-35..36 | PASS 2/2（2026-11-02） |
| @NT-S11 | local-dev/notification-e2e.mjs#NT-37..38 | PASS 2/2（2026-11-02） |
| @NT-S12 | local-dev/notification-e2e.mjs#NT-39 | PASS 1/1（2026-11-02） |
| @NT-S13 | local-dev/notification-e2e.mjs#NT-40（/api/message/list 断言） | PASS 1/1（2026-11-02） |
| @NT-S14 | local-dev/notification-e2e.mjs#NT-41 | PASS 1/1（2026-11-02） |
| @NT-S15 | local-dev/notification-e2e.mjs#NT-42 | PASS 1/1（2026-11-02） |

## 自检记录（2026-11-02）

- `dotnet build src/MoAI/MoAI.csproj`：0 错误。
- `node local-dev/notification-e2e.mjs`：**PASS 42 / FAIL 0**（首轮 30/12，修复 4 类问题后全绿：
  ① 响应 long/枚举为字符串，断言需按字符串比较；② Update 命令 Id 由路由晚于模型验证赋值，校验规则移入 Handler；
  ③ 枚举成员 `DingTalkWebhook` 改名 `DingtalkWebhook` 对齐 NSwag 契约（camelCase(成员名)==JsonPropertyName）；
  ④ 环境坑：遗留的 loginsafety e2e 后端（旧构建，端口 5213）与新后端共享同一 Redis Hangfire 存储，抢占通知任务致 pending——
  终止遗留实例后恢复，见 SOP 排障表第 1 条）。
- `cd ui && npm run typecheck`：0 错误；`npm run lint`：0 错误（15 条存量警告，无本模块新增）；`npm run test`：700/700。
- E2E 可重复执行：渠道按运行标识命名、结束自清理；SMTP 八项设置开头快照、结束恢复。
