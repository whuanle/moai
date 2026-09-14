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
  When 团队 Owner/Admin 打开团队页「内部应用」分区
  Then 可看到「新建应用」按钮与每张卡片右上角的「管理」入口

@AP-S12 @auto:unit
Scenario: 普通成员进入团队只能使用
  When Member 打开团队页
  Then 分区菜单只出现 信息 / 内部应用 / 知识库
  And 不出现 外部应用 / 应用接入 / 成员管理 / 模型网关 / 插件 / 环境变量 / 设置
  And 内部应用分区只读：无新建按钮、无「管理」入口
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
Scenario: 内部应用「公开到平台」开关（创建与编辑均可设置）
  When Admin 创建内部应用时开启「公开到平台」
  Then 详情与列表均返回已公开
  When Admin 更新为关闭
  Then 详情返回未公开
  And 列表以卡片底部标签展示开关状态（前端）
  And 申请「需要授权访问」的内部应用返回参数错误
```

## Feature: 应用卡片与管理入口

```gherkin
@AP-S15 @auto:unit
Scenario: 应用以卡片展示，管理入口在卡片右上角
  When 团队管理员打开团队页「应用」分区
  Then 每个应用渲染为一张卡片（头像/名称/类型/描述/创建时间/发布与公开状态）
  And 卡片右上角有「管理」入口，点击进入该应用的管理页
  When 普通成员打开同一分区
  Then 卡片只读：没有「管理」入口，也没有「新建应用」

@AP-S16 @auto:unit
Scenario: 应用管理页为单页左右分栏
  When 管理员打开 Agent 应用的管理页
  Then 左栏是「应用信息」（头像/类型/名称/描述/公开到平台或授权开关）
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

## Feature: 内部 / 外部应用

```gherkin
@AP-S20 @auto:e2e @auto:unit
Scenario: 外部应用隔离与创建
  When Admin 创建外部应用并设置需要授权
  Then 返回应用 id，详情返回 isExternal/isAuth 为真
  And 团队内部应用列表不包含该外部应用
  When Admin 查询外部应用列表
  Then 返回 200 且包含该应用
  When Member / 非成员查询外部应用列表
  Then 分别返回禁止 / 不存在
  When 为内部应用设置需要授权，或为外部应用设置公开到平台
  Then 返回参数错误

@AP-S21 @auto:e2e @auto:unit
Scenario: 平台公开应用可被平台内非成员使用
  When 非成员查看未发布的公开内部应用详情
  Then 返回不存在
  When Admin 发布该公开应用后，非成员再次查看
  Then 返回 200 且 myRole 为 -1
  And 公开应用列表包含该应用
  And 非成员可为该应用创建会话

@AP-S22 @auto:unit
Scenario: 前端外部应用分区与应用广场
  When 团队管理员打开「外部应用」分区
  Then 可新建外部应用并看到 发布状态 与 需授权/免授权 标签
  And 普通成员看不到「外部应用」分区
  When 任意登录用户打开「应用广场」
  Then 展示平台公开应用并可进入对话；无公开应用时展示空态
```

## Feature: 应用接入（外部应用授权 key）

```gherkin
@AP-S23 @auto:e2e @auto:unit
Scenario: 应用接入增删改查与授权校验
  When 团队管理员创建应用接入并授权本团队的外部应用
  Then 返回接入 id 与 key 原文
  And 列表回显完整 key（可再次查看）
  When 授权列表包含非本团队或非外部的应用
  Then 返回参数错误
  When 普通成员 / 非成员查询或创建应用接入
  Then 分别返回禁止 / 不存在
  When 管理员删除该应用接入
  Then 返回成功且列表不再包含
```

## Feature: 应用工作台与调试会话

```gherkin
@AP-S40 @auto:e2e
Scenario: 管理员对未发布 Agent 应用创建调试会话
  Given 团队管理员已创建一个尚未发布的 Agent 应用
  When 管理员为该应用创建调试会话
  Then 返回调试会话标识
  And 该调试会话不出现在正式会话列表

@AP-S41 @auto:e2e
Scenario: 调试会话的角色与应用类型门禁
  When 普通成员或非成员为该应用创建调试会话
  Then 分别返回禁止与不存在
  When 为流程应用创建调试会话
  Then 返回参数错误
  When 为不存在的应用创建调试会话
  Then 返回不存在
```

## Feature: 应用对话日志

```gherkin
@AP-S42 @auto:e2e @auto:unit
Scenario: 查看应用对话日志
  When 团队管理员查询该应用的对话日志
  Then 返回分页结果且包含团队用户的正式会话
  And 可按标题关键字与用户类型过滤
  When 管理员查询某会话的消息详情
  Then 返回该会话的消息列表
  When 普通成员或非成员查询日志
  Then 分别返回禁止与不存在
  When 查询不属于该应用的会话消息
  Then 返回不存在
```

## Feature: 应用用量监控

```gherkin
@AP-S43 @auto:e2e @auto:unit
Scenario: 查看应用用量监控
  When 团队管理员查询该应用的用量
  Then 返回调用次数与 token 汇总
  And 返回按模型的用量分布
  When 普通成员或非成员查询用量
  Then 分别返回禁止与不存在
  When 查询不存在应用的用量
  Then 返回不存在
```
