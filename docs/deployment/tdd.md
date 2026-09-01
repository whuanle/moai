# 部署与本地环境（Deployment）验证映射（TDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md)（场景定义） ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md)
> 本文只做「场景 → 验证物 → 结果」映射。**D1/D2 缺陷未修复，相关验证为「缺陷证实」而非 PASS；D1 修复后的构建/起容器验收待执行。**

## 场景映射表

| 场景 | 验证物 | 结果（日期） |
|---|---|---|
| @DEP-S1（缺陷 D1） | `ls ui/moai` → No such file or directory；`grep -n "ui/moai" Dockerfile` → 第 13/19 行 | 缺陷证实（2026-09-02，未修复） |
| @DEP-S2 | `docker compose build moai`（依赖 D1 修复） | 未执行（待 D1 修复后按 [SOP 第 2 节](./sop.md) 验收） |
| @DEP-S3、@DEP-S6 | entrypoint heredoc / .env.example 走查（D3/D4 记录） | 走查一致（2026-09-02） |
| @DEP-S4（缺陷 D2） | `grep -n "new Uri" src/MoAI/Modules/ConfigureOpenTelemetryModule.cs` → 第 45/58 行两处无条件调用 | 缺陷证实（2026-09-02，未修复） |
| @DEP-S5 | 需可达 collector 环境 | 未执行（无 collector，记录待验） |
| @DEP-S7 ~ @DEP-S9 | `docker compose config -q`（语法与 .env 插值）+ healthcheck/volumes 定义走查 | PASS（2026-09-02，语法级） |
| @DEP-S10 | ENV MAI_CONFIG 无效 + 回退链走查（infra 加载器） | 走查一致（2026-09-02） |
| @DEP-S11 | `curl -s http://127.0.0.1:5210/api/common/serverinfo` → serviceUrl=5210 | PASS（2026-09-02） |
| @DEP-S12 | `docker ps`：moai-postgres/redis/rabbitmq/minio 四容器 Up(healthy) + 前端 dev 联调 | PASS（2026-09-02） |

## 回归命令

```bash
docker compose config -q && echo OK                      # compose 语法与 .env 插值
ls ui/moai                                               # 证实 D1（预期 No such file or directory）
grep -n "new Uri" src/MoAI/Modules/ConfigureOpenTelemetryModule.cs   # 证实 D2（第 45/58 行）
curl -s http://127.0.0.1:5210/api/common/serverinfo | head -c 200    # 本地形态冒烟（serviceUrl=5210）
```

## 待修复（未完成事项，勿勾选）

1. 修复 Dockerfile 前端阶段路径 `ui/moai/` → `ui/`（D1，[@DEP-S1](./bdd.md#dep-s1)）→ 补验 @DEP-S2。
2. OTLP 空值保护：ConfigureOpenTelemetryModule 对空 Trace/Metrics 跳过 AddOtlpExporter（D2，[@DEP-S4](./bdd.md#dep-s4)）→ 补验 @DEP-S5。
3. 统一容器/本地两种形态的存储选型说明或配置段（D3，[@DEP-S3](./bdd.md#dep-s3)）。
