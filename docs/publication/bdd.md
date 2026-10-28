# publication 上架审核模块 行为规格（BDD，Gherkin）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md)（场景→验证映射） ｜ [SOP](./sop.md)（操作与验收流程） ｜ 上游：[../app/bdd.md](../app/bdd.md)（应用 @AP-S14/S21）、[../team/bdd.md](../team/bdd.md)（角色门禁）
> 编号规则与标签语义见 [../DOC-STANDARD.md](../DOC-STANDARD.md) 第 3 节。

```gherkin
Feature: 上架申请（团队侧）
  Background:
    Given 已注册用户 owner、member 与 outsider，owner 创建团队 T 并拉入 member
    And owner 在团队 T 下创建了内部应用 A（已发布）与外部应用 E

  @PB-S1 @auto:e2e
  Scenario: 未登录访问被拒绝
    When 未登录用户申请上架
    Then 返回 401

  @PB-S2 @auto:e2e
  Scenario: 申请参数与资源校验
    When owner 以非法资源类型或空资源 id 申请上架
    Then 返回 400 与字段级错误信息
    When owner 为不存在的应用或提示词申请上架
    Then 返回 404

  @PB-S3 @auto:e2e
  Scenario: 申请角色门禁
    When member 为应用 A 申请上架
    Then 返回 403（需要团队 Admin 及以上）
    When outsider 为应用 A 申请上架
    Then 返回 404（非成员不泄露资源存在性）

  @PB-S4 @auto:e2e
  Scenario: 团队管理员按版本号申请上架
    When owner 为应用 A 附申请说明与版本号 aaa.bb.cc 提交上架申请
    Then 申请成功并返回审核记录 id，记录保存该版本号（int 编码）
    When owner 为外部应用 E 申请上架
    Then 返回 400（外部应用不支持公开到平台）
    When 为已公开的资源再次申请
    Then 返回 409（已公开无需重复申请）

  @PB-S17 @auto:e2e
  Scenario: 审核列表排序：待审核恒排最前
    When 查询管理员列表或团队列表（含已审批与未审批记录）
    Then 待审核记录全部排在已通过/已驳回之前
    And 同组内按申请时间倒序（最新申请在前）

  @PB-S16 @auto:e2e
  Scenario: 应用上架申请的版本号校验（上架把最新的团队发布上架）
    When owner 为应用 A 不携带版本号提交上架申请
    Then 申请成功，记录版本号自动取应用当前发布版本（无需单独填写）
    When owner 携带非法格式版本号（如 "1.2"）提交
    Then 返回 400（应为 aaa.bb.cc）
    When owner 携带低于应用当前版本的版本号（如当前 1.0.0 时填 "0.0.9"）提交
    Then 返回 400（版本号不能低于当前版本，只能递增不能回退）
    When owner 为提示词/技能申请上架时携带版本号
    Then 返回 400（仅应用申请上架需要版本号）
    When 团队列表或管理员列表查询审核记录
    Then 应用类型的记录回显申请时的版本号

  @PB-S5 @auto:e2e
  Scenario: 同一资源同时只能有一条待审核申请
    When owner 为应用 A 再次提交上架申请
    Then 返回 409

Feature: 上架审核列表（团队侧与管理员侧）
  Background:
    Given owner 已为应用 A 提交上架申请，记录处于待审核状态

  @PB-S6 @auto:e2e
  Scenario: 团队侧查看申请与审批状态
    When owner 查询团队 T 的上架审核列表
    Then 返回 200 且包含该待审核记录（资源类型/资源名称快照/申请说明）
    And member 也可查询团队列表
    When outsider 查询团队 T 的上架审核列表
    Then 返回 404
    When 按审核状态或资源类型过滤
    Then 仅返回匹配的记录

  @PB-S7 @auto:e2e
  Scenario: 管理员接口门禁
    When member 查询全平台上架列表或审批申请
    Then 均返回 403（仅系统管理员）

  @PB-S8 @auto:e2e
  Scenario: 管理员查看全平台上架申请
    When 系统管理员查询上架审核列表
    Then 返回 200 且包含团队 T 的申请
    And 记录带团队名称与申请人姓名（供审批判断）
    And 可按待审核状态与应用类型过滤

Feature: 撤回与审批
  Background:
    Given owner 已为应用 A 提交上架申请且处于待审核状态，系统管理员可访问审批接口

  @PB-S9 @auto:e2e
  Scenario: 团队撤回待审核申请
    When member 撤回该申请
    Then 返回 403
    When owner 撤回该申请
    Then 撤回成功，团队列表不再显示该记录
    And 应用 is_public 仍为 false
    When owner 对同一记录再次撤回
    Then 返回 404
    When owner 重新为应用 A 提交申请
    Then 申请成功（撤回不占用待审核唯一约束）

  @PB-S10 @auto:e2e
  Scenario: 管理员驳回申请
    When 系统管理员以审批意见驳回该申请
    Then 驳回成功，团队列表状态为已驳回且回显审批意见
    And 应用 is_public 仍为 false
    When 系统管理员对该记录再次审批
    Then 返回 409（已审批不可重复审批）
    When owner 撤回已驳回的记录
    Then 返回 409（仅待审核可撤回）
    When owner 重新为应用 A 提交申请
    Then 申请成功（驳回后可重新申请）

  @PB-S11 @auto:e2e
  Scenario: 管理员通过上架，is_public 才变为 true
    When 系统管理员通过该申请
    Then 通过成功，团队列表状态为已通过且记录审批人姓名
    And 应用 A 的 is_public 变为 true
    And 应用 A 出现在平台公开应用列表（应用广场）
    When owner 为已公开的应用 A 再次申请
    Then 返回 409

Feature: 下架（取消公开）
  应用撤回发布时由应用模块联动自动下架并撤下待审申请（见 [@AP-S96](../app/bdd.md#ap-s96)）
  Background:
    Given 应用 A 已通过上架审核公开（is_public=true，见 [@PB-S11](#pb-s11)），另有未公开的应用 B

  @PB-S18 @auto:e2e
  Scenario: 团队管理员下架已公开应用
    When member 为应用 A 下架
    Then 返回 403（需要团队 Admin 及以上）
    When outsider 为应用 A 下架
    Then 返回 404（非成员不泄露资源存在性）
    When owner 下架应用 A
    Then 下架成功，应用 is_public 变回 false 且平台公开应用列表不再包含
    And 应用团队发布状态不受影响（已发布仍已发布）
    When owner 对未公开的应用 B 下架
    Then 返回 409（未公开无需下架）
    When owner 重新为应用 A 申请上架
    Then 申请成功（下架后可重新申请）

  @PB-S19 @manual
  Scenario: 个人提示词/技能仅创建人可下架
    When 非创建人对个人提示词或个人技能下架
    Then 返回 403
    When 对系统内置技能下架
    Then 返回 400（系统内置技能无需下架）

Feature: 前端页面（审批上架菜单与应用侧入口）

  @PB-S12 @manual
  Scenario: 管理员「审批上架」一级菜单
    When 系统管理员打开侧边栏「审批上架」菜单
    Then 展示全平台的上架申请列表（资源类型/名称/团队/申请人/申请时间/申请说明/状态/审批意见/审批人）
    And 可按状态与资源类型筛选、刷新
    And 待审核记录可「通过」（二次确认）或「驳回」（弹窗填审批意见）
    When 非管理员用户访问该页面
    Then 菜单不可见，直接访问路由被重定向

  @PB-S13 @manual
  Scenario: 应用配置页的上架状态与申请入口
    When 团队管理员打开内部应用的上架管理入口
    Then 「上架状态」展示 未公开/审核中/已驳回/已公开 之一
    And 未公开时可「申请上架」（弹窗填申请说明），审核中可「撤回申请」，已驳回可「重新申请」，已公开可「下架」
    And 应用创建与新建弹窗不再提供公开开关
```
