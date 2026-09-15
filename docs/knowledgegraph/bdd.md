# 知识图谱模块行为场景（BDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 证据：[local-dev/kg-e2e.mjs](../../local-dev/kg-e2e.mjs)、[local-dev/kg-external-e2e.mjs](../../local-dev/kg-external-e2e.mjs)

## Feature: 托管图谱（managed）

```gherkin
@KG-S1 @auto:e2e
Scenario: 创建空白图谱
  When 管理员以空白模板创建知识图谱
  Then 返回图谱 id 且来源为托管
  And 该图谱的模型为空

@KG-S2 @auto:e2e
Scenario: 创建模板图谱
  When 管理员以「运维服务」模板创建知识图谱
  Then 返回图谱 id
  And 模型包含实体类型 服务/人员/项目 与关系类型 维护/依赖
  And 维护约束为 人员→服务、依赖约束为 项目→服务
  And 该图谱可新增节点与边

@KG-S3 @auto:e2e
Scenario: 同团队名称唯一
  When 同团队再次使用已存在的名称建图
  Then 返回冲突

@KG-S4 @auto:e2e
Scenario: 维护模型
  When 管理员新增实体类型 服务/人员/项目
  Then 返回各类型 id
  When 管理员新增带起止约束的关系类型 维护（人员→服务）与 依赖（项目→服务）
  Then 返回关系类型 id
  And 查询模型回显上述起止约束

@KG-S5 @auto:e2e
Scenario: 新增节点校验
  When 使用不存在的实体类型新增节点
  Then 返回参数错误
  When 使用已存在的实体类型新增节点
  Then 返回节点 id

@KG-S6 @auto:e2e
Scenario: 新增边校验起止约束
  Given 图谱已有 人员/服务/项目 三类节点
  When 新增 维护 边但起点类型不符
  Then 返回参数错误
  When 新增 维护 边但终点类型不符
  Then 返回参数错误
  When 按 人员→服务 新增 维护 边
  Then 返回边 id

@KG-S7 @auto:e2e
Scenario: 节点与边分页
  Given 图谱有 5 个节点、2 条边
  When 按每页 2 条查询节点
  Then 首屏返回 2 条且总数为 5
  And 第三屏返回 1 条且总数为 5
  When 按每页 1 条查询边
  Then 返回 1 条且总数为 2
  And 按关系类型筛选边仍返回总数 2

@KG-S8 @auto:e2e
Scenario: 删除被引用的实体类型被拒
  Given 实体类型下仍有节点
  When 管理员删除该实体类型
  Then 返回冲突

@KG-S9 @auto:e2e
Scenario: 删除托管图谱清空图数据
  When 管理员删除托管图谱
  Then 返回成功
  And 该图谱详情返回不存在且团队列表不再包含
  And 图库中该图谱的节点与边被清空
```

## Feature: 外部接入图谱（connected）

```gherkin
@KG-S10 @auto:e2e
Scenario: 接入未知库名按方言处理
  When 管理员接入一个未知的库名
  Then neo4j 方言（多库实例）返回参数错误
  And memgraph 方言（社区版单库）登记成功且库名仅作标识，随后可删除

@KG-S11 @auto:e2e
Scenario: 接入现有数据库并内省模型
  Given 目标图数据库实例可达
  When 管理员接入其默认数据库
  Then 返回图谱 id
  And 模型来源为接入、只读为真且库名正确
  And 内省标签含 KgNode、关系类型含 KG_REL、属性键含 kgId
  And 详情同样为接入且只读

@KG-S12 @auto:e2e
Scenario: 接入图谱只读
  Given 图谱来源为外部接入
  When 尝试新增节点
  Then 返回冲突且提示只读
  When 尝试新增实体类型
  Then 返回冲突且提示只读
```

## Feature: 图览画布与权限（v2）

```gherkin
@KG-S13 @auto:e2e
Scenario: 画布有界子图
  Given 托管图谱已有节点与边
  When 查询画布子图
  Then 返回节点与节点集内部的边
  When 按名称关键字过滤
  Then 仅返回命中的节点

@KG-S14 @auto:e2e
Scenario: 一跳邻接展开
  Given 图谱中存在一条维护边
  When 对起点节点查询一跳邻接
  Then 返回邻居节点与相连的边
  When 对不存在的节点查询邻接
  Then 返回不存在

@KG-S15 @manual
Scenario: 成员对图谱全只读
  Given 普通成员可浏览图谱、模型与节点
  When 成员尝试新增节点或边
  Then 返回禁止（v2 权限收紧：节点/边写操作仅限管理员及以上，v1 曾放开成员）

@KG-S16 @auto:e2e
Scenario: 图谱名称全局唯一
  Given 其他团队已存在同名图谱
  When 本团队使用该名称建图
  Then 返回冲突
```

## Feature: 接入图动态识别（v2.1）

```gherkin
@KG-S17 @auto:e2e
Scenario: 接入图画布有界子图
  Given 外部服务已向图库写入节点与边
  When 查询接入图画布子图
  Then 返回带标签（entityLabel）的节点与带关系类型名（relationName）的边
  When 按标签过滤
  Then 仅返回该标签的节点

@KG-S18 @auto:e2e
Scenario: 接入图一跳邻接展开
  Given 接入图画布已返回节点
  When 对某节点按 elementId 查询一跳邻接
  Then 返回邻居节点与相连的边
  When 对不存在的节点查询邻接
  Then 返回不存在

@KG-S19 @auto:e2e
Scenario: 内省缓存与强制刷新
  Given 接入图 schema 已查询过一次
  When 五分钟内再次查询 schema
  Then 命中 Redis 缓存（fromCache=true）
  When 以 refresh=true 强制刷新
  Then 跳过缓存重新内省（fromCache=false），并与基线 diff 出新增/消失的标签与关系类型
```

## Feature: 图谱头像（v2.2）

```gherkin
@KG-S20 @auto:e2e
Scenario: 设置图谱头像
  Given Owner/Admin 已通过存储直传完成图片上传并登记
  When 以 objectKey 设置图谱头像
  Then 设置成功，详情与列表回显 avatarPath
  When 以未登记的 objectKey 设置头像
  Then 返回不存在（404，防伪造 objectKey）
```

## Feature: 模型属性设置（v2.3）

```gherkin
@KG-S21 @auto:e2e
Scenario: 实体类型属性定义与实例属性
  Given Owner/Admin 在模型页为实体类型定义属性（名称/类型/必填/说明）
  When 查询 schema
  Then 回显属性定义；属性名重复或类型非法返回 400
  When 录入实例并填写属性值
  Then 属性值以 JSON 存储在图库节点（propsJson）并在实例列表回显
  When 编辑实例属性
  Then 新值覆盖旧值并回显
```

> 外部开放接口（`/api/external/knowledge-graph`）场景编号沿用证据脚本 `kg-external-e2e.mjs` 的 KX-\* 体系（KX-01~KX-08），不复用 KG-\*。授权模型：应用 token 即团队级授权（等价团队 Admin 作用于本团队托管图谱），设计见 [sdd.md §5.1](./sdd.md#51-外部开放接口apexternalknowledge-graph)。

## Feature: 外部开放接口（应用 token，KX-*）

```gherkin
@KX-01 @auto:e2e
Scenario: 应用 token 换取与团队级图谱列表
  When 以应用接入 key 换取应用 token
  Then 返回类型为「应用」的 token 对
  When 查询外部图谱列表
  Then 仅返回本团队托管（managed）图谱，不含他团队与接入图
  When 另一团队的应用 token 查询列表
  Then 仅返回该团队自己的图谱

@KX-02 @auto:e2e
Scenario: 跨团队与不存在资源一律不存在
  When 对他团队图谱发起任意读写
  Then 返回不存在（404，不泄露资源存在性）
  When 以随机 id 查询 schema 或节点列表
  Then 返回不存在

@KX-03 @auto:e2e
Scenario: 实体类型与关系类型维护
  When 新增实体类型与带起止约束的关系类型
  Then schema 回显类型与约束
  When 更新与删除类型
  Then schema 同步且仍被引用的类型删除返回冲突（409）

@KX-04 @auto:e2e
Scenario: 节点增删改查与分页邻接
  When 新增节点、分页查询、查详情与一跳邻接、更新、删除
  Then 全部成功且 schema 计数随写入回退
  And 删除节点连带其边（DETACH 级联）

@KX-05 @auto:e2e
Scenario: 边增删改查与起止约束
  When 新增符合约束的边
  Then 返回边 id 且分页/详情正确
  When 新增违反起止约束的边
  Then 返回参数错误（400）

@KX-06 @auto:e2e
Scenario: 节点与边批量导入整批拒绝
  When 批量导入 ≤200 条且全部合法
  Then 逐条落库并回写 id
  When 任一条类型/端点非法或超过 200 条上限
  Then 整批拒绝（400）且已有数据不变

@KX-07 @auto:e2e
Scenario: 接入图对外部只读
  When 对接入（connected）图谱发起节点或类型写操作
  Then 返回冲突（409，接入图只读）

@KX-08 @auto:e2e
Scenario: 外部接口仅接受应用 token
  When 无 token、伪造 token 或内部用户 JWT 调用外部接口
  Then 分别返回未认证/未认证/未认证或禁止
```
