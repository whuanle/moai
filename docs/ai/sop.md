# Agent 运行时操作手册（SOP）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 上游：[../database-scaffold/sop.md](../database-scaffold/sop.md)

## 1. 应用 schema 变更

存量库必须先执行 DDL，否则应用相关查询因缺列报错：

```bash
# 有 psql 客户端时（连接串取 appsettings.Development.json 的 MoAI:Database）
psql "host=192.168.50.199 port=5432 dbname=moai_v2 user=postgres password=***" -f asserts/app_agent_chat.sql
```

新增列：`app.publish_status`、`app.publish_time`、`app_agent_session.state(jsonb)`。执行后重跑逆向生成（[database-scaffold SOP](../database-scaffold/sop.md) 第 1 节）：

```bash
dotnet run --project tool/PostgresScaffold
```

`src/database/**/Partial/*.cs` 为人工扩展（发布字段/冷快照映射），rescaffold 不会覆盖。

## 2. 日常操作

| 事项 | 操作 |
|---|---|
| 发布/取消发布 | 应用管理页「发布 / 取消发布」，或 `POST /api/app/{id}/publish`、`/unpublish` |
| 查会话 | `GET /api/app/{appId}/session/list` |
| 查历史 | `GET /api/app/session/{sessionId}/messages`（压缩后视图） |
| 清会话热态 | Redis 删除 `moai:appagent:{msg,session,usage}:{sessionId}`（前缀 `moai:`） |
| 对话端点 | `POST /api/agent/{appId}/chat`（AG-UI SSE，需登录态 JWT） |

## 3. 排障

| 现象 | 原因 | 处理 |
|---|---|---|
| 应用列表/详情报缺列 | 未执行 DDL | 执行第 1 节 DDL 并重跑 rescaffold |
| 对话返回 RunError「应用尚未配置对话模型」 | `app_agent_config.model_id` 为空 | 在应用管理页选择对话模型 |
| 对话 RunError「模型不可用」 | 模型/渠道停用或未授权团队 | 检查模型与渠道启用、团队授权 |
| 用户看不到「进入对话」 | 应用未发布或 `publish_status≠1` | 管理员发布应用 |
| 历史消息变少 | 落库即压缩（D4） | 预期行为；被压缩原文不可恢复 |
| Redis 重启后对话异常 | 会话快照失效 | 自动回退 PG 冷快照/消息重建；确认 flush 已落库 |
| 会话列表为空 | 仅返回本人会话 | 预期行为 |

## 5. 沙箱（OpenSandbox）

| 事项 | 操作 |
|---|---|
| 全局配置 | `MoAI:OpenSandBox`（二级对象）：`Address`（如 `http://192.168.50.199:18123`）、`ApiKey`、`Image`、`TimeoutSeconds`（默认 900）、`RenewThresholdSeconds`（默认 300） |
| 开启沙箱 | 应用管理页「Agent 配置」→「启用沙箱」；存 `execution_settings.sandbox.enabled` |
| 查沙箱 | `curl http://<opensandbox>/v1/sandboxes`（按 `metadata` 含 `moai.*` 识别本系统） |
| 手删沙箱 | `curl -X DELETE http://<opensandbox>/v1/sandboxes/{id}` |
| 会话热态 | Redis `moai:appagent:sandbox:{sessionId}` 存 `{sandboxId, expiresAt}` |
| 定时回收 | Hangfire 周期任务 `sandbox-reaper`（每 5 分钟；`ReapOrphansAsync`） |

| 现象 | 原因 | 处理 |
|---|---|---|
| 工具报「未配置沙箱服务地址」 | `MoAI:OpenSandBox:Address` 为空 | 补配置后重启 |
| 首次调用沙箱工具较慢/超时 | 沙箱服务端需拉取镜像 | 已放宽请求/就绪超时；建议在沙箱服务端预热镜像 |
| 沙箱长期 `Pending` | 镜像拉取失败或服务端不可用 | 检查沙箱服务端 Docker/K8s 与镜像仓库连通性 |
| 沙箱未随会话删除 | 跨模块清理未硬保证 | 由 TTL + `sandbox-reaper` 兜底 |

## 4. 验收流程

1. 执行第 1 节 DDL 与 rescaffold，`dotnet build src/MoAI/MoAI.csproj` 0 error。
2. 启动后端，管理员在应用管理页发布 Agent 应用（[@AI-S1](./bdd.md#ai-s1)）。
3. 成员进入对话页发起对话，核对流式事件与消息落库（[@AI-S10](./bdd.md#ai-s10)~[@AI-S12](./bdd.md#ai-s12)）。
4. 绑定知识库后提问，核对检索工具被调用（[@AI-S16](./bdd.md#ai-s16)）。
5. 前端 `npm run typecheck && npm run lint && npm run test` 全绿。
6. 开启应用沙箱后发起对话，模型经 `list_tools`/`call_tool` 调用 `sandbox_run_shell` 返回 `sandbox_ok`（[@AI-S29](./bdd.md#ai-s29)）。
