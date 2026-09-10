# 知识图谱模块行为场景（BDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 证据：[local-dev/kg-e2e.mjs](../../local-dev/kg-e2e.mjs)

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
Scenario: 接入不存在的数据库被拒
  When 管理员接入一个不存在的数据库
  Then 返回参数错误

@KG-S11 @auto:e2e
Scenario: 接入现有数据库并内省模型
  Given 目标 Neo4j 实例可达
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
