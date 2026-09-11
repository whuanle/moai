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
Scenario: 实例 key 全局唯一 & 不与系统 key 冲突
  When 其它团队用已存在的实例 key 创建
  Then 返回冲突
  When 同团队用已有实例 key 保存
  Then 视为更新该实例（实例 key 不可变）

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

## Feature: 团队插件——能力对齐管理员面板

```gherkin
@TP-S20 @auto:e2e
Scenario: 查询可用动态模板
  Given 团队 T 存在
  When 成员查询动态模板列表
  Then 返回含动态模板（isDynamic=true）
  When 非成员查询
  Then 返回不存在

@TP-S21 @auto:e2e
Scenario: 查看团队插件函数列表
  When 成员查看团队自有/已授权插件的函数列表
  Then 返回函数列表
  When 非成员查看
  Then 返回不存在

@TP-S22 @auto:e2e
Scenario: 查看团队自定义插件详情
  When 成员查看本团队自定义插件详情
  Then 返回服务器地址、Header/Query、OpenAPI 文件等
  When 查看非本团队且未授权的插件
  Then 返回不存在

@TP-S23 @auto:e2e
Scenario: 刷新团队 MCP 插件
  When Owner/Admin 刷新团队自有 MCP 插件
  Then 重新拉取函数列表并返回成功
  When Member 刷新
  Then 返回禁止

@TP-S24 @auto:e2e
Scenario: 预上传团队 OpenAPI 文件
  When Owner/Admin 预上传 .json/.yaml 文件
  Then 返回 fileId 与签名上传地址
  When 非成员预上传
  Then 返回不存在

@TP-S25 @auto:e2e
Scenario: 成员运行团队可用插件
  When 成员运行团队自有动态实例
  Then 路由可达并返回执行结果
  When 非成员运行
  Then 返回不存在

@TP-S26 @auto:e2e
Scenario: 创建团队动态实例后正确关联
  When Owner 创建团队动态实例
  Then 列表包含该实例且带创建/更新人与时间字段

@TP-S27 @auto:e2e
Scenario: 跨团队实例 key 冲突
  Given 团队 A 已创建实例 key K
  When 团队 B 用同一 key K 创建
  Then 返回冲突
```

## Feature: 团队插件——前端分栏（@manual）

```gherkin
@TP-S28 @manual
Scenario: 团队插件页分为自定义与动态两个 Tab
  When 团队 Owner/Admin 打开团队详情 → 插件
  Then 页面分为「自定义插件」「动态插件」两个 Tab
  And 自定义 Tab 展示导入 MCP/OpenAPI、编辑、删除（团队自有时）
  And 动态 Tab 展示新建实例、运行、编辑、删除（团队自有时）
  And Member 仅可运行、查看函数与筛选，无管理入口
```
