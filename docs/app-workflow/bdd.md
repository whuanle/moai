# 流程应用模块行为规格（BDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 上游：[../app/bdd.md](../app/bdd.md) ｜ 证据：[local-dev/workflow-e2e.mjs](../../local-dev/workflow-e2e.mjs)

```gherkin
Feature: 流程应用编排设计与执行
  团队管理员用可视化设计器编排流程应用（保存草稿、发布版本），
  并同步调试执行查看节点级状态；普通成员只读。
```

@WF-S1 @auto:e2e
Scenario: 创建流程应用作为编排载体
  Given 团队管理员已登录
  When 创建 app_type=workflow 的应用
  Then 创建成功并返回应用 id，可在工作台进入「流程编排」分区

@WF-S2 @auto:e2e
Scenario: 保存并查看编排草稿
  Given 团队管理员在画布上编排了 start→compute(JavaScript)→check(条件)→hit/miss→end 的流程
  When 保存草稿（流程定义 JSON + 编辑器画布 JSON）
  Then 草稿保存成功，查询配置返回两份 JSON，版本为 0、状态为「草稿有变更」
  But 请求缺少流程定义或团队 id 非法时返回 400

@WF-S3 @auto:e2e
Scenario: 编排权限门禁
  Given 团队成员（Member）与非团队成员存在
  When Member 保存草稿
  Then 返回 403
  When Member 查询配置
  Then 返回 200（成员可读）
  When 非团队成员查询配置
  Then 返回 404

@WF-S4 @auto:e2e
Scenario: 非法定义拒绝发布
  Given 草稿的条件节点只有 true 一条出边（缺少 false 分支）
  When 发布流程
  Then 返回 400 且错误信息指明缺少 false 出边，版本号不增长

@WF-S5 @auto:e2e
Scenario: 合法流程发布生成不可变版本快照
  Given 草稿定义通过图结构全量校验（单开始、连通、无环、条件分支完整、变量仅引用上游）
  When 发布流程
  Then 版本号递增、状态置为「已发布」、生成不可变已发布快照
  And 所属应用被置为已发布

@WF-S6 @auto:e2e
Scenario: 调试执行命中分支
  Given 已保存的流程中 JavaScript 节点按输入计算布尔结果，命中分支用插值拼接上游输出
  When 以启动参数 {"query":"yes"} 调试执行
  Then 实例终态为 completed，最终输出 answer 为「命中:sum-yes」
  And 命中分支与上游节点均为 completed，未命中分支为 skipped

@WF-S7 @auto:e2e
Scenario: 调试执行未命中分支
  When 以启动参数 {"query":"no"} 调试执行
  Then 实例终态为 completed，missAnswer 为「未命中」且 answer 为 null（required=false 允许引用未执行分支）
  And 命中分支为 skipped

@WF-S8 @auto:e2e
Scenario: 启动参数校验
  When 缺少开始节点声明的必需启动参数执行
  Then 实例挂起（suspended）且开始节点为 failed，错误信息指明缺失参数
  When 启动参数不是合法 JSON
  Then 返回 400

@WF-S9 @auto:e2e
Scenario: 运行历史与节点级详情
  Given 该流程已有至少 3 次调试运行
  When 团队管理员分页查询运行历史
  Then 列表包含实例状态、类型（调试/正式）、定义版本、错误信息与触发人姓名
  When 查看某实例详情
  Then 返回每个节点的执行状态、输入与输出
  But 团队成员（Member）查询运行历史时返回 403
```
