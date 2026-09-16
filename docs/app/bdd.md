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
Scenario: 公开（is_public）不再提供直接开关，仅上架审核可设置
  When Admin 创建内部应用（创建接口不含 is_public）
  Then 创建成功且详情与列表均返回未公开
  When Admin 更新基础信息（更新接口不含 is_public）
  Then 更新成功且 is_public 保持未公开
  And 列表以卡片底部标签展示公开状态（前端）
  And 申请「需要授权访问」的内部应用返回参数错误
  And 公开的申请与审批流转见 [@PB-S4](../publication/bdd.md#pb-s4)~[@PB-S11](../publication/bdd.md#pb-s11)
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
  Then 左栏是「应用信息」（头像/类型/名称/描述/上架状态或授权开关）
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
  When 为内部应用设置需要授权
  Then 返回参数错误
  And 公开（is_public）只能经上架审核设置，创建/更新接口不再提供该字段（见 @AP-S14）

@AP-S21 @auto:e2e @auto:unit
Scenario: 平台公开应用可被平台内非成员使用
  When 非成员查看未发布的内部应用详情
  Then 返回不存在
  When Admin 发布该应用，团队申请上架并由系统管理员审批通过（[@PB-S11](../publication/bdd.md#pb-s11)）
  Then 应用 is_public 变为 true
  When 非成员再次查看
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

## Feature: 外部应用接入 token（/api/external）

```gherkin
@EA-S1 @auto:e2e
Scenario: 外部接口必须持外部 token 访问
  When 未携带令牌查询外部授权应用列表
  Then 返回未认证
  When 以内部用户令牌查询外部授权应用列表
  Then 返回未认证（内部 JWT 与外部 token 的 audience 不同，互不通用）

@EA-S2 @auto:e2e
Scenario: 应用接入 key 换取应用 token
  When 第三方应用仅凭应用接入 key 请求外部 token
  Then 返回 access_token、refresh_token、有效期与类型为「应用」
  When 以该应用 token 查询外部授权应用列表
  Then 返回该接入授权的全部已发布外部应用，不含授权范围外的应用

@EA-S3 @auto:e2e
Scenario: 外部 token 不能访问内部接口
  When 以外部应用 token 调用内部应用列表接口
  Then 返回未认证

@EA-S4 @auto:e2e
Scenario: 凭 key 为外部用户签发用户 token（绑定单应用）
  When 第三方应用凭 key、目标应用 id 与外部用户标识请求外部 token
  Then 返回类型为「用户」的 token 对与外部用户 id
  When 同一接入下用相同外部用户标识再次申请
  Then 复用同一外部用户 id（继承同一身份）
  When 以该用户 token 查询外部授权应用列表
  Then 仅返回其绑定的单个应用

@EA-S5 @auto:e2e
Scenario: 授权范围与请求校验
  When 用户 token 的目标应用不在接入授权范围内
  Then 返回禁止
  When 凭 key 换用户 token 但缺少应用 id 或外部用户标识等必要参数
  Then 返回参数错误
  When 不带 key 直接换取需授权应用的外部 token，或使用无效 key
  Then 分别返回禁止与未认证

@EA-S6 @auto:e2e
Scenario: 免授权外部应用匿名换取 token
  When 仅凭应用 id 请求外部 token，且该应用为需授权=false 的外部应用
  Then 返回类型为「用户」的 token 对与随机临时外部用户标识

@EA-S7 @auto:e2e
Scenario: 外部 token 刷新
  When 以 refresh_token 请求刷新
  Then 返回新的 access_token 与 refresh_token，且身份不变
  When 以刷新后的 access_token 查询外部授权应用列表
  Then 返回 200
  When 以 access_token 冒充 refresh_token 刷新
  Then 返回未认证
  When 刷新应用 token
  Then 返回类型为「应用」的 token 对，授权范围以接入当前配置为准

@EA-S8 @auto:e2e
Scenario: 删除应用接入即吊销其全部外部 token
  When 管理员删除应用接入后，其应用 token 与用户 token 分别请求刷新
  Then 均返回未认证
  When 以已删除接入的 key 再次换取 token
  Then 返回未认证
```

## Feature: 外部会话与对话（/api/external/agent）

```gherkin
@EA-S9 @auto:e2e
Scenario: 外部用户创建并查看自己的会话
  When 外部用户 token 对其授权范围内已发布的 Agent 外部应用创建会话
  Then 返回会话 id
  When 查询该应用的会话列表
  Then 返回 200 且包含该会话
  When 查询该会话的消息
  Then 返回 200 且消息列表为空

@EA-S10 @auto:e2e
Scenario: 外部会话与对话端点的鉴权与范围
  When 应用 token（无用户身份）创建会话
  Then 返回禁止
  When 外部用户 token 对授权范围外的应用创建会话
  Then 返回禁止
  When 无 token、无效 token 或内部用户 token 访问外部会话/对话端点
  Then 返回未认证
  When 其他外部用户查询该会话的消息
  Then 返回不存在（不泄露会话存在性）
  When 外部用户 token 对授权范围外的应用发起对话
  Then 返回禁止
  When 外部用户 token 以会话 id 发起对话
  Then 请求被受理（会话归属校验通过，进入派发链路）
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

## Feature: 会话专家提示词（app_agent_session.prompt_id）

```gherkin
@AP-S44 @auto:e2e @auto:unit
Scenario: 会话绑定专家提示词，后续对话追加到系统提示词
  Given 用户在应用对话页展开右侧专家侧边栏（本人个人提示词 + 所在团队提示词）
  When 为会话选择一个可用专家（未发送过消息则随会话创建一并绑定）
  Then 绑定成功，会话列表回读该会话的提示词 id
  And 后续每轮对话装配 Agent 时，专家提示词内容追加在应用系统提示词之后
  When 再次点击同一专家或点击提示条上的取消
  Then 清除绑定（提示词 id 置 0），后续对话不再携带
  When 切换到其他会话
  Then 按该会话绑定的提示词回显选中态

@AP-S45 @auto:e2e
Scenario: 不可用专家与越权操作被拒绝
  When 绑定他人个人提示词或不存在的提示词
  Then 返回不存在
  When 提示词 id 为负数
  Then 返回参数错误
  When 非会话归属用户设置会话提示词
  Then 返回不存在
  When 创建会话时绑定不可用的提示词
  Then 返回不存在且不产生会话
```

## Feature: 访问点配置（app_access_point）

```gherkin
@EA-S11 @auto:e2e
Scenario: 团队管理员维护外部应用的访问点配置
  When 未保存过配置时查询访问点配置
  Then 返回默认值（面板宽 380、高 560、启用）而不返回不存在
  When 管理员保存标题/主题色/位置/面板尺寸/启用等配置
  Then 保存成功且回读一致
  When 主题色不是 #RRGGBB 或面板尺寸越界
  Then 返回参数错误
  When 对内部应用保存访问点配置
  Then 返回参数错误

@EA-S12 @auto:e2e
Scenario: 悬浮组件的公开配置与脚本托管
  When 匿名查询外部应用的访问点公开配置
  Then 返回 200 且包含应用名、生效后的标题/位置/尺寸与 isAuth/enabled
  When 应用不存在或非外部应用
  Then 返回不存在
  When 匿名请求 /embed/moai-widget.js
  Then 返回 200 的 JavaScript 脚本
```
