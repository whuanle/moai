# 部署与本地环境（Deployment）验证映射（TDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md)（场景定义） ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md)
> 本文只做「场景 → 验证物 → 结果」映射。**D1/D2/D3 已修复且容器实测通过（2026-09-03）：@DEP-S2 全链路验收完成。**

## 场景映射表

| 场景 | 验证物 | 结果（日期） |
|---|---|---|
| @DEP-S1（缺陷 D1） | `grep -c "ui/moai" Dockerfile` → 0；`docker build` 前端阶段 `COPY ui/package*.json` 实际执行成功 | **PASS（2026-09-03，镜像构建成功）** |
| @DEP-S2 | `docker build -t moai:dep-s2-verify .`（arm64 原生）→ 起容器 → `serverinfo` 200 + SPA `/`、`/login` 均 200 + **e2e 41/41**（UM 34 + 存储 7，存储审计在容器内跑） | **PASS（2026-09-03）** |
| @DEP-S3、@DEP-S6 | entrypoint 生成 S3 段（D3 修复后）；未配 S3 时 S3 客户端 500（已实证）；.env.example 走查 | 走查一致（2026-09-03） |
| @DEP-S4（缺陷 D2） | 容器内 **OTLP 留空启动成功**（`ParseOtlpEndpoint` 空值跳过导出），Hangfire 正常调度 | **PASS（2026-09-03，容器实测）** |
| @DEP-S5 | 需可达 collector 环境 | 未执行（无 collector，记录待验） |
| @DEP-S7 ~ @DEP-S9 | `docker compose config -q`（语法与 .env 插值）+ healthcheck/volumes 定义走查 | PASS（2026-09-02，语法级） |
| @DEP-S10 | ENV MAI_CONFIG 无效 + 回退链走查（infra 加载器） | 走查一致（2026-09-02） |
| @DEP-S11 | `curl -s http://127.0.0.1:5210/api/common/serverinfo` → serviceUrl=5210 | PASS（2026-09-02） |
| @DEP-S12 | `docker ps`：moai-postgres/redis/rabbitmq/minio 四容器 Up(healthy) + 前端 dev 联调 | PASS（2026-09-02） |

## 回归命令

```bash
docker compose config -q && echo OK                      # compose 语法与 .env 插值
grep -c "ui/moai" Dockerfile                             # 预期 0（D1 修复复核）
grep -n "ParseOtlpEndpoint" src/MoAI/Modules/ConfigureOpenTelemetryModule.cs   # D2 修复复核（空值跳过）
curl -s http://127.0.0.1:5210/api/common/serverinfo | head -c 200    # 本地形态冒烟（serviceUrl=5210）
```

## 修复记录与遗留

1. ✅ D1 已修复并**实测验证**（2026-09-02 修 / 09-03 验）：`ui/moai/` → `ui/`；镜像升 net10；`ENV MAI_FILE`。[@DEP-S2](./bdd.md#dep-s2) 整链构建成功。
2. ✅ D2 已修复并**实测验证**（2026-09-03 容器内 OTLP 留空启动正常）（[@DEP-S4](./bdd.md#dep-s4)）。
3. ✅ D3 已修复（2026-09-03）：entrypoint 生成 S3 段（S3_* env），compose/.env.example 同步；容器内存储 e2e 7/7（[@DEP-S3](./bdd.md#dep-s3)）。遗留注意：S3_ENDPOINT 单端点设计（须同时被应用与上传客户端可达），见 sdd 缺陷表。
4. ⬜ D6（新，低优）：aspnet:10.0 缺 libgssapi_krb5.so.2 启动告警，实测不影响功能。
5. ⬜ @DEP-S5：OTLP 上报端到端（需真实 collector）。
