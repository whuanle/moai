# 团队插件模块行为场景（BDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md)

术语：Owner=团队所有者（角色 0）；Admin=管理员（1）；Member=成员（2）。「系统插件」= `is_system=true` 且 `team_id=0` 的插件；「团队插件」= `is_system=false` 且 `team_id=teamId` 的插件。

## Feature: 系统插件私有授权（管理员）

```gherkin
@TP-S1 @auto:e2e
Scenario: 未登录不能访问授权接口
  When 未携带令牌查询系统插件授权
  Then 返回未认证

@TP-S2 @auto:e2e
Scenario: 私有插件授权团队
  Given 存在私有系统插件 P
  When 管理员为 P 授权团队 T
  Then P 的授权列表包含 T，且 isPublic=false

@TP-S3 @auto:e2e
Scenario: 授权全量替换与撤销
  Given 私有系统插件 P 已授权团队 [T1, T2]
  When 管理员将 P 授权改为 [T2]
  Then P 的授权列表仅剩 T2，T1 不再可见

@TP-S4 @auto:e2e
Scenario: 公开插件不可设置授权
  Given 系统插件 P 为公开（isPublic=true）
  When 管理员为 P 设置授权团队
  Then 返回参数错误
  When 管理员查询 P 的授权
  Then 返回 isPublic=true 且 items 为空

@TP-S5 @auto:e2e
Scenario: 授权团队必须存在
  When 管理员为私有插件授权一个不存在的团队
  Then 返回不存在

@TP-S6 @auto:e2e
Scenario: 非管理员不可授权
  When 普通用户查询或更新系统插件授权
  Then 返回禁止
```

## Feature: 团队插件——访问与权限

```gherkin
@TP-S7 @auto:e2e
Scenario: 未登录不能访问团队插件
  When 未携带令牌查询团队插件列表
  Then 返回未认证

@TP-S8 @auto:e2e
Scenario: 只有团队成员可查看
  Given 团队 T 存在
  When 非成员查询 T 的插件列表
  Then 返回不存在（不泄露团队存在性）

@TP-S9 @auto:e2e
Scenario: 管理权限
  When Member 创建/编辑/删除团队插件
  Then 返回禁止
  When Owner/Admin 创建团队插件
  Then 返回成功
```

## Feature: 团队插件——列表可见性

```gherkin
@TP-S10 @auto:e2e
Scenario: 列表返回团队自有插件与可用系统插件
  Given 团队 T 有自己的动态插件 D 与自定义插件 C
  And 存在公开系统插件 S_public 与私有系统插件 S_private（已授权 T）
  When Member 查询 T 的插件列表
  Then 返回 D、C、S_public、S_private，且 D/C 标记为团队自有，S_* 标记为系统插件
  And isTeamOwned 按来源正确

@TP-S11 @auto:e2e
Scenario: 私有系统插件未授权则不可见
  Given 私有系统插件 S_private 未授权团队 T
  When 查询 T 的插件列表
  Then 返回结果不含 S_private
```

## Feature: 团队插件——动态实例

```gherkin
@TP-S12 @auto:e2e
Scenario: 创建团队动态实例
  Given 存在动态模板 dp_tpl
  When Admin 为团队创建实例（实例 key 团队内唯一）
  Then 返回成功且列表包含该实例

@TP-S13 @auto:e2e
Scenario: 实例 key 团队内唯一 & 不与系统 key 冲突
  When 用已存在的实例 key 再创建
  Then 返回冲突

@TP-S14 @auto:e2e
Scenario: 模板必须为动态
  When 用静态插件模板 key 创建团队动态实例
  Then 返回不存在

@TP-S15 @auto:e2e
Scenario: 编辑不改变实例 key
  When Admin 编辑团队动态实例的标题/描述/配置
  Then 实例 key 不变且列表回显新值
```

## Feature: 团队插件——自定义（MCP/OpenAPI）

```gherkin
@TP-S16 @auto:e2e
Scenario: 导入团队 MCP 插件
  Given 配置好一个 MCP 服务
  When Admin 导入团队 MCP 插件
  Then 返回插件 id 且列表包含该插件

@TP-S17 @auto:e2e
Scenario: 导入团队 OpenAPI 插件
  Given 已完成一次 OpenAPI 文件上传
  When Admin 导入团队 OpenAPI 插件
  Then 返回插件 id 且列表包含该插件

@TP-S18 @auto:e2e
Scenario: 团队插件名团队内唯一
  When 同团队创建同名插件
  Then 返回冲突
```

## Feature: 团队插件——删除

```gherkin
@TP-S19 @auto:e2e
Scenario: 删除团队插件
  When Owner 删除本团队插件
  Then 返回成功且列表不再包含
  When Member 删除团队插件
  Then 返回禁止
```
