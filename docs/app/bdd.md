# 应用管理模块行为场景（BDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md)

## Feature: 访问与权限

```gherkin
@AP-S1 @auto:e2e
Scenario: 未登录不能访问
  When 未携带令牌查询应用列表
  Then 返回未认证

@AP-S3 @auto:e2e
Scenario: 管理权限（应用是团队下的产物）
  When Member 创建/更新应用或设置头像
  Then 返回禁止
  When 非成员查询列表/详情或创建应用
  Then 返回不存在
```

## Feature: 创建与校验

```gherkin
@AP-S2 @auto:e2e
Scenario: 校验
  When 以空名称/超长名称/非法应用类型/非法 teamId 创建应用
  Then 返回参数错误

@AP-S4 @auto:e2e
Scenario: 创建两种类型的应用
  When Admin 创建 Agent 应用与流程应用
  Then 返回应用 id（Guid）

@AP-S5 @auto:e2e
Scenario: 团队内应用名唯一
  When 同团队创建同名应用
  Then 返回冲突
```

## Feature: 查询

```gherkin
@AP-S6 @auto:e2e
Scenario: 列表
  When 团队成员查询团队应用列表
  Then 返回 200、含 myRole，且按创建时间倒序
  And 列表项含应用类型（agent/workflow）

@AP-S7 @auto:e2e
Scenario: 详情
  When 团队成员查看应用详情
  Then 返回名称/描述/类型/头像 objectKey/创建时间
  When 非成员或应用不存在
  Then 返回不存在
```

## Feature: 更新基础信息

```gherkin
@AP-S8 @auto:e2e
Scenario: 更新名称与描述
  When Admin 更新名称/描述
  Then 返回成功且详情生效
  And 应用类型保持不变（请求中的 appType 被忽略）
  When Admin 将名称改为团队内已有应用的名称
  Then 返回冲突
```

## Feature: 头像

```gherkin
@AP-S9 @auto:e2e
Scenario: 设置头像
  When Admin 使用未登记/未完成上传的 objectKey
  Then 返回不存在
  When Admin 使用已完成上传登记的文件
  Then 返回成功且详情回填 objectKey
```

## Feature: 边界

```gherkin
@AP-S10 @auto:e2e
Scenario: 团队不存在
  When 查询不存在团队的应用列表
  Then 返回不存在
```

## Feature: 入口归属（应用在团队内管理）

```gherkin
@AP-S11 @auto:unit
Scenario: 应用入口只在团队内
  Then 侧边栏没有一级「应用」菜单，也没有 /app 路由
  When 团队 Owner/Admin 打开团队页「应用」分区
  Then 可看到「新建应用」按钮与每张卡片右上角的「管理」入口

@AP-S12 @auto:unit
Scenario: 普通成员进入团队只能使用
  When Member 打开团队页
  Then 分区菜单只出现 信息 / 应用 / 知识库
  And 不出现 成员管理 / 模型网关 / 插件 / 环境变量 / 设置
  And 应用分区只读：无新建按钮、无「管理」入口
  When Member 直接访问管理分区或应用管理页的 URL
  Then 回落到「信息」分区
```

## Feature: 头像与「允许外部使用」

```gherkin
@AP-S13 @auto:e2e @auto:unit
Scenario: 创建应用时可设置头像
  When Admin 预上传图片取得 objectKey，并随创建请求一并提交
  Then 创建成功且详情回填该 objectKey
  When Admin 用未登记/未完成上传的 objectKey 创建应用
  Then 返回不存在
  When Member 或非成员创建
  Then 分别返回禁止 / 不存在

@AP-S14 @auto:e2e @auto:unit
Scenario: 「允许外部使用」开关（创建与编辑均可设置）
  When Admin 创建时开启「允许外部使用」
  Then 详情与列表均返回已开启
  When Admin 更新为关闭
  Then 详情返回未开启
  And 列表以卡片底部标签展示开关状态（前端）
  And 开关仅落库，团队外用户使用应用的能力尚未开放
```

## Feature: 应用卡片与管理入口

```gherkin
@AP-S15 @auto:unit
Scenario: 应用以卡片展示，管理入口在卡片右上角
  When 团队管理员打开团队页「应用」分区
  Then 每个应用渲染为一张卡片（头像/名称/类型/描述/创建时间/外部使用状态）
  And 卡片右上角有「管理」入口，点击进入该应用的管理页
  When 普通成员打开同一分区
  Then 卡片只读：没有「管理」入口，也没有「新建应用」

@AP-S16 @auto:unit
Scenario: 应用管理页为单页左右分栏
  When 管理员打开 Agent 应用的管理页
  Then 左栏是「应用信息」（头像/类型/名称/描述/允许外部使用）
  And 右栏是「Agent 配置」（对话模型/提示词/插件/知识库）
  And 页面没有左侧分区菜单
  When 打开流程应用的管理页
  Then 右栏只提示流程应用配置能力尚未开放
```

## Feature: Agent 应用配置（对话模型 / 插件 / 知识库 / 提示词）

```gherkin
@AP-S17 @auto:e2e
Scenario: 配置读写与权限
  When 团队成员查询 Agent 应用配置
  Then 返回 200；未保存过时为空配置（提示词空串、模型为空 Guid、知识库与插件为空）
  When 未登录查询
  Then 返回未认证
  When 非成员查询
  Then 返回不存在
  When Member 保存配置
  Then 返回禁止

@AP-S18 @auto:e2e @auto:unit
Scenario: 只能绑定该团队有权使用的资源
  When Admin 保存配置，绑定团队可用模型、本团队知识库与团队可访问插件
  Then 返回成功，重新查询回读一致
  When 绑定不属于本团队的知识库
  Then 返回参数错误，且原配置不被改写
  When 绑定该团队无权使用的插件
  Then 返回参数错误
  When 绑定该团队无权使用的模型
  Then 返回参数错误，且原配置不被改写
  And 管理页的可选项只来自 团队模型列表 / 团队插件列表 / 本团队知识库（前端）

@AP-S19 @auto:e2e
Scenario: 配置校验与类型约束
  When 提示词超过 4000 字
  Then 返回参数错误
  When 对流程应用保存配置
  Then 返回参数错误
  When 对不存在的应用查询/保存配置
  Then 返回不存在
```
