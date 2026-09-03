# 部署与本地环境（Deployment）验证映射（TDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md)（场景定义） ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md)
> 本文只做「场景 → 验证物 → 结果」映射。**D1/D2 已于 2026-09-02 修复并经代码复核**；镜像构建/起容器整体验收（@DEP-S2）待执行。

## 场景映射表

| 场景 | 验证物 | 结果（日期） |
|---|---|---|
| @DEP-S1（缺陷 D1） | `grep -c "ui/moai" Dockerfile` → 0；`COPY ui/package*.json`、`COPY ui/` 正确 | **修复证实**（2026-09-02） |
| @DEP-S2 | `docker compose build moai`（D1 已修复，可执行） | 未执行（待整体验收，按 [SOP 第 2 节](./sop.md)） |
| @DEP-S3、@DEP-S6 | entrypoint heredoc / .env.example 走查（D3/D4 记录） | 走查一致（2026-09-02） |
| @DEP-S4（缺陷 D2） | `ConfigureOpenTelemetryModule` 现为 `ParseOtlpEndpoint` 预解析，空值/非法地址跳过 `AddOtlpExporter` | **修复证实**（2026-09-02 代码复核） |
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

1. ✅ D1 已修复（2026-09-02）：`ui/moai/` → `ui/` 两处；镜像升 sdk/aspnet **10.0**；`ENV MAI_CONFIG` → `ENV MAI_FILE`（[@DEP-S1](./bdd.md#dep-s1)）→ 待补验 @DEP-S2。
2. ✅ D2 已修复（2026-09-02）：`ParseOtlpEndpoint` 空值/非法地址跳过导出（[@DEP-S4](./bdd.md#dep-s4)）→ 待补验 @DEP-S5。
3. ⬜ 遗留 D3：统一容器/本地两种形态的存储选型说明或配置段（[@DEP-S3](./bdd.md#dep-s3)）。
