# 部署与本地环境（Deployment）行为规格（BDD，Gherkin）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md)（场景→验证映射） ｜ [SOP](./sop.md)（操作与验收流程）
> 编号规则与标签语义见 [../DOC-STANDARD.md](../DOC-STANDARD.md) 第 3 节。本模块全部为命令/走查验证 → @manual；缺陷 D1~D4 如实写成场景并标注（缺陷记录）。

```gherkin
Feature: Docker 镜像构建（docker build）
  @DEP-S1 @manual
  Scenario: 按当前仓库直接构建失败（缺陷记录 D1：COPY 路径不存在）
    Given 仓库保持现状（前端目录为 ui/，不存在 ui/moai 子目录）
    When 在仓库根目录执行 docker build .
    Then frontend-builder 阶段 COPY ui/moai/package*.json 报错，构建失败

  @DEP-S2 @manual
  Scenario: 修复路径后构建成功
    Given Dockerfile 中两处 ui/moai/ 已改为 ui/
    When 依次执行前端 npm ci + build、后端 publish
    Then 最终镜像含 /app 后端产物、/app/wwwroot 前端产物、/app/docker-entrypoint.sh

Feature: 容器启动与配置生成（docker-entrypoint.sh）
  @DEP-S3 @manual
  Scenario: 环境变量生成 system.json（缺陷记录 D3：容器形态为本地盘存储）
    Given 容器启动并注入 POSTGRES_*/REDIS_*/RABBITMQ_*/MOAI_* 环境变量
    When entrypoint 执行
    Then 生成 /app/configs/system.json，各连接串指向 compose 服务名（postgres/redis/rabbitmq）
    And Storage 为 LocalPath=/app/files（非对象存储）

  @DEP-S4 @manual
  Scenario: OTLP 留空时启动即崩（缺陷记录 D2：无条件 new Uri）
    Given .env 未配置 OTLP_TRACE/OTLP_METRICS
    When 后端模块装配执行到 ConfigureOpenTelemetryModule
    Then new Uri("") 抛异常，进程启动失败

  @DEP-S5 @manual
  Scenario: OTLP 配置真实端点
    Given OTLP_TRACE/OTLP_METRICS 指向可达的 collector
    When 后端启动
    Then Trace/Metrics 经 OTLP 协议（0=grpc，1=http/protobuf）上报成功

  @DEP-S6 @manual
  Scenario: 照抄 .env.example 回环地址上报失败（缺陷记录 D4）
    Given OTLP 端点照抄示例值 127.0.0.1:4012
    When 容器内后端上报
    Then 127.0.0.1 指向容器自身而非宿主机，上报失败

Feature: compose 依赖编排（docker compose up）
  @DEP-S7 @manual
  Scenario: 基础设施就绪顺序
    Given moai 声明 depends_on postgres/redis/rabbitmq（condition: service_healthy）
    When 任一基础设施 healthcheck 未通过
    Then moai 容器不启动

  @DEP-S8 @manual
  Scenario: 首次建库初始化 pgvector
    Given postgres 数据卷为空
    When 首次启动 postgres 容器
    Then docker-entrypoint-initdb.d/init-pgvector.sql 执行，vector 与 uuid-ossp 扩展创建

  @DEP-S9 @manual
  Scenario: 数据持久化
    Given 服务运行并产生数据（postgres/redis/rabbitmq/上传文件）
    When docker compose down（不带 -v）后重新 up
    Then named volume 保留，数据不丢失

Feature: 配置加载优先级（MAI_FILE）
  @DEP-S10 @manual
  Scenario: 容器内不设 MAI_FILE
    Given 工作目录 /app 且存在 entrypoint 生成的 /app/configs/system.json
    When 后端启动
    Then 加载该回退路径的配置（Dockerfile 的 ENV MAI_CONFIG 为无效变量）

  @DEP-S11 @manual
  Scenario: 本地开发显式注入 MAI_FILE
    Given MAI_FILE 指向 system.local.json（Port=5210）
    When 后端启动
    Then 仓库 configs/system.json（Port=5000）被完全覆盖
    And /api/common/serverinfo 返回的 serviceUrl 与 MAI_FILE 文件一致

Feature: 本地开发环境
  @DEP-S12 @manual
  Scenario: 前后端联调
    Given 四个基础设施容器 healthy（自定义端口）
    When 后端 5210 启动、前端 4000 dev server 启动
    Then 前端经 VITE_ServerUrl 直连 5210 完成登录与业务请求
    And 上传文件走 MinIO（127.0.0.1:9000，桶 moai）
```
