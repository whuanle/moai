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
  When 对流程应用保存配置（该类型仅对话开场白适用，模型/知识库/插件/技能不校验不落库）
  Then 返回成功
  When 对不存在的应用查询/保存配置
  Then 返回不存在

@AP-S69 @auto:e2e @auto:unit
Scenario: 绑定知识图谱（仅本团队托管图）
  When Admin 保存配置，绑定本团队托管知识图谱
  Then 返回成功，重新查询回读 graphIds 一致
  When 绑定不属于本团队的知识图谱
  Then 返回参数错误且文案含「知识图谱」，原配置不被改写
  When 绑定本团队接入图（connected）
  Then 返回参数错误（接入图不支持应用侧检索绑定）
  And 应用配置页图谱可选项仅列本团队托管图（前端过滤）
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

```gherkin
@AP-S24 @auto:vitest
Scenario: 应用接入区块顶部展示 key 用途提示
  When 团队管理员进入团队管理的应用接入区块
  Then 区块顶部展示提示：第三方系统可通过应用接入 key 换取外部 token，接入本平台并操作该团队的资源
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
  Given 用户在应用对话页打开「应用设置」面板（专家列表 = 本人个人提示词 + 所在团队提示词）
  When 为会话选择一个可用专家并保存（未发送过消息则随会话创建一并绑定）
  Then 绑定成功，会话列表回读该会话的提示词 id
  And 后续每轮对话装配 Agent 时，专家提示词内容追加在应用系统提示词之后
  When 在设置面板再次点击同一专家保存、或点击提示条上的取消
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

## Feature: 对话开场白（Agent 应用）

```gherkin
@AP-S46 @auto:e2e @auto:unit
Scenario: 配置开场白并在新会话开始时展示
  When Admin 在 Agent 配置中启用对话开场白并填写内容，随配置一并保存
  Then 保存成功，配置回读一致
  And 应用详情（成员可读）随详情下发开场白与启用状态
  And 聊天页与调试面板在新会话开始时先展示该开场白（不参与模型上下文、不入会话历史）

@AP-S47 @auto:e2e @auto:unit
Scenario: 开场白的权限、开关与校验
  When Member 保存开场白配置
  Then 返回禁止
  When 关闭开关保存
  Then 详情返回未启用且内容保留
  When 开场白超过 4000 字
  Then 返回参数错误且原配置不被改写
  When 查看流程应用详情
  Then 开场白恒为空串且不启用
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

## Feature: 外部应用能力限制（沙箱与技能）

```gherkin
@EA-S13 @auto:e2e
Scenario: 外部应用不能绑定技能也不能开启沙箱
  Given 团队已创建并发布外部 Agent 应用
  When 管理员保存 Agent 配置且显式携带非空技能列表
  Then 返回参数错误提示外部应用不能绑定技能
  When 管理员保存 Agent 配置且执行参数中沙箱启用
  Then 返回参数错误提示外部应用不能开启沙箱
  When 管理员保存不带技能且沙箱关闭的配置
  Then 保存成功且回读技能为空、沙箱未启用
  When 内部应用保存启用沙箱的配置
  Then 保存成功（限制仅作用于外部应用）

@EA-S14 @manual
Scenario: 外部应用对话装配强制关闭沙箱与技能
  Given 外部应用的生效配置（发布快照或存量草稿）携带技能或已启用沙箱
  When 外部用户或管理员与其对话装配 Agent
  Then 按外部应用限制克隆生效配置：技能清空、沙箱关闭，不暴露任何沙箱/技能工具
  And 原草稿行与发布快照不被修改
```

## Feature: 沙箱资源上限（应用配置强校验）

```gherkin
@AP-S48 @auto:e2e @auto:unit
Scenario: 沙箱配置受系统上限约束
  Given 超级管理员已在系统设置收紧沙箱上限（如存活 600 秒 / CPU 2000m / 内存 1Gi）
  When 任意登录用户查询沙箱上限
  Then 返回三项上限（应用配置页据此约束取值范围）
  When 团队 Admin 保存 Agent 应用配置且启用的沙箱存活时间、CPU 或内存任一超出上限
  Then 返回请求错误（400）提示不得超出系统上限
  When 沙箱 CPU 或内存数量格式非法（如 "fast"）
  Then 返回请求错误（400）提示格式无效
  When 沙箱未启用时携带超限值保存
  Then 保存成功（仅暂存不生效，避免收紧上限阻断其他字段的保存）
  When 保存等于上限的合法配置
  Then 保存成功且回读一致
```

## Feature: 应用信息分区（Agent 应用工作台）

```gherkin
@AP-S49 @auto:unit
Scenario: 工作台「信息」分区维护应用基础信息
  Given 团队成员打开 Agent 应用工作台
  Then 左侧菜单在「配置」之后展示「信息」项（流程应用工作台无此项）
  When 进入「信息」分区
  Then 可查看并维护应用头像、类型、名称与描述
  And 内部应用展示上架状态（公开/待审核/被驳回），管理员可申请上架、撤回申请
  And 外部应用展示访问授权开关
  When 普通成员进入「信息」分区
  Then 表单只读且无保存入口
```

## Feature: 应用对话界面（欢迎态与输入卡）

```gherkin
@AP-S50 @auto:unit
Scenario: 新会话展示居中欢迎态与热门专家
  Given 团队成员打开已发布 Agent 应用的对话页且当前无对话消息
  Then 主区展示居中欢迎态：应用头像与名称问候、副标题、开场白引导行（如配置且启用）
  And 输入卡占位为「发消息给 {应用名}」，左下角为工具审批模式开关、右下角为发送/停止按钮
  And 输入卡下方展示使用次数最多的前 10 个专家提示词（本人个人 + 本团队范围，含使用次数，点击即选用）
  When 发送首条消息
  Then 切换为常规对话布局，输入卡停靠底部，不再展示热门专家
  Then 全程无「Enter 发送 · Shift + Enter 换行」提示文案
```

## Feature: 工具审批模式（用户级偏好）

```gherkin
@AP-S51 @auto:unit
Scenario: 审批模式随用户级应用配置持久化并随对话请求生效
  Given 用户在某应用下的个性化配置（app_user_config）含 tool_approval_mode（auto/approval，缺省 auto）
  When 查询用户级应用配置
  Then 返回 tool_approval_mode 与审批卡豁免工具清单（search_knowledge_base 等只读工具）
  When 保存非法模式值
  Then 返回请求错误（400）
  When 用户在输入卡切换模式（或应用设置面板切换并保存）
  Then 立即持久化到用户级应用配置，并在下一轮对话请求头（X-Moai-Tool-Approval）中下发
```

## Feature: 工具调用人工审批（重要工具闸口）

```gherkin
@AP-S52 @auto:unit
Scenario: 审批模式下重要工具调用挂起等待人工决策
  Given 应用绑定沙箱或插件类工具（来源类型 sandbox/dynamic/static/mcp/openapi）且用户处于审批模式
  When 模型发起 call_tool 调用重要工具
  Then 服务端在执行前创建待审批记录（Redis）并挂起当前对话流（最长 5 分钟）
  And 前端对话流内展示审批卡：真实工具名、可展开调用参数、「等待审批」状态与批准/拒绝按钮
  When 用户点击「批准执行」
  Then 决策接口（POST /app/session/{id}/tool-approval）置为 approved，闸口放行执行工具，卡片转为「已批准 · 执行中」并在工具产出后转「已完成」
  When 用户点击「拒绝」
  Then 决策接口置为 rejected，工具不执行并向模型返回拒绝说明，卡片转为「已拒绝」且对话继续
  When 等待超时未决策
  Then 按 timeout 处理，工具不执行并向模型说明审批超时
  When 调用知识库检索（search_knowledge_base）或技能装载（skill_*）等豁免工具
  Then 不挂起直接执行，前端不展示审批按钮
  When 非会话归属用户调用决策接口
  Then 返回不存在（404）
  When 决策无匹配待审批记录（已处理/已超时/工具本不需审批）
  Then 返回 status=missing，前端卡片按已自动执行收敛
  Given 用户处于自动模式（或外部渠道/飞书等无请求头场景）
  When 模型发起任意工具调用
  Then 不经审批直接执行
```

## Feature: 工具调用卡片（对话流状态展示）

```gherkin
@AP-S53 @auto:unit
Scenario: 对话流内工具调用卡片随执行进展流转状态
  Given 对话轮内发生工具调用
  Then 助手消息上方展示工具卡片：工具名（等宽字体）+ 状态标签（执行中/等待审批/已批准 · 执行中/已完成/已拒绝/超时未执行）
  And 支持展开查看格式化调用参数
  When 流式文本继续或回合结束
  Then 执行中/已批准的卡片收敛为「已完成」
```

## Feature: 发布配置快照双轨（草稿与已发布隔离）

```gherkin
@AP-S54 @auto:e2e
Scenario: 发布后管理员改配置只落草稿，线上按发布快照执行
  Given 团队管理员保存 Agent 应用配置（含开场白 OS_A）并发布应用
  Then 发布生成配置快照，配置状态为「草稿与已发布一致」
  When 管理员修改配置（提示词与开场白 OS_B）保存
  Then 配置回读为草稿值且状态转为「有未发布变更」，配置区提示需重新发布
  And 已发布应用的详情仍下发快照开场白 OS_A（线上对话/飞书/外部渠道均按快照执行）
  When 管理员重新发布
  Then 草稿进入发布快照：详情下发开场白 OS_B 且配置状态回「已发布一致」
  And 管理员调试会话按草稿执行（发布与否不影响调试视角）
  Given 未发布应用保存开场白
  Then 详情按实时配置下发（未进入双轨）
```

## Feature: 对话附件（上传 + 文本提取注入）

```gherkin
@AP-S55 @auto:e2e
Scenario: 对话附件直传与文本提取（后端链路）
  Given 登录用户在应用对话输入卡上传附件（文档/图片白名单，≤20MB）
  Then 附件走 pre_upload_chat_file 直传 public/chat 目录（经 /static 免登录下载）
  When 对文档附件调用 POST /api/app/chat-attachment/extract
  Then 返回 Maomi.ToMarkdown 提取的 markdown 文本（超 12 万字符截断并标记）
  And objectKey 非 public/chat 前缀（越权读私有目录）返回 400，不存在附件返回 404，未登录 401
  When 上传白名单外扩展名（如 .exe）或超 20MB 文件
  Then 预上传返回 400
```

```gherkin
@AP-S56 @auto:unit
Scenario: 输入卡附件交互与发送拼接（前端）
  Given 用户在输入卡点击附件按钮选择文件（一次最多 5 个）
  Then 附件以 chip 展示状态流转：上传中 → 提取中（文档）→ 就绪（显示大小/截断标记）；图片不做提取
  And 就绪图片附件的 chip 展示缩略图（上传中/失败无地址回退图片图标），文档附件的 chip 按扩展名展示类型图标（Word/Excel/PPT/PDF/Markdown/代码/文本）；用户气泡附件 chip 同规则（图片缩略图、文档类型图标）
  And 上传或提取失败的附件以失败态展示，可移除后重试
  When 存在未就绪/失败附件时点发送
  Then 发送被阻止并提示「附件处理中」
  When 附件就绪后发送消息
  Then 发送文本为「用户输入 + <moai-attachment name="…">提取内容</moai-attachment> 标记块」，图片附件块携带 objectKey 属性且内容为裸下载地址（chip 可点击打开）
  And 用户气泡把标记块解析回附件 chip：文档可展开查看提取内容，历史回看（刷新加载）同样解析
  And 发送后输入卡附件清空
```

```gherkin
@AP-S64 @auto:unit
Scenario: 对话图片附件多模态注入（后端链路）
  Given 用户消息含图片附件标记块（objectKey 属性或历史 [图片附件](url) 格式，objectKey 必须 public/chat/ 前缀）
  When Agent 对话请求发往模型
  Then 图片标记块被重写为文本占位 [图片附件：文件名] 并在消息尾部追加 image/* DataContent（存储字节内联，svg 与越权/读取失败/超 20MB 项降级保留原标记文本）
  And 会话落库与历史回放仍存标记文本（历史重放走同一转换），文档标记块不受影响
```

## Feature: 快捷输入（应用配置自定义 + 欢迎态点击即发送）

```gherkin
@AP-S57 @auto:e2e
Scenario: 快捷输入配置与下发（后端链路）
  Given 团队管理员在 Agent 应用配置中保存多条快捷输入（最多 10 条、每条最长 200 字）
  When 保存的列表含首尾空白、空串或重复项
  Then 入库前被规范化（去空白、丢弃空串、去重），配置回读与保存内容一致
  And 应用详情随开场白一并下发快捷输入（成员可读，已发布应用按发布快照下发，草稿修改不影响线上）
  When 请求超过 10 条、单条超 200 字、由普通成员提交，或不携带该字段（旧前端保存其他字段）
  Then 越限/越权返回 400/403 且已保存内容不变，不携带字段时保持原值，空数组则清空
```

```gherkin
@AP-S58 @auto:unit
Scenario: 对话欢迎态快捷输入与输入卡布局（前端）
  Given 管理员已在应用配置中定义快捷输入，成员进入应用对话新会话欢迎态
  Then 欢迎态以胶囊展示管理员定义的快捷输入（未配置时不展示该区），不再展示「输入问题开始对话」副标题
  And 输入框默认高度为三行（随内容自动伸缩至十行）
  And 管理端配置分区可增删改快捷输入列表（≤10 条、单条 ≤200 字），随配置保存一并提交且空白项过滤
  When 点击任一快捷输入胶囊
  Then 直接以该内容发送消息（首轮创建会话），无需先填入输入框再点发送
```

```gherkin
@AP-S59 @auto:unit
Scenario: 重新发布入口（发布快照双轨的前端闭环）
  Given 已发布应用的管理员修改配置并保存（配置状态转为「有未发布变更」）
  Then 工作台头部出现「重新发布」主按钮（「取消发布」降为次按钮），配置区警告条附带「重新发布」操作
  When 管理员确认重新发布
  Then 当前草稿配置写入发布快照并立即对线上对话生效，头部入口与警告条消失
  And 未发布应用、无草稿变更或普通成员不展示重新发布入口
```

## Feature: 对话日志用户归属展示

```gherkin
@AP-S60 @auto:unit
Scenario: 日志用户列按用户类型精确归属
  Given 管理员查看应用对话日志
  Then 内部用户（normal）显示用户名（缺失时显示 #id），外部用户/外部应用显示「类型 #id」
  And 用户类型识别不到（none，存量脏数据）时只显示 #id，不误标为外部用户，类型标签如实展示「未知」
```

## Feature: 流程应用绑定为工具（Agent 应用复用团队流程）

```gherkin
@AP-S61 @auto:e2e
Scenario: 绑定校验与配置回读（后端）
  Given 团队管理员在 Agent 应用配置中绑定流程应用作为工具（workflowApps）
  Then 仅允许本团队已发布、未禁用的内部流程应用（Agent 应用/未发布/不存在一律 400，普通成员 403）
  And 保存后配置回读一致；请求不携带该字段（旧前端保存其他字段）时保持原绑定，空数组清空
  And 绑定随发布快照整行写入（D42 契约含 workflowApps）：发布后草稿修改不影响线上，重新发布后生效
```

```gherkin
@AP-S62 @auto:e2e
Scenario: 对话中调用流程应用工具（后端链路）
  Given Agent 应用已绑定可用模型与已发布流程应用并发布，用户发起正式会话对话
  When 模型经 call_tool 调用 workflow__<流程名>（参数 query，兼容 question/input/text/prompt 键与纯 JSON 字符串）
  Then 按该流程应用的发布快照执行一轮流程，结束节点输出作为工具结果回传（success/reply/instanceId，中文不转义）
  And 流程内 sys.conversationId/sys.history 与当前会话一致；审批模式下流程工具属重要工具需人工批准
  When 绑定被解绑或流程应用被取消发布后重新发布应用
  Then 该工具自然下线（工具列表不再装配），模型同名调用得到工具不存在错误
```

```gherkin
@AP-S63 @auto:unit
Scenario: 配置分区流程应用多选（前端）
  Given 管理员进入 Agent 应用「配置」分区
  Then 「流程应用」多选仅列出本团队已发布的内部流程应用（Agent 应用与未发布流程应用被过滤）
  And 已绑定项回显为选中值，随配置保存一并提交 workflowApps；无已发布流程应用时提示空态
```

## Feature: 审批策略（插件白名单与沙箱自动放行）

```gherkin
@AP-S65 @auto:e2e
Scenario: 审批策略配置、校验与发布双轨（后端）
  Given 团队管理员在 Agent 应用配置中维护审批策略（自动放行插件白名单 + 沙箱自动放行开关，随执行参数保存）
  When 白名单包含未绑定插件、插件 id 非法或策略结构不合法
  Then 保存返回请求错误（400）且已保存策略不变
  When 保存合法策略并解绑某白名单插件
  Then 白名单自动收敛为本次绑定插件的子集（前端先剔除，后端强校验兜底）
  And 策略随发布快照整行写入：发布后修改草稿不影响线上放行行为，重新发布后新策略生效
```

```gherkin
@AP-S66 @auto:e2e
Scenario: 审批模式下按策略自动放行（后端链路）
  Given 已发布应用配置审批策略（某插件在白名单、沙箱未开自动放行），用户处于审批模式
  When 模型调用白名单插件的工具
  Then 不创建待审批记录直接执行（决策接口查询返回 missing）
  When 模型调用未开自动放行的沙箱工具（非白名单）
  Then 挂起等待人工决策，拒绝后工具不执行并向模型返回拒绝说明
  Given 用户切换为自动模式
  When 模型发起任意工具调用
  Then 全部直接执行，不经审批
  When 用户级应用配置被查询
  Then 下发策略自动放行工具名清单（静态/动态=插件名，MCP/OpenAPI=插件名__函数名）与沙箱前缀（sandbox_）
```

```gherkin
@AP-S67 @auto:vitest
Scenario: 配置分区审批策略表单（前端）
  Given 管理员进入 Agent 应用「配置」分区
  Then 「审批策略」区含沙箱自动放行开关与自动放行插件多选，多选候选仅为上方已绑定插件
  And 已保存策略回显（开关态与白名单选中值），随配置保存一并提交并收敛为绑定插件子集
  When 未绑定任何插件
  Then 自动放行插件多选提示先绑定插件
```

```gherkin
@AP-S68 @auto:vitest
Scenario: 对话页策略自动放行工具免审批卡（前端）
  Given 应用配置审批策略且用户处于审批模式
  When 模型调用白名单插件工具或沙箱工具（策略开启沙箱自动放行）
  Then 工具卡片直接进入执行流转（执行中/已完成），不出现「等待审批」状态与批准/拒绝按钮
  And 不调用审批决策接口；其余工具仍按 [@AP-S52](#ap-s52) 展示审批卡
```

```gherkin
@AP-S69 @auto:e2e
Scenario: 应用绑定分类（classify_id）
  Given 管理员在分类管理创建 type=app 分类（可配 emoji）
  When 团队 Admin+ 创建应用携带 classifyId
  Then 200，团队列表与详情返回 classifyId
  When 更新应用换绑另一分类
  Then 200，详情反映新分类
  When 创建/更新提交不存在的 app 分类 id
  Then 404「应用分类不存在」
```

```gherkin
@AP-S70 @auto:e2e
Scenario: 首页应用市场搜索与分类过滤
  Given 平台内存在已公开、已发布的内部应用
  When GET /api/app/public/list 不带参数
  Then 返回全部公开应用（含 classifyId）
  When 携带 Keywords（名称/描述包含匹配）
  Then 仅返回命中应用，未命中返回空列表
  When 携带 ClassifyId
  Then 仅返回该分类下的公开应用
```

```gherkin
@AP-S71 @manual
Scenario: 新建/编辑应用分类下拉（前端）
  Given 分类管理存在 type=app 分类
  When 团队 Admin+ 打开新建应用弹窗或应用信息分区
  Then 出现「应用分类」下拉（emoji + 名称），可清空表示未分类
```
```
