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
  Then 可看到顶部工具栏（关键字/发布状态/类型筛选 + 「查询」按钮），「新建应用」按钮在「查询」按钮右侧，每张卡片右上角有「管理」入口

@AP-S114 @auto:unit
Scenario: 内部应用工具栏筛选
  Given 内部应用分区顶部工具栏：关键字输入框 + 发布状态下拉 + 应用类型下拉 + 「查询」按钮 + 「新建应用」按钮（查询按钮右侧）
  When 输入关键字并点击「查询」或回车
  Then 卡片按名称/描述关键字过滤
  When 切换发布状态或应用类型下拉
  Then 卡片即时过滤；筛选无结果时展示「没有符合筛选条件的应用」空态（区别于无应用空态）
  And Member 同样可用筛选，但无「新建应用」按钮

@AP-S12 @auto:unit
Scenario: 普通成员进入团队只能使用
  When Member 打开团队页
  Then 分区菜单只出现 信息 / 内部应用 / 知识库
  And 不出现 外部应用 / 应用接入 / 成员管理 / 插件 / 环境变量 / 设置
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
  And 配置页模型下拉将全零 Guid 归一为未选择（显示占位符），不展示裸串
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
```

```gherkin
@AP-S91 @auto:vitest
Scenario: 配置分区收缩卡片分组（前端）
  Given 管理员进入 Agent 应用「配置」分区
  Then 配置拆为六张可折叠收缩卡片：基础配置（对话模型/上下文压缩/子代理协作）、提示词（系统提示词/对话开场白/快捷输入）、能力扩展（插件/流程应用/Agent 应用/技能/知识库/知识图谱，选中资源后下方逐项勾选「自动放通」）、沙箱（执行参数与审批模式沙箱自动放行）、内容脱敏、长期记忆
  And 「对话模型」与「系统提示词」为必填项，标题旁带红色 *（与「信息」分区应用名称一致）；未选模型或提示词为空时「保存设置」被拦截提示
  And 「摘要模型」默认自动绑定上方对话模型显示，可手动更换，清空后恢复跟随对话模型（存储空串=跟随）
  And 自动放通列表表头带「全部放通」总开关：勾选一键全选该类全部绑定资源、取消一键全清；部分勾选时呈半选态
  And 前四张卡片默认展开、点卡片头可收起，内容脱敏与长期记忆默认收起；保存配置一次提交全部卡片内容
  When 应用为外部应用
  Then 不渲染「沙箱」卡片（含沙箱自动放行开关）与「长期记忆」卡片、不展示技能选项，顶部提示外部应用限制
```

@AP-S19 @auto:e2e
Scenario: 配置校验与类型约束
  When Agent 应用保存配置未选对话模型（null / 空 Guid）
  Then 返回参数错误（不存在「默认模型」回退语义）
  When Agent 应用保存配置系统提示词为空或空白
  Then 返回参数错误
  When 提示词超过 4000 字
  Then 返回参数错误
  When 对流程应用保存配置（该类型仅对话人设适用：系统提示词（可选，经 sys.prompt 供节点引用）/开场白/快捷输入落库，模型/知识库/插件/技能不校验不落库，也不做必填校验）
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

## Feature: 上下文压缩配置与自动压缩管线

```gherkin
@AP-S115 @auto:e2e
Scenario: 压缩默认值端点回显自动模式与比例常量
  When 登录用户查询压缩默认值
  Then 返回 200，含 compactionMode=auto 与工具折叠 0.5 / 摘要触发 0.8 / 压回目标 0.5 / 紧急截断 0.95 四个比例
  And 自定义模式兜底默认值（工具折叠 16 条、摘要 16K、截断 64K）与摘要开关关闭一并回显
  But 响应不再携带 preserveTurns（滑动窗口策略已退役）
  When 未登录查询
  Then 返回未认证

@AP-S116 @auto:e2e
Scenario: 压缩模式随应用配置保存与回读
  When Admin 保存 executionSettings.compactionMode=auto
  Then 返回成功，重新查询回读 auto
  When 保存 compactionMode=custom 且携带工具折叠/摘要触发/紧急截断绝对值
  Then 返回成功，重新查询回读 custom 与全部绝对值一致

@AP-S117 @auto:e2e
Scenario: 压缩参数保存校验
  When 保存 compactionMode 为枚举外取值
  Then 返回参数错误，已存压缩配置不被改写
  When custom 模式摘要触发低于下限（<200）
  Then 返回参数错误
  When custom 模式紧急截断上限不大于摘要触发
  Then 返回参数错误

@AP-S118 @auto:e2e
Scenario: 自动模式忽略历史绝对值参数
  Given 存量应用 execution_settings 携带旧版固定数值且无模式标记
  When 按 auto 模式保存
  Then 返回成功（绝对值字段不生效也不校验，运行时由所选模型窗口推导阈值）

@AP-S119 @auto:unit
Scenario: 自动压缩管线按模型窗口比例推导阈值
  Given 应用绑定对话模型（上下文窗口 1M / 最大输出 100K）
  When 对话运行时构建压缩管线
  Then 工具结果折叠在约 50% 预算（450K token）触发
  And 开启摘要压缩时在约 80% 预算触发并压回约 50%（大步长低频，保持前缀缓存稳定）
  And 紧急截断在约 95% 预算触发兜底
  When 模型窗口元数据缺失
  Then 回落固定值管线（工具折叠 16 条 / 摘要 16K / 截断 64K，与历史行为一致）

@AP-S120 @auto:unit
Scenario: 滑动窗口策略退役
  Given 历史 execution_settings 携带 preserveTurns
  When 构建压缩管线
  Then 不再产生按轮数滑窗淘汰（字段读取容忍、不生效）
  And 轮数远超旧阈值但 token 低于阈值时不发生压缩
```

## Feature: 模型参数展示（配置页对话模型）

```gherkin
@AP-S121 @auto:manual
Scenario: 对话模型下拉旁展示所选模型参数
  When 用户在配置页选中一个对话模型
  Then 下拉框右侧以标签展示模型参数：上下文窗口与最大输出（K/M 缩写）、视觉、工具调用、推理（元数据缺失的项不展示）
  And 团队可用模型接口透出 contextWindow / maxOutput / supportsToolCall / supportsReasoning
  When 自动压缩模式生效且所选模型有窗口元数据
  Then 压缩策略说明按当前模型换算显示具体 token 阈值；无元数据时展示固定值兜底说明
```

## Feature: 模型下拉聚合与来源分组（配置页 / 流程节点）

```gherkin
@AP-S122 @auto:e2e @auto:unit
Scenario: 对话类下拉只列 conversation 模型
  Given 团队可用模型聚合列表（团队自有渠道 ∪ 公开 ∪ 已授权）中含 embedding 等非对话模型
  When Agent 应用配置页打开对话模型或 LLM 摘要压缩模型下拉
  Then 列表仅出现 modelKind=conversation 的模型（流程节点 aiChat/图片理解模型选择器同规则，图片理解额外要求支持视觉）
  And 团队可用模型接口每项透出 modelKind（见 @AP-S121 能力字段同源）

@AP-S123 @auto:unit
Scenario: 模型下拉按渠道来源分组并支持按渠道名搜索
  When 打开对话模型 / 摘要压缩模型下拉
  Then 选项按来源分组：「团队渠道模型」（isTeamChannel=true）在前、「系统渠道模型」在后，空组不展示
  And 选项 title 附所属渠道名，输入渠道名关键字可过滤命中
  And 摘要压缩模型未手动指定时仍跟随上方对话模型（空串=跟随语义不变）
```

## Feature: 插件下拉来源分组（配置页）

```gherkin
@AP-S130 @auto:unit
Scenario: 插件下拉按来源分组展示团队与系统插件
  Given 团队插件列表已聚合「团队自有 ∪ 系统公开 ∪ 已授权系统插件」（后端口径见 @TP 用例 TP-35c/i/l，此处不重复）
  When 打开配置页「允许使用的插件」下拉
  Then 选项按来源分组：「团队插件」（isTeamOwned=true）在前、「系统插件」在后，空组不展示
  And 内存静态插件（空 Guid，无 DB 记录）仍被过滤不进下拉
  And 选项保持「头像 + 标题」渲染，按名称关键字可搜索（title 过滤）
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
  Then 区块顶部展示提示：第三方系统可使用应用接入 key 全量访问团队资源（无范围勾选，原「按功能范围授权」提示随 scope 退役移除）
```

## Feature: 外部应用接入 token（/api/teamres）

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

## Feature: 外部会话与对话（/api/teamres/agent）

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

## Feature: key 直连外部接入（免换 token）

```gherkin
@EA-S15 @auto:e2e
Scenario: 应用接入 key 直访团队资源
  When 以 Bearer 或 x-api-key 直接携带应用接入 key 调用 /api/teamres 团队资源接口
  Then 全量放行（无范围概念，原「按勾选的知识库范围放行」随 scope 退役改写），无需先换取外部 token
  But 伪造 key 返回未认证

@EA-S16 @auto:e2e
Scenario: 需授权应用的会话面仅限外部用户 token
  When 应用接入 key 直连或应用 token 访问需授权外部应用的建会话/会话列表端点
  Then 返回禁止并提示先经 /api/teamres/token 换取外部用户 token
  But 外部用户 token 不受影响

@EA-S17 @auto:e2e
Scenario: 免授权应用 key 直连会话
  When 应用接入 key 直连对免授权外部应用创建会话、查询会话列表、读取消息并发起对话
  Then 系统以接入「直连会话身份」承载会话归属，各端点放行且归属校验通过

@EA-S19 @auto:e2e
Scenario: 已下线团队接入 key 前缀拒绝
  When 以 moai- 前缀（非 moai-ac-）凭证访问 /api/teamres 或网关
  Then 返回未认证（团队接入 key 已下线，接入统一走应用接入 key）
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

```gherkin
@AP-S147 @auto:vitest
Scenario: 配置页调试对话面板复刻真实对话界面
  Given 团队管理员打开应用配置页的调试对话面板
  Then 面板与真实对话页（@AP-S50）同布局：无消息时展示欢迎态（应用徽标与名称、开场白 Markdown 简报卡、「试试这样问」快捷输入两列卡片点击即发送、圆角输入卡内嵌模型切换器与发送按钮）
  And 输入框占位为「发消息给 {应用名}」，模型切换器初始选中应用默认模型（同 @AP-S146 口径）
  And 开场白仅展示在欢迎态简报卡，不混入消息流；发送首条消息后切换为消息流 + 底部停靠输入卡
  And 开场白与快捷输入为配置保存前的草稿实时预览（改配置即见效果，不必先保存）
```

```gherkin
@AP-S148 @auto:vitest
Scenario: 调试面板输入卡一比一复用真实对话页输入卡
  Given 团队管理员打开应用配置页的调试对话面板
  Then 输入卡为与真实对话页（@AP-S50）同一共享组件：附件按钮与附件 chips、审批模式切换（自动/审批）、可选模型切换器、发送/停止按钮同构
  And 附件可选且随消息一起发送（上传/提取中发送置灰，气泡解析回附件 chip），发送后输入卡与附件清空
  And 审批模式初始取用户级应用配置，面板内切换仅本地生效（随下轮对话请求头下发），不写用户级应用配置
  And 调试会话不落库不计用量，上下文占用环形指示与专家选择不出现（@AP-S147 已知边界维持）
```

```gherkin
@AP-S149 @auto:vitest
Scenario: 调试面板审批卡批准与拒绝
  Given 团队管理员在调试面板开启审批模式后发送消息
  When 模型调用非豁免的重要工具（沙箱/插件等）
  Then 消息流挂起审批卡（真实工具名与参数、等待审批、批准/拒绝按钮）
  When 点击批准或拒绝
  Then 调试会话走同一审批决策端点，后端按调试会话注册表校验归属后落决策（正式会话仍按会话表校验，非归属返回不存在）
  And 卡片状态流转为已批准·执行中或已拒绝；无匹配待审批记录时按已自动执行收敛
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
  And 返回活动统计（累计使用次数 / 会话数 / 去重用户数（按用户+类型对）/ 消息数 / 最近使用时间）
  When 普通成员或非成员查询用量
  Then 分别返回禁止与不存在
  When 查询不存在应用的用量
  Then 返回不存在
```

```gherkin
@AP-S141 @auto:e2e
Scenario: 消息总数经统计模块增量维护
  Given 应用会话每轮落库成功后（AppChatFlushService）按物理消息行数增量推送统计模块（app-message-usage，压缩折叠历史为负增量）
  Then 消息总数随统计模块每分钟批量落库累加到 app.message_count（分钟级滞后）
  And 删除会话不回减（累计口径，与使用次数一致）；统计推送失败仅告警，不阻塞会话落库
```

```gherkin
@AP-S142 @auto:e2e
Scenario: 用量聚合缓存与实时活动统计口径分离
  When 团队管理员查询用量
  Then tokens/调用次数/按模型分布经 Redis 缓存返回（1 分钟 TTL cache-aside，聚合用量表本身滞后约 1 分钟）
  And 缓存故障时降级直查数据库，不影响正确性
  And 会话数 / 去重用户数 / 最近使用时间为实时小聚合（删除会话立即生效，不进缓存）；使用次数与消息总数读应用自身表
```

```gherkin
@AP-S143 @auto:e2e
Scenario: 查询应用使用趋势
  When 团队管理员查询近 N 天使用趋势（days 缺省 30，范围 1~90）
  Then 返回逐日使用次数与消息条数序列（统计模块统一日表按日聚合，hour 粒度桶并入当天，缺数据日期补 0，升序含今日）
  When days 非法（0 或超上限 90）
  Then 返回参数错误
  When 未登录 / 普通成员 / 非成员 / 不存在应用查询趋势
  Then 分别返回未登录 / 禁止 / 不存在 / 不存在
```

```gherkin
@AP-S144 @auto:e2e
Scenario: 按模型分布并入生效配置的可选模型列表
  Given 应用生效配置含默认模型与可选模型列表（已发布取快照，否则草稿，与对话端同口径）
  When 团队管理员查询用量
  Then byModel 为「已用模型 ∪ 配置模型」并集：默认模型带 isDefault 标记排首位，其它配置模型按配置顺序跟随
  And 配置但未调用的模型以零用量项补全（isConfigured=true）；历史使用但已移出配置的模型保留（isConfigured=false，按 token 降序殿后）
  And 合并在缓存外做（配置变更即时反映，token 数值仍走 1 分钟聚合缓存）
```

```gherkin
@AP-S145 @auto:e2e
Scenario: 趋势返回各模型 Token 消耗序列
  When 团队管理员查询趋势
  Then 同时返回各模型逐日 token 序列（统计日表 ai-model-usage 行按「resource_id 以 :{appId}:total 结尾」后缀聚合，SQL 端按日期+模型归并）
  And 序列按区间合计 token 降序最多 8 条，每条覆盖完整日期区间（无数据日补 0）；模型名经模型表解析（已删模型回落 id）
  And 趋势响应走 1 分钟 Redis 缓存（key 含区间天数，缓存故障降级直查）；无模型用量数据时序列为空列表
```

## Feature: 会话标题自动提炼

```gherkin
@AP-S76 @auto:e2e
Scenario: 首轮对话后由模型从提问提炼会话标题
  Given 用户为已绑定对话模型的 Agent 应用创建会话（不传标题，落默认「未命名标题」）
  When 首轮对话结束落库时（AppChatFlushService）
  Then 会话标题由应用绑定的对话模型从用户首条提问提炼（≤20 字，剔除附件标记块后提炼，非提问原文截断）
  And 标题生成限时 12 秒；应用未绑模型/模型不可用/生成失败或输出为空时回退为提问原文截断，落库不因此中断
  And 用户手动改过标题（UpdateTitle）或创建时传入标题的会话不再自动生成
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

@EA-S20 @auto:unit @auto:e2e
Scenario: 悬浮组件 token 拦截器与请求中间件（需授权应用自托管接入）
  When 需授权应用（is_auth=true）由业务系统自托管组件 JS 并配置 tokenProvider
  Then 组件经 tokenProvider 获取业务后端签发的外部用户 token（业务后端持应用接入 key 调 /api/teamres/token），组件不再自己换 token、不需要也不发送应用接入 key
  When 组件发出换 token、建会话或对话请求
  Then 请求先经 onRequest 中间件改写（url/headers/body 可替换，可整体代理到业务后端）
  When 缓存的 access token 临近过期
  Then 优先重新调用 tokenProvider 获取新 token，否则以 refresh_token 调 /api/teamres/token/refresh 换新后继续使用，生效期内不重复刷新
  When 需授权应用未提供 key 与 tokenProvider 且无中间件接管换 token
  Then 换 token 失败，组件提示需要配置 key 或接入 tokenProvider
  And 组件暴露全局 MoaiWidget.mount（window.MoaiWidget = { mount, MoaiWidget }）供下载后的 JS 在业务站点编程挂载；无 data-app-id 的 script 引入静默跳过自动引导
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

## Feature: 沙箱出站网络策略与管控（应用配置强校验）

```gherkin
@AP-S125 @auto:e2e @auto:unit
Scenario: 出站网络策略保存校验与回读
  Given 应用已启用沙箱
  When 保存出站策略：默认动作 + 规则列表（每条 = 动作 allow/deny + 目标域名/*.通配符/IP/CIDR + 可选端口）
  Then 合法策略保存成功且回读一致（默认动作、规则动作/目标/端口逐项一致）
  When 规则目标格式非法（如 "not a domain!"）或默认动作取值非法
  Then 返回请求错误（400）提示目标格式或取值无效
  When 域名目标携带端口、端口数值越界或重复、规则超过 100 条
  Then 返回请求错误（400）提示端口约束或规则数量上限
  When 保存旧格式 egress（字符串数组）且目标合法
  Then 保存成功（旧格式兼容，读取时按默认 deny 为放行名单、否则为拒绝名单转换）
  And 配置页以规则行列表回显旧格式（含动作归一）

@AP-S126 @auto:e2e
Scenario: 平台管控模式拦截默认允许
  Given 超级管理员将出站网络管控模式设为 deny 或 intranet
  When 团队 Admin 保存默认动作 allow 的出站策略
  Then 返回请求错误（400）提示平台已强制默认拒绝、可改为追加放行规则
  When 保存默认动作 deny（含放行规则）的出站策略
  Then 保存成功

@AP-S127 @auto:vitest
Scenario: 配置页出站规则编辑与管控提示
  Given 团队 Admin 打开应用配置页沙箱分区
  Then 出站网络策略以「默认动作 + 规则行列表（动作/目标/可选端口）」呈现，旧格式配置按旧语义转换为规则行回显
  When 新增规则并保存
  Then 以 rules 新格式提交（action/target/ports 与编辑一致）
  When 管控模式为 deny/intranet
  Then 默认动作的「允许」选项禁用并展示平台强制拒绝提示
  And 目标格式 / 端口约束 / 规则数超限在前端即时拦截（后端 400 为最终防线）
```

## Feature: 沙箱命令过滤拦截

```gherkin
@AP-S128 @auto:e2e @auto:unit
Scenario: 命令过滤配置校验与回读
  Given 应用已启用沙箱
  When 保存应用级命令过滤（enabled + deny 模式清单）
  Then 合法清单保存成功且回读一致（enabled 与 deny 逐条一致）
  When deny 含不可编译的 regex: 模式或超过 100 条
  Then 返回请求错误（400）提示模式无效或数量上限（未启用时同样拦截）
  When 平台级禁用命令清单（SANDBOX_DENIED_COMMANDS）含非法模式或超量
  Then root 保存返回请求错误（400）

@AP-S129 @auto:vitest
Scenario: 配置页与系统设置页命令过滤编辑
  Given 团队 Admin 打开应用配置页沙箱分区
  Then 沙箱卡片提供「命令过滤」开关与「禁用命令清单」文本域（每行一条，回显 executionSettings.sandbox.commandFilter）
  When 开关启用后编辑清单并保存
  Then 以 commandFilter 新格式提交（enabled 与 deny 归一后一致）
  And root 在系统设置沙箱卡片编辑平台级禁用命令清单并随卡片保存提交（非法模式前端拦截不发请求）
```

## Feature: 应用信息分区（Agent 应用工作台）

```gherkin
@AP-S49 @auto:unit
Scenario: 工作台「信息」分区维护应用基础信息
  Given 团队成员打开 Agent 应用工作台
  Then 左侧菜单在「配置」之后展示「信息」项（流程应用工作台无此项）
  When 进入「信息」分区
  Then 可查看并维护应用头像、类型、名称、描述与分类（版本号不在此设置，只能在发布/申请上架时设置，见 [@AP-S93](./bdd.md#ap-s93)）
  And 内部应用的上架申请与审批状态不在本分区展示，统一在「上架管理」分区维护（见 [@AP-S92](./bdd.md#ap-s92)）
  And 外部应用展示访问授权开关
  When 普通成员进入「信息」分区
  Then 表单只读且无保存入口
```

## Feature: 应用上架管理分区（工作台）

```gherkin
@AP-S92 @auto:unit
Scenario: 工作台「上架管理」分区统一承载团队发布与公开上架
  Given 团队成员打开内部应用的工作台
  Then 左侧菜单在「信息」之后展示「上架管理」项（外部应用展示「发布」项且分区仅保留团队发布卡，见 [@AP-S108](./bdd.md#ap-s108)；流程应用在「配置」二级菜单，成员可见）
  When 进入「上架管理」分区
  Then 卡片按序为「团队应用」→「上架管理（应用市场）」→「申请记录」
  And 「团队应用」展示团队发布状态与版本（线上发布快照/发布时间/当前版本一致性）：「发布」常驻（弹发布弹窗设置版本号，见 @AP-S97）、已发布并列红色「停用」（二次确认：成员将无法进入对话，已上架的应用同步下架，见 @AP-S96；按钮语义见 @AP-S113）
  And 「上架管理（应用市场）」展示公开状态（未申请/审核中/已公开/已驳回）与提示文案：未申请/被驳回提供「申请上架/重新申请」（应用未发布时按钮禁用并提示先发布，上架前置见 [@PB-S14](../publication/bdd.md#pb-s14)；上架把最新的团队发布上架，无需填写版本号，自动记录应用当前发布版本；弹窗含「可见范围」配置，见 @AP-S131）、审核中可撤回申请、已公开时显示发布版本号（最近一次审批通过的上架版本）与可见范围标签并提供红色「下架」入口（二次确认：平台内用户不再可用，团队内使用不受影响）
  And 被驳回时以醒目区展示管理员填写的拒绝原因，管理员可重新申请
  And 申请记录列表展示历次申请（状态/版本/可见范围/申请理由/审批意见/申请人/申请时间/审批人与审批时间；可见范围列标签见 @AP-S131）
  When 管理员对待审核申请执行撤回（概览或记录行均可）
  Then 该记录被删除且概览回到未申请状态
  And 上述任一操作完成后即时刷新工作台详情与申请记录数据（不整页刷新）：撤回发布联动撤下待审申请后申请记录同步更新，操作失败也刷新以自愈陈旧状态（如点击已被联动撤下的申请撤回按钮，报错后列表回到真实状态）
  When 普通成员进入「上架管理」分区
  Then 可查看状态与申请记录，但无发布/停用/申请/撤回/下架入口
```

## Feature: 应用版本号

```gherkin
@AP-S93 @auto:unit
Scenario: 应用版本号只在发布/上架时设置，int 编码存储与比较
  Given 应用版本号以 aaa.bb.cc 展示、按 int 编码 aaa*10000+bb*100+cc 落库（新建应用默认 1.0.0=10000）
  When 管理员在「信息」分区维护基础信息
  Then 表单不提供版本号输入，请求携带 version 字段也被忽略（版本保持原值）
  When 版本号格式非法（段数不符或各段超出 3/2/2 位数字）
  Then 发布请求被拒绝且不落库
  When 团队成员查看「上架管理」分区的「版本与草稿」
  Then 分别展示线上版本号（发布快照）与当前版本号，已发布且不一致时提示「版本未发布」
```

```gherkin
@AP-S97 @auto:e2e
Scenario: 发布时设置版本号（「上架管理」分区团队应用卡的发布弹窗）
  Given 管理员在应用工作台「上架管理」分区的「团队应用」卡点击「发布」弹出发布弹窗（见 @AP-S92）
  When 弹窗中版本号必填并自动填入严格递增的下一版本号（可改为更大的版本，相同或更低就地拦截，见 @AP-S98），确认发布
  Then 应用当前版本更新为该值，线上版本号快照同步为该版本（停用清空快照、当前版本保留并联动下架，见 @AP-S96）
  When 发布请求不携带版本号（配置区警告条一键发布、外部应用列表快捷发布等）
  Then 版本保持原值，线上版本号快照取当前版本
  When 管理员申请上架（见 @AP-S92）
  Then 上架把最新的团队发布上架，版本号自动记录应用当前发布版本（请求可不携带；携带时校验格式且不能低于当前版本，见 @AP-S98），审批通过后作为市场版本记录在申请历史
```

```gherkin
@AP-S98 @auto:e2e
Scenario: 发布版本号必须严格递增（相同或更小一律拒绝）
  Given 应用存在当前版本号（int 编码 aaa*10000+bb*100+cc，直接比较大小）
  When 发布携带的新版本号等于或低于当前版本号（如当前 1.0.0 时填 1.0.0 或 0.0.1）
  Then 请求被拒绝（前端弹窗就地提示，后端同样 400 拒绝），版本号保持不变且不产生发布快照
  When 管理员打开发布弹窗
  Then 版本号自动填入严格递增的下一版本号（patch 段 +1、段满进位，如 0.0.1→0.0.2、0.0.99→0.1.0），可手动改为更大的版本
```

## Feature: 外部应用发布（工作台）

```gherkin
@AP-S108 @auto:unit
Scenario: 外部应用「发布」分区——直接发布即生效，无上架审批
  Given 团队管理员打开外部 Agent 应用的工作台
  Then 左侧菜单在「信息」之后展示「发布」项（非「上架管理」；外部流程应用仍走编排设计器发布，无此项）
  When 进入「发布」分区
  Then 仅展示团队发布卡：发布状态与版本展示同 [@AP-S92](./bdd.md#ap-s92) 的「团队应用」卡——「发布」常驻（发布弹窗设置版本号，规则同 @AP-S97/@AP-S98）、已发布并列红色「停用」（确认文案提示停用后外部用户将无法访问该应用，见 @AP-S113）
  And 外部应用不公开到平台：无「上架管理（应用市场）」卡与「申请记录」卡，也不请求团队申请列表
  And 卡内提示直接发布即生效无需审批：发布走同一 `POST /app/{id}/publish`（后端本就支持外部 Agent 应用），外部用户经访问点使用已发布版本（未发布/撤回后外部访问被拒）
```

## Feature: 发布/停用按钮语义与公开应用可视范围

```gherkin
@AP-S113 @auto:unit
Scenario: 团队发布按钮收敛为「发布」常驻 + 已发布并列「停用」
  Given 团队管理员打开应用的发布入口（上架管理分区团队应用卡/外部应用发布卡/配置区草稿变更警告条）
  Then 「发布」按钮常驻（未发布即首次发布、已发布把当前草稿推入快照按弹窗版本号更新，语义幂等不再区分「重新发布」文案）
  And 已发布时并列红色「停用」按钮（即原撤回发布，二次确认，见 @AP-S96）
  And 配置区「草稿有未发布变更」警告条的快捷入口文案同步为「发布」
```

```gherkin
@AP-S124 @auto:e2e
Scenario: 公开应用使用门禁按可视范围拦截（团队内不受限）
  Given 应用 A 已发布且公开（is_public=true），可视范围为按组织限定（见 [@PB-S20](../publication/bdd.md#pb-s20)），用户 U1 在范围组织内、U2 不在且非团队成员
  When U2 以直链创建会话/查看应用详情/读写用户配置/查询会话列表
  Then 均被拒绝（与未公开应用同路径：团队不存在或你不是团队成员）
  When U1 执行同样操作
  Then 放行（范围内用户可正常使用）
  And 团队成员始终放行（不受市场可视范围约束，仅约束平台侧公开可用性）

```gherkin
@AP-S131 @auto:e2e
Scenario: 应用广场按可视范围过滤与范围标签回显
  Given 应用广场列表接口按当前用户过滤：无组织授权记录（全员范围）的应用所有人可见，有记录的仅被范围组织覆盖的用户可见
  When U1（范围内）查询广场列表
  Then 应用 A 出现在结果中；U2（范围外）查询则不出现
  And 申请记录与已公开卡回显可视范围标签（全员可见/组织可见，组织可见以悬浮提示展开组织名与方向 self/up/down）
```

```gherkin
@AP-S132 @auto:e2e
Scenario: 收藏与取消收藏（含幂等）
  Given 已登录用户浏览应用广场（公开列表内应用均可收藏）
  When 用户收藏某个广场可见的应用，随后重复收藏同一应用
  Then 两次均返回成功，收藏记录 (app_id, user_id) 活跃行唯一不重复
  And 广场列表该应用回显 isFavorite=true
  When 用户取消收藏后再重复取消
  Then 两次均返回成功；应用下架/停用后仍可取消遗留收藏（清理不阻塞）
  And 取消后列表回显 isFavorite=false
```

```gherkin
@AP-S133 @auto:e2e
Scenario: 收藏门禁与广场可见性同口径
  When 用户收藏一个不在广场可见范围的应用（未公开/已停用，或组织范围限定且本人不在范围内）
  Then 返回 404（与广场不可见同路径，不泄露应用存在性）
  And 范围内的用户收藏同一应用成功
```

```gherkin
@AP-S134 @auto:e2e
Scenario: 广场收藏置顶与热度排序
  Given 广场列表基线按热度（累计使用次数）降序、同次数按发布时间倒序
  When 用户收藏一个排序靠后的应用
  Then 该应用升至本人列表首位（收藏置顶，组内仍按热度排；仅影响本人视图，他人列表不变）
  And 取消收藏后恢复原热度排序
```

## Feature: 应用对话界面（欢迎态与输入卡）

```gherkin
@AP-S50 @auto:unit
Scenario: 新会话展示居中欢迎态与热门专家
  Given 团队成员打开已发布 Agent 应用的对话页且当前无对话消息
  Then 主区展示居中欢迎态：应用头像与名称问候（主色环境光背景）、副标题（未配置开场白时）、开场白简报卡（如配置且启用，按 Markdown 渲染：列表/加粗等富文本，与输入区同宽对齐）
  And 输入卡占位为「发消息给 {应用名}」，左下角为工具审批模式开关、右下角为发送/停止按钮
  And 输入卡下方展示使用次数最多的前 10 个专家提示词（本人个人 + 本团队范围，单行横向滑动、两端渐隐，含使用次数，点击即选用）
  And 快捷输入以「试试这样问」两列建议卡片展示（图标+文案+悬浮箭头，点击即发送）
  When 发送首条消息
  Then 切换为常规对话布局，输入卡停靠底部，不再展示热门专家与快捷输入
  And 对话页整页沉浸：无外框卡片与四周留白（AppLayout 对 /chat 分区去全局 24px 内边距），侧栏与主区以竖线分隔
  And 消息以分组展示（无卡片容器）：用户消息为右对齐纯文本（无气泡）；一次回答的多个回合（提问后 AI 连续产出的多条助手消息）合并为一组——共用一个头像、组内无分隔线
  And 分隔线只出现在新提问上方（隔开上一轮回答），提问与其回答之间不加线
  And 地址栏携带当前会话（/chat/:sessionId）：点击会话或首轮发送创建会话时更新地址，刷新/深链/前进后退恢复该会话；新对话与删除当前会话回到无会话地址
  Then 全程无「Enter 发送 · Shift + Enter 换行」提示文案
```

```gherkin
@AP-S86 @auto:vitest
Scenario: 会话列表删除会话
  Given 团队成员在应用对话页的会话列表中已有多个会话
  Then 悬停或聚焦某个会话项时显示红色删除入口，点击弹出删除确认框
  When 确认删除某个会话
  Then 该会话连同消息一并移除（服务端软删除；仅会话归属用户可操作，他人按不存在处理）
  And 若删除的是当前会话，则清空消息回到新对话欢迎态，地址栏回到无会话地址
  And 点击确认框的确认或取消仅作用于删除确认，不触发选中该会话
```

```gherkin
@AP-S87 @auto:vitest
Scenario: 对话页头部返回导航
  Given 用户进入应用对话页（团队成员经团队应用列表进入，或任意用户经应用市场打开公开应用）
  When 点击头部返回按钮
  Then 返回应用市场（/apps），不落入所属团队的应用列表——公开应用的非团队成员不再触发团队应用列表无权限提示
```

```gherkin
@AP-S88 @auto:vitest
Scenario: 多会话并行对话
  Given 用户在某会话 A 发送问题且回复仍在流式生成中
  When 点击「新对话」
  Then 立即进入空白欢迎态（输入卡显示发送按钮而非停止按钮），可马上发送新问题创建会话 B——A 的流在后台继续写入自身会话不受影响
  And 进行中的会话在会话列表以呼吸点标识「回复生成中」，流结束后标识消失
  When 切回流式中的会话 A
  Then 恢复 A 的实时现场（已到达的部分回答与提问可见，不回拉服务端历史），流继续推进内容持续更新
  And 发送/停止仅作用于当前查看的会话：点停止只中止当前会话的流，后台并行会话不受影响
  And 首条消息创建会话后立即刷新会话列表（不等流结束），被删除会话的进行中流一并中止
```

## Feature: 工具审批模式（用户级偏好）

```gherkin
@AP-S51 @auto:unit
Scenario: 审批模式随用户级应用配置持久化并随对话请求生效
  Given 用户在某应用下的个性化配置（app_user_config）含 tool_approval_mode（auto/approval，缺省 auto）
  When 查询用户级应用配置
  Then 返回 tool_approval_mode 与审批卡豁免/自动放行工具清单（检索与技能装载已纳入审批范围，豁免清单当前为空；自动放行清单按发布快照审批策略解析为工具名与前缀）
  When 保存非法模式值
  Then 返回请求错误（400）
  When 用户在输入卡切换模式（或应用设置面板切换并保存）
  Then 立即持久化到用户级应用配置，并在下一轮对话请求头（X-Moai-Tool-Approval）中下发
```

## Feature: 工具调用人工审批（重要工具闸口）

```gherkin
@AP-S52 @auto:unit
Scenario: 审批模式下纳入审批范围的工具调用挂起等待人工决策
  Given 应用绑定工具（来源类型 sandbox/dynamic/static/mcp/openapi/workflow/agentapp/skill/wiki/graph，内部工具 memory/subagent 除外）且用户处于审批模式
  When 模型发起 call_tool 调用纳入审批范围的工具
  Then 服务端在执行前创建待审批记录（Redis）并挂起当前对话流（最长 5 分钟）
  And 前端对话流内以扁平行展示审批信息：真实工具名、可展开调用参数、「等待审批」状态与批准/拒绝按钮
  When 用户点击「批准执行」
  Then 决策接口（POST /app/session/{id}/tool-approval）置为 approved，闸口放行执行工具，工具行转为「已批准 · 执行中」并在工具产出后转「已完成」
  When 用户点击「拒绝」
  Then 决策接口置为 rejected，工具不执行并向模型返回拒绝说明，工具行转为「已拒绝」且对话继续
  When 等待超时未决策
  Then 按 timeout 处理，工具不执行并向模型说明审批超时
  When 调用审批策略自动放通的资源工具（见 @AP-S66）
  Then 不挂起直接执行，前端不展示审批按钮
  When 非会话归属用户调用决策接口
  Then 返回不存在（404）
  When 决策无匹配待审批记录（已处理/已超时/工具本不需审批）
  Then 返回 status=missing，前端工具行按已自动执行收敛
  Given 用户处于自动模式（或外部渠道/飞书等无请求头场景）
  When 模型发起任意工具调用
  Then 不经审批直接执行
```

## Feature: 工具调用行（对话流状态展示）

```gherkin
@AP-S53 @auto:unit
Scenario: 对话流内工具调用行随执行进展流转状态
  Given 对话轮内发生工具调用
  Then 已完成的工具调用在回合头部以折叠组展示：摘要行「已执行 N 次工具」（单条直接展示明细行），默认收缩、点击展开/收起
  And 明细行为无容器扁平行：状态图标 + 工具名（等宽字体）+ 状态标签（执行中/等待审批/已批准 · 执行中/已完成/已拒绝/超时未执行），「调用参数」紧跟状态而非推至行尾
  And 支持展开查看格式化调用参数
  When 执行中/待审批/已批准的工具调用进行时
  Then 该工具行固定展示在正文内容底部（始终可见），待审批行附批准/拒绝按钮
  When 流式文本继续或回合结束
  Then 执行中/已批准的工具行收敛为「已完成」并归入头部折叠组
  And 刷新页面后历史消息同样回放各回合的工具调用行（后端持久化 toolCalls，前端按折叠组还原）
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

## Feature: 停用（团队发布下线，联动取消上架）

```gherkin
@AP-S96 @auto:e2e
Scenario: 停用清除发布快照并自动下架
  Given Admin 已发布应用并通过上架审批（is_public=true，见 [@PB-S11](../publication/bdd.md#pb-s11)）
  When Member 停用应用
  Then 返回禁止
  When Admin 停用该应用
  Then 发布快照清除（publishStatus 回未发布、publishTime/publishVersion 清空）
  And 应用 is_public 变为 false，平台公开应用列表不再包含该应用（联动下架见 [@PB-S18](../publication/bdd.md#pb-s18)）
  And 审核中的上架申请被自动撤下，团队列表不再显示待审核记录（历史已审批记录保留）
  When Admin 再次发布并重新申请上架
  Then 申请成功（联动撤下不占用待审核唯一约束）
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
  And 就绪图片附件的 chip 展示缩略图（上传中/失败无地址回退图片图标），文档附件的 chip 按扩展名展示类型图标（Word/Excel/PPT/PDF/Markdown/代码/文本）；用户消息附件 chip 同规则且为浅色描边样式（图片缩略图、文档类型图标）
  And 上传或提取失败的附件以失败态展示，可移除后重试
  When 存在未就绪/失败附件时点发送
  Then 发送被阻止并提示「附件处理中」
  When 附件就绪后发送消息
  Then 发送文本为「用户输入 + <moai-attachment name="…">提取内容</moai-attachment> 标记块」，图片附件块携带 objectKey 属性且内容为裸下载地址（chip 可点击打开）
  And 用户消息行把标记块解析回附件 chip：文档可展开查看提取内容，历史回看（刷新加载）同样解析
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
Scenario: 发布入口收敛在「上架管理」分区的「团队应用」卡（发布快照双轨的前端闭环）
  Given 团队管理员打开应用工作台（Agent 应用）
  Then 页面无面包屑页头：应用身份（头像/名称/发布状态标签）与「进入对话」「返回应用列表」收敛在左侧菜单栏顶部（与团队工作台一致），不提供发布/重新发布按钮，发布/撤回发布统一在「上架管理」分区维护（见 @AP-S92）
  And 左侧菜单栏恒为视口内容区高度（sticky 吸顶，不随右侧分区内容拉伸）：发布状态/线上版本/当前版本吸底展示，与右侧内容高度无关；右侧内容照常随页面滚动
  When 团队应用未发布且管理员在「团队应用」卡点击「发布」，或已发布时点击「重新发布」（重新发布幂等，不依赖草稿状态）
  Then 弹出发布弹窗：版本号必填并预填当前版本（可改为本次发布版本，见 @AP-S97），确认后当前草稿配置写入发布快照并立即对线上对话生效
  And 已发布且草稿有变更时配置区警告条保留「重新发布」一键重发（不携带版本号，版本保持原值）；普通成员不展示发布入口
```

## Feature: 对话日志用户归属展示

```gherkin
@AP-S60 @auto:unit
Scenario: 日志用户列按用户类型精确归属
  Given 管理员查看应用对话日志
  Then 内部用户（normal）双行显示昵称与登录用户名（昵称在上、用户名次要色在下；两者相同或缺失时任一降级单行，全缺显示 #id），外部用户/外部应用显示「类型 #id」
  And 用户类型识别不到（none，存量脏数据）时只显示 #id，不误标为外部用户，类型标签如实展示「未知」
```

## Feature: 对话日志消息详情会话式呈现

```gherkin
@AP-S99 @auto:vitest
Scenario: 消息详情抽屉按对话界面排版
  Given 管理员在日志列表点击某会话的「查看」
  Then 抽屉内按对话界面排版还原会话现场：用户消息靠右、助手消息靠左带头像（复用正式对话页消息流样式）
  And 助手消息的 Markdown 正文与历史工具调用（折叠卡，可展开参数）照对话页呈现，不使用逐条边框容器堆叠
  And 仅还原 user 与 assistant 消息（与对话页同口径），tool/system 中间行不进会话视图；无消息时展示空态
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
  And 选项与已选标签均以「头像 + 名称」展示（2026-10-30 起与 Agent 应用多选同渲染）
  And 已绑定项回显为选中值，随配置保存一并提交 workflowApps；无已发布流程应用时提示空态
```

## Feature: 审批策略（六类绑定资源自动放通与沙箱自动放行）

```gherkin
@AP-S65 @auto:e2e
Scenario: 审批策略配置、校验与发布双轨（后端）
  Given 团队管理员在 Agent 应用配置中维护审批策略（插件/流程应用/Agent 应用/技能/知识库/知识图谱六类自动放通白名单 + 沙箱自动放行开关，随执行参数保存）
  When 任一白名单包含未绑定资源、id 非法或策略结构不合法
  Then 保存返回请求错误（400）且已保存策略不变
  When 保存合法策略并解绑某白名单资源
  Then 白名单自动收敛为本次绑定资源的子集（前端先剔除，后端强校验兜底；请求未携带的绑定字段按库中现值校验）
  And 策略随发布快照整行写入：发布后修改草稿不影响线上放行行为，重新发布后新策略生效
```

```gherkin
@AP-S66 @auto:e2e
Scenario: 审批模式下按策略自动放通（后端链路）
  Given 已发布应用配置审批策略，用户处于审批模式
  When 模型调用白名单插件/流程应用/Agent 应用/技能的工具，或知识库/知识图谱检索覆盖的全部绑定资源均在白名单内
  Then 不创建待审批记录直接执行（决策接口查询返回 missing）
  When 知识库/知识图谱检索覆盖的资源中任一未在白名单内
  Then 挂起等待人工决策，拒绝后工具不执行并向模型返回拒绝说明
  When 模型调用未开自动放行的沙箱工具（非白名单）
  Then 挂起等待人工决策，拒绝后工具不执行并向模型返回拒绝说明
  Given 用户切换为自动模式
  When 模型发起任意工具调用
  Then 全部直接执行，不经审批
  When 用户级应用配置被查询
  Then 下发策略自动放行工具名清单（静态/动态=插件名，MCP/OpenAPI=插件名__函数名，流程应用=workflow__应用名，Agent 应用=agent__应用名，技能=skill_key，知识库/图谱检索=其全部绑定资源放通时下发检索工具名）与沙箱前缀（sandbox_）
```

```gherkin
@AP-S67 @auto:vitest
Scenario: 配置分区审批策略表单（前端）
  Given 管理员进入 Agent 应用「配置」分区
  Then 六类资源选择器（插件/流程应用/Agent 应用/技能/知识库/知识图谱）选中资源后，下方逐行生成绑定资源列表（应用类带头像），行右侧提供「自动放通」勾选；沙箱自动放行开关位于「沙箱」收缩卡片
  And 已保存策略回显（勾选态与开关态），随配置保存一并提交并收敛为绑定资源子集（外部应用技能白名单固定为空）
  When 未选择任何资源
  Then 对应选择器下方不生成列表
```

```gherkin
@AP-S68 @auto:vitest
Scenario: 对话页策略自动放行工具免审批卡（前端）
  Given 应用配置审批策略且用户处于审批模式
  When 模型调用白名单插件工具或沙箱工具（策略开启沙箱自动放行）
  Then 工具行直接进入执行流转（执行中/已完成），不出现「等待审批」状态与批准/拒绝按钮
  And 不调用审批决策接口；其余工具仍按 [@AP-S52](#ap-s52) 展示审批行
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

```gherkin
@AP-S72 @auto:e2e
Scenario: 应用接入 key 换取外部 token
  When 第三方提交应用接入 key（moai-ac- 前缀）换取外部 token
  Then 签发成功，token 不再携带 scope claim（范围概念已退役），刷新按来源接入存在性与密钥重验
  But 伪造 key 未认证；无凭证且无 appId 时参数错误；接入已设密钥则兑换必须携带 secret
```

```gherkin
@AP-S73 @auto:e2e
Scenario: 应用接入 key 全量访问（去范围化改写）
  When 团队 Admin 创建应用接入且不携带任何范围字段
  Then 创建成功，key/token 对 /api/teamres 全量放行（wiki 读写、知识图谱、应用对话、ACP/A2A、MCP 均无范围门禁）
  But 创建请求携带 scopes 字段（含非法代码）被忽略仍返回成功，列表项不回显 scopes（列已退役）
```

```gherkin
@AP-S74 @auto:e2e
Scenario: 应用对话范围门禁（已退役改写：无资格位）
  When 以应用接入 key 换取外部用户 token
  Then 直接签发放行（app_chat 资格位随 scope 体系删除，无需任何勾选）
  And 会话面门禁保留「需授权（is_auth）应用仅外部用户 token」与 appId 归属团队校验
```

## Feature: 应用 ACP（agent-to-agent 协议接入）

```gherkin
Background:
  Given 团队管理员已创建应用接入 key（moai-ac-，全量）或应用资源 key（moai-tr-，团队级 ACP/A2A 一把）
  And 团队存在已发布的应用（Agent 应用或流程应用）

@ACP-S1 @auto:e2e
Scenario: ACP 端点认证与包围圈门禁
  When 无凭证 POST 应用 ACP 端点
  Then 返回 401
  When 以 moai-ac- 接入 key（不携带任何范围）或 app 资源 key（moai-tr-）访问
  Then 放行（范围勾选已退役；app key 仅 ACP/A2A 在包围圈内）
  But 以 wiki 资源 key 越圈访问返回 403 insufficient_scope；应用 token 403（无外部用户语义）
  When 以 GET 访问端点
  Then 返回 405（仅支持 POST JSON-RPC）
  When 提交非法 JSON 或未知方法
  Then 按协议返回 -32700 / -32601 错误
```

```gherkin
@ACP-S2 @auto:e2e
Scenario: initialize 握手
  When 客户端发送 initialize 请求
  Then 返回协议版本（1）与 Agent 能力（支持会话恢复）
```

```gherkin
@ACP-S3 @auto:e2e
Scenario: 应用门禁（团队归属与可用性）
  When 用其他团队的应用访问本应用 ACP 端点
  Then 返回 404（跨团队不泄露存在性）
  When 访问未发布应用
  Then 返回 403 应用不可用
```

```gherkin
@ACP-S4 @auto:e2e
Scenario: 会话创建与流式对话（Agent 应用）
  When 客户端 session/new 创建会话
  Then 返回会话 id，会话归属凭证外部用户
  When session/prompt 提交文本块
  Then 以 SSE 流式下发 agent_message_chunk 增量并以 end_turn 收口
  When 同一会话继续 prompt
  Then 上下文延续且两轮消息落库可在会话消息接口查询
```

```gherkin
@ACP-S5 @auto:e2e
Scenario: 外部用户 token 路径与会话隔离
  When 以应用接入 key 换取的外部用户 token 调用 ACP
  Then 同样可建会话并对话（会话归属该外部用户）
  But 其他凭证对该会话 prompt 返回「会话不存在」（会话隔离）
  When session/load 恢复本人会话
  Then 成功；恢复未知会话返回「会话不存在」
```

```gherkin
@ACP-S6 @auto:e2e
Scenario: 流程应用 ACP 对话
  When 对已发布流程应用 session/prompt 提交启动参数
  Then 流程执行，节点状态以 tool_call/tool_call_update 通知下发
  And AI 文本与最终回复以 agent_message_chunk 流式下发并 end_turn 收口
```

```gherkin
@ACP-S7 @auto:e2e
Scenario: session/cancel 中断进行中的一轮对话
  When 对话进行中发送 session/cancel 通知
  Then 该轮执行被中断，保留已产出内容并以 cancelled 收口
  But 无进行中对话时 cancel 幂等不报错
```

```gherkin
@ACP-S8 @auto:e2e
Scenario: 资源 key 吊销的即时性（改写：原范围勾选立即性随 scope 退役）
  When 以 app 资源 key（moai-tr-）调用 ACP
  Then 放行；管理端停用该 key 后下一请求即 401（无缓存等待）
  When 重新启用该 key
  Then 下一请求恢复放行
```

```gherkin
@ACP-S9 @auto:e2e
Scenario: 凭证语义门禁
  When 以应用 token（无外部用户语义）调用 ACP
  Then 返回 403 external_user_token_unsupported（会话归属需要外部用户身份）
  When 查询接入列表
  Then 列表项无 scopes 字段（范围概念已退役）
```

## Feature: 团队资源 key（teamres_key，moai-tr-，凭证双轨）

> 端点 `/api/team-resource-key`（团队 Admin+ 门禁）；凭证体系与包围圈总账见 [../gateway/external-api-scope-system.md](../gateway/external-api-scope-system.md) §1/§2b。场景编号 TR-\*（TA-\* 已随 access_app.scopes 退役，编号永久不复用）。

```gherkin
@TR-S1 @auto:e2e
Scenario: 创建应用接入不携带范围字段
  When 团队管理员创建应用接入（名称/描述/授权应用）
  Then 创建成功并返回 moai-ac- 前缀 key（无范围概念）

@TR-S2 @auto:e2e
Scenario: 请求携带已退役的 scopes 字段被忽略
  When 创建应用接入请求携带 scopes（含合法与非法代码）
  Then 仍返回成功（字段退役，传与不传行为一致）

@TR-S3 @auto:e2e
Scenario: 接入列表不回显范围字段
  When 查询应用接入列表
  Then 返回项不含 scopes 字段

@TR-S4 @auto:e2e
Scenario: 接入 key 直连读写全放行
  When 以 moai-ac- key 直连知识库读接口与写接口
  Then 均放行（无读/写分档）

@TR-S5 @auto:e2e
Scenario: key 直连支持 x-api-key 头
  When 以 x-api-key 头携带 moai-ac- key 调用外部接口
  Then 放行

@TR-S6 @auto:e2e
Scenario: 接入 key 直连知识图谱列表
  When 以 moai-ac- key 调用团队级知识图谱列表
  Then 放行并返回本团队图谱

@TR-S7 @auto:e2e
Scenario: 接入 key 直连团队级知识库列表
  When 以 moai-ac- key 调用团队级知识库列表
  Then 放行且返回该团队全部知识库

@TR-S8 @auto:e2e
Scenario: 接入 key 换应用 token
  When 携带 moai-ac- key 与密钥请求换取 token
  Then 签发应用 token（范围概念已退役）

@TR-S9 @auto:e2e
Scenario: 换用户 token 无需对话资格位
  When 携带 key/密钥/appId/外部用户标识换取用户 token
  Then 签发成功（app_chat 门禁随 scope 体系删除）

@TR-S10 @auto:e2e
Scenario: 生成知识库资源 key
  When 团队管理员为知识库生成资源 key
  Then 返回 moai-tr- 前缀 key 且 created=true、resourceId 为该库 id

@TR-S11 @auto:e2e
Scenario: get-or-create 幂等
  When 对同一（团队, 知识库）再次生成资源 key
  Then 原样返回既有 key 且 created=false（行 id 不变）

@TR-S12 @auto:e2e
Scenario: 各资源实例各自一把 key（不同应用 key 互不相同）
  When 为另一知识库与第二个应用分别开启功能并生成资源 key
  Then 各自生成（wiki 行 resourceId=wikiId 字符串、app 行 resourceId=应用 Guid），不同应用的 key 互不相同；同一应用的 ACP/A2A 共用其应用的一把

@TR-S13 @auto:e2e
Scenario: 应用资源 key 幂等
  When 对同一应用再次生成应用资源 key
  Then 返回同一行（一应用一把，ACP/A2A 共用）

@TR-S14 @auto:e2e
Scenario: 资源不归属团队时生成被拒
  When 为本团队指定不存在的知识库 id 生成资源 key
  Then 返回不存在（404，不泄露存在性）

@TR-S15 @auto:e2e
Scenario: 资源 key 列表回显
  When 团队管理员查询资源 key 列表
  Then 回显全部 key（明文，仅 Admin+ 可见），wiki/应用各行齐全

@TR-S16 @auto:e2e
Scenario: wiki 资源 key 本库读写放行
  When 以 wiki 资源 key 直连本库 REST 读接口与写接口
  Then 均放行

@TR-S17 @auto:e2e
Scenario: wiki 资源 key 越库被拒
  When 以 wiki 资源 key 访问同团队其它知识库
  Then 返回 403 insufficient_scope

@TR-S18 @auto:e2e
Scenario: wiki 资源 key 团队级列表只见绑定库
  When 以 wiki 资源 key 调用团队级知识库列表
  Then 返回 200 且仅含绑定库（OnlyWikiId 过滤）

@TR-S19 @auto:e2e
Scenario: wiki 资源 key 越圈被拒
  When 以 wiki 资源 key 访问知识图谱列表或 agent 会话端点
  Then 均返回 403 insufficient_scope

@TR-S20 @auto:e2e
Scenario: wiki 资源 key 本库 MCP 放行、它库拒绝
  When 以 wiki 资源 key 对本库 MCP 端点做 initialize
  Then 握手成功
  When 对其它库的 MCP 端点做 initialize
  Then 返回 403 insufficient_scope

@TR-S21 @auto:e2e
Scenario: app 资源 key 调用 ACP
  When 以 app 资源 key 对已发布应用 ACP 端点 initialize
  Then 握手成功

@TR-S22 @auto:e2e
Scenario: app 资源 key 获取 A2A Agent Card
  When 以 app 资源 key 请求 a2a/agent.json
  Then 返回 200

@TR-S23 @auto:e2e
Scenario: app 资源 key 越圈被拒
  When 以 app 资源 key 访问知识库端点或 agent 会话端点
  Then 均返回 403 insufficient_scope（app key 仅 ACP/A2A）

@TR-S24 @auto:e2e
Scenario: 重置资源 key
  When 团队管理员重置某资源 key
  Then 返回新 key 且行 id 不变（直连身份延续）

@TR-S25 @auto:e2e
Scenario: 重置后旧 key 立即失效
  When 以重置前旧 key 直连
  Then 返回 401；以新 key 直连放行

@TR-S26 @auto:e2e
Scenario: 停用与恢复即时生效
  When 管理端停用资源 key 后直连
  Then 返回 401；重新启用后下一请求恢复放行

@TR-S27 @auto:e2e
Scenario: 资源 key 不可换 token
  When 以 moai-tr- key 请求换取外部 token
  Then 拒绝（400/401，换 token 仅认 moai-ac-）

@TR-S28 @auto:e2e
Scenario: 删除资源 key 后直连失效
  When 管理端删除某资源 key 后以原 key 直连
  Then 返回 401；重新 get-or-create 生成新 key（created=true）

@TR-S29 @auto:e2e
Scenario: 伪造凭证一律未认证
  When 以伪造的 moai-tr- / moai-ac- key 或旧 moai- 前缀 key 直连
  Then 均返回 401

@TR-S30 @auto:e2e
Scenario: 跨团队接入 key 被拒
  When 以其它团队的 moai-ac- key 访问本团队知识库
  Then 返回不存在（404）

@TR-S31 @auto:e2e
Scenario: 跨团队生成资源 key 被拒
  When 其它团队管理员对本团队资源生成资源 key
  Then 返回不存在（404）

@TR-S32 @auto:vitest
Scenario: 应用接入分区挂应用资源 key 卡片
  Given 团队管理员进入「应用接入」分区
  Then 渲染应用资源 key 卡片，可生成（get-or-create）并展示/复制 key
  And 新建接入表单无范围勾选项

@TR-S33 @manual
Scenario: 知识库 MCP 页挂库资源 key 卡片
  Given 团队管理员进入知识库详情「MCP」页
  Then 渲染本库资源 key 卡片（父页传 teamId/canManage），生成后可复制端点地址 + key 接入

@TR-S34 @manual
Scenario: 知识图谱 MCP 页挂图资源 key 卡片
  Given 团队管理员进入知识图谱「MCP」页
  Then 渲染本图资源 key 卡片，行为同知识库（重置/启停/删除同款交互）

@TR-S35 @manual
Scenario: 非管理员不渲染资源 key 卡片
  Given 普通成员查看上述页面
  Then 不渲染资源 key 卡片与管理入口（后端 Admin+ 门禁兜底 403）
```

## Feature: Agent 对话知识库工具（检索 + 按序号补取片段）

```gherkin
@AP-S75 @auto:e2e
Scenario: 绑定知识库的应用对话中检索并按片段序号补取上下文
  Given 团队应用绑定知识库且文档已向量化，桩模型按「检索 → 补取片段 → 回答」驱动工具
  When 用户提问触发 search_knowledge_base
  Then 返回命中文档、片段序号（chunkIndex）、文档切片总数、得分与去重的相邻上下文
  When 模型按返回的 documentId 与 chunkIndex 调用 get_knowledge_base_chunk
  Then 返回该文档指定序号的原文片段内容
  When 以不存在的文档 id 调用 get_knowledge_base_chunk
  Then 返回工具级错误说明（文档不存在或不属于绑定的知识库），对话不中断
```

## Feature: 应用安全（内容脱敏）

```gherkin
@AP-S76 @auto:unit
Scenario: 安全配置的权限与校验
  When 团队管理员保存应用安全配置（总开关/作用范围/规则列表）
  Then 保存成功且再次查询回显一致；未保存过的应用查询返回默认值
  When 普通成员保存安全配置
  Then 返回禁止
  When 保存含非法类型、空正则或不可编译正则的自定义规则
  Then 返回参数错误
  When 保存策略代码不在 redact/mask/hash/block/flag 之内，或为模型回复规则配置 block 策略
  Then 返回参数错误（模型回复流式不可撤回，不允许 block）

@AP-S77 @auto:unit
Scenario: Agent 对话工具调用结果脱敏
  Given 应用启用脱敏并配置命中规则，工具参数与返回值均含敏感信息
  When 模型调用工具（list_tools / call_tool 或任意注册工具）
  Then 工具按原始参数执行，脱敏不污染执行
  And 模型后续轮次可见的工具结果、流式下发与落库记录均为脱敏后内容

@AP-S78 @auto:unit
Scenario: 工具参数与模型回复的范围脱敏（两套规则相互独立）
  Given 应用启用脱敏并配置内容规则，同时为模型回复独立配置专属规则（与内容规则互不影响）
  When 模型发起带敏感信息的工具调用或输出含敏感信息的回复
  Then 工具调用参数按「工具参数」范围套用内容规则脱敏，模型回复正文按「模型回复」范围套用模型专属规则脱敏
  When 关闭对应范围开关
  Then 对应内容原样保留
  When 仅开启模型回复脱敏（内容脱敏总开关关闭）
  Then 模型回复仍按模型专属规则脱敏（模型回复不依赖内容脱敏总开关），工具内容不脱敏

@AP-S79 @auto:unit
Scenario: 存量消息读取兜底脱敏
  Given 应用在产生消息之后才启用脱敏
  When 用户查询会话消息或管理员查询对话日志
  Then 返回的消息按当前规则脱敏（覆盖启用安全策略前的存量数据）

@AP-S80 @auto:vitest
Scenario: 应用安全设置与安全事件的分区归属
  Given 团队管理员进入应用工作台
  Then Agent 应用侧栏无独立「安全」菜单，安全（内容脱敏）为「配置」分区收缩卡片（默认收起、展开加载，总开关与工具调用结果/工具调用参数/模型回复/用户输入四个作用域常显）；流程应用从「配置」二级组进入「安全」页，两者设置内容一致
  When 开启内容脱敏或模型回复开关
  Then 对应设置内嵌的规则编辑器（内置类型 + 自定义正则 + 处理策略选择，按策略显隐替换文本与保留末位参数）可维护，两组规则相互独立、互不干扰，模型回复策略选项不含 block
  When 关闭内容脱敏
  Then 编辑器隐藏但已配置规则保留，保存不清空
  When 管理员进入「日志」分区 / 普通成员查看
  Then 「日志」为 Tab 容器（日志默认 / 安全事件 / 使用分析，切换懒加载），管理员可见安全事件审计列表（时间/作用域/规则/动作/命中）；侧栏无独立「监控」菜单（旧 /monitor 深链落到使用分析 Tab）；成员仅保留日志 Tab，不渲染安全收缩卡片与事件列表，无保存与添加规则入口
```

```gherkin
@AP-S86 @auto:unit
Scenario: 规则处理策略语义（redact/mask/hash/block/flag）
  Given 应用配置规则并指定处理策略（存量规则无 strategy 字段时按 redact 处理）
  When 内容被规则命中
  Then redact 替换为规则替换文本（默认 ***）
  And mask 按类型样式部分遮蔽（手机 3+4、证件 6+4、邮箱保留域名，custom/银行卡保留末 keepLast 位）
  And hash 以部署级派生 pepper 做 SHA256 截断（同值同哈希保留可关联性，换部署换盐；未派生 pepper 时降级为 redact 不裸露原文）
  And flag 原样放行仅记录命中；block 在非强制上下文（读侧兜底/落库）降级为 [BLOCKED] 占位替换
  When 规则类型为 injection（提示词注入启发式，内置中英模式集：指令覆盖/系统提示窃取/越狱关键词，大小写不敏感）
  Then 正常对话不误伤，注入模式命中按所配策略处理（建议 block/flag）
  When 用户输入/工具结果/工具参数/模型回复命中规则
  Then 对应作用域的命中按（作用域, 规则, 策略, 动作）聚合进收集器，供运行结束批量写审计
  When 模型回复规则配置 block 策略
  Then 流式输出走缓冲窗拦截（文本积压超阈值先整段检测再释放，保留尾部与后续合并重检覆盖跨片边界），命中即截断后续输出并下发拦截提示
  And 非流式路径对完整回复检测，命中整条替换为拦截占位（MAF 合并 update 时整段一次判定，语义一致）
```

```gherkin
@AP-S87 @auto:e2e
Scenario: 用户输入检查点与工具调用拦截（block 强制语义）
  Given 应用启用安全配置且内容规则含 block 策略，开启用户输入与工具参数范围
  When 用户发送命中 block 规则的消息
  Then 本轮对话被拒绝并回复策略拦截提示，原文不落库（审计记录 input/blocked）
  When 用户发送含 PII 的消息（input 范围 mask/redact/hash 策略）
  Then 发送给模型的用户消息已脱敏（模型可见遮蔽形态），落库用户消息为脱敏后内容
  And 模型回复按模型专属规则处理，流式下发与落库一致（hash 策略同值跨会话哈希一致）
  When 模型发起参数命中 block 规则的工具调用
  Then 工具不被执行，模型收到拒绝应答（审计记录 toolArgs/blocked）
  When 用户发送命中 injection 规则的消息（input block 策略）
  Then 注入消息被输入检查点拦截，正常消息不误伤
```

```gherkin
@AP-S88 @auto:e2e
Scenario: 安全事件审计
  Given 应用安全规则发生命中（脱敏/拦截/观测）
  When 命中聚合为事件写入 app_security_event（含应用/团队/会话/用户归属与作用域/规则/策略/动作/次数）
  Then 团队管理员可分页查询 GET /api/app/{id}/security/events（按时间倒序，支持 scope 与关键字过滤）
  And 关键字匹配规则名称与规则类型代码（类型代码大小写不敏感；内置规则 rule_name 为空，中文类型别名「手机号/身份证/银行卡/注入」等归一到类型代码命中）
  But 普通成员查询返回 403
```

## Feature: 日志分区走查优化（会话 JSON 下载 / 安全事件工具栏 / 使用分析大屏）

```gherkin
@AP-S135 @auto:vitest
Scenario: 会话日志下载 JSON
  Given 团队管理员在应用「日志」分区列表
  When 点击某条日志的「下载」（或详情抽屉右上角「下载 JSON」）
  Then 拉取该会话消息全量并触发 JSON 文件下载（文件名即会话 id：`{sessionId}.json`，便于与日志列表对号）
  And 下载内容含 exportedAt/appId/session（行元信息）与 messages 数组（含 system/tool 行与思维链原始字段，比抽屉会话视图更完整）
  And 下载成功后提示「会话 JSON 已下载」
```

```gherkin
@AP-S136 @auto:vitest
Scenario: 安全事件工具栏与动作语义色
  Given 团队管理员进入「安全事件」Tab
  Then 默认每页 50 条；底部分页换页与切换每页数量均生效（pageSize 为受控状态，切换容量后回到第 1 页重新加载）
  And 工具栏为规则关键字搜索框 + 作用域下拉 + 查询按钮（裸排页面底色；查询即刷新，不再单设独立刷新按钮；筛选简单不设重置）
  When 输入规则名（如「供应商」）或中文类型别名（如「手机号」）查询
  Then 按关键字与作用域过滤并回到第 1 页（后端过滤见 @AP-S88）
  And 动作列带语义色标签（已拦截红 / 已脱敏金 / 已记录蓝），时间列统一 YYYY-MM-DD HH:mm
```

```gherkin
@AP-S137 @auto:vitest
Scenario: 使用分析大屏布局
  Given 团队管理员进入「使用分析」Tab
  Then 顶部为品牌渐变汇总横幅：合计 Tokens 大数 + 调用次数 + 输入/输出分列 + 缓存命中率环形（白环、环心透出渐变底，按占输入 token 计算）+ 最近使用时间
  And 活动统计为五张彩色图标指标卡（累计使用次数/会话总数/使用用户数/消息总数/记忆条数）
  And 按模型分布为排行条列表：名次徽标 + 模型名 + 长度条（按合计 tokens 归一）+ 占比标注与调用/输入/输出/缓存明细行，无数据时保留空态与滞后提示
```

## Feature: 前端展示工具（对话侧边栏：文档 / 代码 / 图表）

```gherkin
@AP-S81 @auto:unit
Scenario: 前端展示工具的注册与桩执行
  Given 对话请求携带前端工具开关请求头且为 Agent 应用对话
  Then 模型直接获得 ui_show_document / ui_show_code / ui_show_chart 三个工具与使用指引（不经 list_tools/call_tool 渐进披露）
  When 模型调用任一前端展示工具（含漏传字段或参数形态不符）
  Then 后端不执行任何业务，返回固定「已渲染」结果，AG-UI 事件流以真实工具名推送工具调用，对话不中断
  When 对话请求未携带该请求头
  Then 以上工具均不注册（嵌入组件与外部渠道行为不变）
```

```gherkin
@AP-S82 @auto:vitest
Scenario: 长内容折叠为卡片并自动打开侧边栏
  Given 模型在对话中调用前端展示工具并产出标题与内容
  Then 消息流仅折叠展示一张卡片（类型标签 + 标题 + 查看），正文不重复完整内容
  And 右侧侧边栏自动打开并渲染内容（文档渲染 markdown、代码展示语言标签与代码、图表按 GPT-Vis 图表配置渲染）
  And 模型把图表配置序列化为 JSON 字符串或围栏包裹时，前端归一化后同样完成渲染
  When 用户关闭侧边栏
  Then 点击消息中的卡片可重新打开对应内容
```

```gherkin
@AP-S83 @auto:vitest
Scenario: 历史会话回放恢复前端展示工具卡片
  Given 历史助手消息包含前端展示工具调用及参数
  When 用户重新打开该会话
  Then 工具调用同样渲染为折叠卡片，点击可在侧边栏打开
  But 侧边栏不自动展开（回放仅点击触发）
```

```gherkin
@AP-S84 @auto:vitest
Scenario: 异常参数的渲染降级
  Given 模型产出无效的前端展示工具调用（参数流中断、内容缺失或未知 ui_ 工具名）
  Then 卡片展示「生成中/内容缺失」且不可点击，对话与正文展示不受影响
  When 图表配置无法渲染（type 不在支持白名单、数据形态不符该类型契约（行级缺 data 数组、dual-axes 缺 series、liquid 缺 percent、树/图家族 data 非对象）或历史遗留的旧版 ECharts option）
  Then 卡片同样按内容缺失降级，不挂载图表
  When 渲染层仍遇到不可渲染的配置
  Then 侧边栏展示兜底提示
```

```gherkin
@AP-S90 @auto:vitest
Scenario: 行级图表字段漂移自动修复与非 data 数组形态放行
  Given 模型给出的图表 type 与数据字段名不符渲染契约（如 column 数据写成 time/value）
  When 解析图表配置
  Then 缺失的 x/value 字段按常见别名整列自动改名（group 等其余字段保留），字段正确的数据原样通过
  When 模型按正确契约给出 dual-axes（categories+series）、liquid（percent）或树/图家族的 data 对象
  Then 配置同样通过校验并交渲染层渲染（此前被 data 数组校验误杀）
```

```gherkin
@AP-S85 @auto:vitest
Scenario: 侧边栏多标签与宽度调节
  Given 一轮对话产出多份前端展示内容（多张图表/文档）
  Then 侧边栏头部以标签展示全部内容项，面板自动聚焦最新一项，点击标签切换展示
  When 关闭当前标签
  Then 自动切换到相邻标签，全部关闭后面板收起
  When 收起面板后点击消息中的卡片
  Then 面板重新展开且剩余标签保留
  When 拖动面板左缘手柄
  Then 面板宽度实时调节（有上下限，主对话区保留最小宽度）并持久化到本地
```

```gherkin
@AP-S89 @auto:vitest
Scenario: 图表导出为图片下载
  Given 侧边栏正在展示一张 GPT-Vis 图表
  Then 头部操作区提供图片下载入口（table 图表无画布，不提供该入口）
  When 用户点击下载
  Then 当前图表画布以 PNG 保存到本地，文件名取自内容标题（非法字符净化、空回退「图表」）
  And 导出为白底位图，分辨率取画布像素尺寸（devicePixelRatio 高清导出）
  When 导出失败（无画布或导出异常）
  Then 提示下载失败，对话与侧边栏不受影响
```

## Feature: 外部渠道入口（飞书机器人单聊）

```gherkin
@AP-S94 @auto:e2e
Scenario: 应用列表带出外部渠道入口
  Given 团队应用接入了启用的外部渠道连接（飞书/钉钉）
  When 团队成员查询团队内部应用列表，或任意登录用户打开应用广场
  Then 已接入应用的列表项携带 externalChannels：channelType=feishu 的 chatUrl 为按连接接入域名拼出的飞书 applink 机器人单聊地址
  And channelType=dingtalk 的 chatUrl 为 dingtalk://dingtalkclient/action/jumprobot?dingtalkid=…（优先机器人 dingtalk id，未回填用 AppKey 兜底，见 dingtalk @DT-S27）
  And 前端应用卡片右上角据此显示各渠道品牌图标，点击新窗口打开 chatUrl
  But 未接入渠道的应用列表项为空列表，卡片不显示图标
  And 连接被禁用后列表不再携带该渠道（重新启用恢复）
```

## Feature: 应用列表团队名称与使用次数

```gherkin
@AP-S95 @auto:e2e
Scenario: 应用列表携带团队名称与使用次数
  Given 平台存在归属团队的内部应用，部分应用已有会话
  When 查询团队内部应用列表、应用广场公开列表或团队外部应用列表
  Then 列表项携带 teamName=所属团队名称（团队不存在时为空串）与 useCount=该应用累计使用次数（会话创建计数，Redis 计数器每分钟批量累加，删除会话不回退，最多滞后约 1 分钟，见 @AP-S100）
  And 前端应用广场卡片底部两端对齐：左侧「团队图标 + 团队名称」（不显示「团队」前缀），右侧「火焰图标 + N 次使用」
  And 应用广场列表按使用次数降序排列，同次数按发布时间倒序（未发布快照兜底创建时间）
  But 未产生会话的应用 useCount 为 0，卡片照常显示「0 次使用」
```

## Feature: 应用使用次数计数（Redis 计数器批量落库）

```gherkin
@AP-S100 @auto:e2e
Scenario: 会话创建累计使用次数
  Given 用户对某应用拥有会话权限
  When 通过任一会话入口创建正式会话（内部对话 / 外部 token / 飞书 / 钉钉 / 企业微信机器人 / ACP / A2A）
  Then 该应用使用次数经 Redis 计数器自增，计数任务批量落库后应用列表 useCount 增加
  And 计数为异步累加，列表读数最多滞后约 1 分钟；多次会话创建在同一周期内合并累加不丢失

@AP-S101 @auto:e2e
Scenario: 删除会话不回退累计使用次数
  Given 某应用已有使用次数落库（列表可见）
  When 会话归属用户删除自己的一条会话
  Then 应用列表 useCount 立即不回退
  And 跨计数落库周期后 useCount 仍保持不变（删除不触发任何扣减）

@AP-S102 @auto:e2e
Scenario: 累计使用次数与存活会话数双口径区分
  Given 某应用累计创建 3 个会话且其中 1 个已被删除
  When 团队管理员查询使用分析
  Then 活动统计中累计使用次数为 3（删除不减少）
  And 存活会话数为 2（软删除会话被过滤）、使用用户数按会话归属用户+类型去重

@AP-S103 @auto:e2e
Scenario: 调试会话不计入使用次数
  Given 团队管理员对未发布应用创建调试会话
  Then 调试会话不进正式会话列表，也不推入使用次数计数
  And 与 token 用量口径一致（调试会话同样不计模型用量）
```

```gherkin
@AP-S104 @auto:vitest
Scenario: 长期记忆并入配置分区收缩卡片
  Given 团队管理员打开内部 Agent 应用工作台
  Then 左侧菜单不提供独立「长期记忆」入口（旧 /memory 深链回落「配置」分区）
  And 「配置」分区以收缩卡片承载长期记忆（默认收起、展开才加载，仅内部应用且 Admin+ 渲染，外部应用不渲染）
  When 展开卡片调整记忆配置并点击「保存设置」
  Then memory 节随主配置一次提交（面板无自己的保存/发布按钮），随发布快照生效；卡片未展开未注册时保存保持记忆配置原值
  And 应用记忆统计与「清空应用全部记忆」危险区保留在卡片内
```

## Feature: 会话上下文用量查询与环形指示

```gherkin
@AP-S105 @auto:e2e
Scenario: 查询会话上下文用量的门禁与零值回显
  Given 用户拥有一个尚未对话的会话
  When 会话归属用户查询该会话的上下文用量
  Then 返回会话累计 token（输入/输出/总数/缓存命中，无对话时均为零值）、当前上下文占用（最近一次模型调用的输入 token）与生效对话模型名及其上下文窗口上限（未配置或未知时为 0）
  And 未登录查询返回 401；非会话归属用户（含同团队成员）按不存在处理返回 404，不泄露会话存在性
  But 空会话 id 与会话消息端点行为一致归一为 404（GET 命令由控制器手动构造，不走模型绑定自动验证）

@AP-S106 @auto:e2e
Scenario: 对话轮次后上下文用量累计与生效模型回显
  Given 应用已绑定对话模型且会话已完成至少一轮真实对话
  When 会话归属用户查询该会话的上下文用量
  Then 输入与总 token 大于零（对话热态累计优先，落库值兜底）
  And 模型名回显为生效配置的对话模型（已发布应用按发布快照解析），上下文窗口为数值型
  And 当前上下文占用等于最近一轮模型调用的输入 token（上下文压缩后随下一轮调用回落，环形指示按此口径显示比例）
  And 模型响应上报了缓存命中（OpenAI 兼容 prompt_tokens_details.cached_tokens）时缓存命中 token 大于零，应用使用分析（汇总与按模型）同步聚合出缓存命中 token（审计批量落库约 1 分钟滞后）
```

```gherkin
@AP-S107 @auto:vitest
Scenario: 对话输入卡底部的上下文占用环形指示
  Given 用户进入 Agent 应用对话页
  When 当前会话存在 token 用量
  Then 输入卡底部操作区发送（或停止）按钮左侧显示环形指示与紧凑 token 统计（如 7.2k / 128k），悬浮展示模型名、当前上下文/上限与累计输入输出明细
  And 占用比例按当前上下文占用（最近一轮输入 token）计算：达 70% 环形转警示色、达 90% 连文字转危险色；模型上限未知时仅展示 token 数不画比例弧
  And 悬浮明细始终展示「缓存命中 N tokens（占累计输入百分比）」行，模型未上报时显示 0
  But 欢迎态（无会话）或会话尚无用量时不显示环形指示；会话切换与每轮对话结束后自动刷新
```

## Feature: Agent 应用绑定为工具（Agent 复用团队其它 Agent）

```gherkin
@AP-S109 @auto:e2e
Scenario: 绑定校验与配置回读（后端）
  Given 团队管理员在 Agent 应用配置中绑定其它 Agent 应用作为工具（agentApps）
  Then 仅允许本团队已发布、未禁用的内部 Agent 应用，且不能绑定自身（未发布/类型不符/不存在/自身一律 400，普通成员 403）
  And 保存后配置回读一致；请求不携带该字段（旧前端保存其他字段）时保持原绑定，空数组清空
  And 绑定随发布快照整行写入（D42 契约含 agentApps）：发布后草稿修改不影响线上，重新发布后生效
  And 保存时沿 Agent→Agent 工具边做闭包检测：互绑（A↔B）与传递环（A→B→C→A 绑 C↔A）一律 400 拒绝；绑定「含自身节点的流程」不在此拦截（该类环由流程侧守卫负责，见 @WF-S26 语义）
```

```gherkin
@AP-S110 @auto:e2e
Scenario: 对话中调用 Agent 应用工具（后端链路）
  Given Agent 应用已绑定可用模型与已发布 Agent 应用并发布，用户发起正式会话对话
  When 模型经 call_tool 调用 agent__<应用名>（参数 query，兼容别名键；可选 history 携带 [{role,content}] 近期上下文，截最近 10 条）
  Then 按目标 Agent 应用的发布快照无头单轮执行（携带其自身工具链，不带调用方会话历史），最终回复文本作为工具结果回传；异常转工具错误文本不中断对话
  And 目标应用被取消发布/禁用后重新发布绑定方应用时该工具自然下线（工具列表不再装配）
  But 运行期检测到目标已在祖先调用链上（保存竞态等绕过保存校验的路径）时拒绝执行并返回工具错误文本
```

```gherkin
@AP-S111 @auto:e2e
Scenario: 嵌套执行按发布快照与调用链防护（后端链路）
  Given 主 Agent 绑定的目标 Agent 已发布，目标的管理员随后修改其草稿提示词且未重新发布
  When 主 Agent 对话中调用 agent__ 工具
  Then 嵌套执行使用目标的发布快照（草稿改动不生效），工具结果为按发布快照执行的回复
  And 工具参数 history 中的近期上下文透传给目标 Agent（仅本次调用内有效）
  And 流程 agentApp 节点调用的 Agent 同样入调用链：其 Agent 工具无法再回调链上祖先应用（运行期纵深防御）
```

```gherkin
@AP-S112 @auto:vitest
Scenario: 配置分区 Agent 应用多选（前端）
  Given 管理员进入 Agent 应用「配置」分区
  Then 「Agent 应用」多选与「流程应用」并列，仅列出本团队已发布的其它 Agent 应用（自身与未发布应用被过滤）
  And 选项与已选标签均以「头像 + 名称」展示（头像按存储路径解析、缺失回退首字母，搜索按名称过滤）
  And 已绑定项回显为选中值，随配置保存一并提交 agentApps；无候选时提示空态
```

## Feature: 多模型绑定与对话端模型切换

```gherkin
@AP-S138 @auto:e2e
Scenario: 可选模型列表保存校验与归一
  When Admin 保存 Agent 配置携带 modelIds（含默认模型、≤20 个、全部该团队可用）
  Then 返回成功，重新查询回显 modelIds 一致（已去重）
  When modelIds 不含默认模型（model_id）
  Then 返回参数错误（「默认模型必须在可选模型列表中」）
  When modelIds 含不存在/已停用/该团队无权使用的模型
  Then 返回参数错误（逐个与默认模型同口径校验）
  When modelIds 超过 20 个
  Then 返回参数错误
  When 保存不携带 modelIds（存量客户端形态）或携带空列表
  Then 返回成功且归一为仅默认模型（单元素列表，存量单模型应用行为不变）
```

```gherkin
@AP-S139 @auto:e2e
Scenario: 对话端按请求头切换模型与回退
  Given 应用可选模型为 [默认 A，备用 B] 且两模型该团队可用
  When 携带请求头 X-Moai-Model-Id=B 发起对话
  Then 本轮按 B 装配（模型客户端/用量计数/压缩阈值与摘要模型均按 B 推导），会话 last_model_id 更新为 B
  And 会话上下文用量接口回显 modelName=B 与 B 的上下文窗口
  When 携带 X-Moai-Model-Id 指向可选列表外的模型、不可用模型或非法值
  Then 静默回退默认模型 A 对话成功（管理员调整配置后旧会话的旧选择平滑降级，不炸会话；last_model_id=A）
  When 不携带该请求头（外部渠道/ACP/A2A/嵌入组件/旧前端）
  Then 按默认模型 A 对话，行为与存量一致
  When 应用未配置模型（模型 id 空）或默认模型也不可用
  Then 返回既有参数错误（「应用尚未配置对话模型」/「应用的对话模型不可用」），语义不变
```

```gherkin
@AP-S146 @auto:vitest
Scenario: 对话端切换器初始选中应用默认模型
  Given 应用配置了多个可选模型，且默认模型不是团队可用列表的第一项
  When 团队成员打开应用对话页（未手动切换过模型）
  Then 模型切换器默认选中应用默认模型（userconfig.defaultModelId），而非可用列表第一项
  And 默认模型不在可用集合时落回第一项
```

```gherkin
@AP-S140 @auto:e2e
Scenario: 发布快照携带可选模型且回显按生效口径
  Given 草稿配置可选模型 [A,B] 并发布（快照含 modelIds），随后把草稿可选模型改为 [A,C]（不重新发布）
  Then 用户配置接口（userconfig）仍按发布快照回显 默认=A、modelIds=[A,B]（与正式对话同一生效口径），对话端切换器据此展示
  And 调试会话按最新草稿装配（切换器选项=[A,C] 口径），正式会话按快照 [A,B]
  When 存量应用的发布快照无 modelIds 字段或行内 model_ids 为空
  Then 运行时与回显均归一为 [快照 modelId]，行为与存量一致
```

```gherkin
Scenario: 配置页多选与默认模型、对话端切换器（前端）
  Given 管理员进入 Agent 应用「配置」分区
  Then 「对话模型」为多选下拉（保留团队/系统渠道分组与参数 Tag），「默认模型」为单选下拉且选项仅限已选模型
  And 模型下拉行内附渠道名次要文本（不同渠道可能存在同名模型，名称相同凭渠道名区分）；关闭态与多选标签仍显示纯模型名（optionLabelProp），搜索按「模型名（渠道名）」匹配
  And 已选列表变更时默认模型不在其中则自动落到第一项；保存前端拦截：未选模型、默认模型不在可选列表
  Given 用户进入应用对话页或调试页
  Then 仅可选模型多于 1 个时在输入区工具栏（调试页在标题栏）展示模型切换器，选项按应用可选模型 ∩ 团队当前可用过滤，默认项带「默认」标记
  And 切换器下拉行内同样附渠道名次要文本，关闭态显示纯名称（含默认标记），搜索按「名称（渠道）」匹配
  When 用户切换模型后发送消息
  Then 该轮对话请求携带 X-Moai-Model-Id，随后上下文占用 Tooltip 的模型名跟随所选模型（会话 last_model_id 回显）
```

## Feature: Agent 应用绑定团队智能体（team-agent Phase 2：应用绑定与运行时派发）

```gherkin
@AP-S150 @auto:e2e
Scenario: 绑定保存校验
  Given 团队管理员配置 Agent 应用绑定团队智能体（team_agent_ids 去重 ≤20，须为本团队启用中智能体）
  When 绑定任一智能体而子代理协作未开启
  Then 保存 200（D53 解耦：绑定只代表主智能体提示词注入主会话，不再要求开启子代理协作；前端关闭协作亦不再清空绑定）
  When defaultTeamAgentId 不在列表内 / 智能体 id 伪造或跨团队 / 智能体已停用 / 应用为外部应用
  Then 分别 400（默认必须在列表 / 包含不存在已停用或无权使用 / 外部应用不能绑定团队智能体）
  When 智能体主智能体声明的插件/知识库/知识图谱依赖未绑定到应用
  Then 不阻断绑定（弱依赖语义，team-agent sdd D3）：保存 200，agent-config 的 teamAgents 选项随带声明依赖（mainPluginIds/mainWikiIds/mainGraphIds）供前端对比应用绑定提示「一键添加」补齐；子依赖是运行时白名单不做保存校验
  And 保存请求不携带 teamAgentIds 时保持已保存绑定不变（旧客户端不清空）；defaultTeamAgentId 未携带时归一为列表第一个，列表为空归一 null
```

```gherkin
@AP-S151 @auto:e2e
Scenario: 配置回显与 userconfig 切换器数据
  Then agent-config 回显 teamAgentIds / defaultTeamAgentId / teamAgents（按绑定顺序实时行，含 key/name/头像/启用标记，供管理页与调试面板）
  And userconfig 与正式对话同一生效口径：已发布按快照内嵌定义回显 teamAgents（Enabled 恒 true）与 defaultTeamAgentId，未发布按实时启用行
```

```gherkin
@AP-S152 @auto:e2e
Scenario: 发布快照内嵌智能体定义（冻结）
  When 发布应用
  Then 快照随 teamAgentIds/defaultTeamAgentId 携带 teamAgentDefs（主提示词/主依赖/子智能体定义内嵌，配置行 team_agent_defs 同步）
  And 发布后编辑或停用团队智能体，已发布应用仍按快照旧定义执行（主提示词与子智能体提示词均为发布时版本）
  And 发布时绑定存在已删除/停用/非本团队智能体 → 400「绑定的团队智能体已被删除或停用，无法发布」
  And 调试会话按实时草稿定义装配（跟随编辑，草稿/发布双轨同口径）
```

```gherkin
@AP-S153 @auto:e2e
Scenario: 运行时主智能体注入与具名派发工具
  Given 应用绑定团队智能体（单选或多选）
  Then 主代理系统提示词按「应用提示词 → [协作模式：{名称}] 主提示词 → 专家提示词」层级组装
  And 生效智能体的子智能体逐个注册为具名 subagent_{key} 工具（描述=子智能体描述+委派说明）替代通用 spawn_subagent；智能体无子智能体时保留通用工具；未绑定应用行为完全不变
  When 用户经 X-Moai-Team-Agent-Id 请求头切换（须在绑定列表内，非法值静默回退）
  Then 该轮主提示词与派发工具集切换为所选智能体（如规划师 plan ↔ 构建器）；未携带按默认（defaultTeamAgentId，缺省取列表第一个）
  And 会话行落 last_team_agent_id 供回显；外部应用装配时兜底解绑（CloneWithExternalRestrictions）
```

```gherkin
@AP-S154 @auto:e2e
Scenario: 具名子智能体派发（提示词注入 + 声明即边界 + 深度恒 1）
  When 主代理调用 subagent_{key}（task/guidance）
  Then 子会话系统提示词 = 应用提示词 + [子智能体：{名称}] 描述 + 子提示词 + 执行要求 + [子代理模式] 后缀；模型跟随主会话本轮实际模型（解析失败回退应用默认模型）
  And 工具按「声明即边界」收缩：仅注入 子声明 ∩ 应用绑定 的插件/知识库/知识图谱；技能/流程/Agent 应用/沙箱/前端展示工具/长期记忆/派发工具全部退出（声明为空则零工具、无 list_tools 元工具）
  And 超时与结果截断沿用子代理协作配置（timeoutSeconds/resultMaxChars）；仅最终结论回传主代理
```

```gherkin
@AP-S155 @auto:vitest
Scenario: 配置页绑定 UI
  Then 「子代理协作」开关参数区下方展示「团队智能体」多选下拉（方形头像+名称 option，仅启用中智能体，≤20）与「默认智能体」单选下拉（仅多选 ≥2 时显示，选项限已选）
  And 已选变更时默认项不在列表自动落第一项；关闭协作开关时清空绑定；保存前端拦截「绑定未开协作」并随主配置提交 teamAgentIds/defaultTeamAgentId
  And 外部应用不展示该区块
```

```gherkin
@AP-S156 @auto:vitest
Scenario: 对话端智能体切换器
  Then 仅绑定智能体 ≥2 个时展示切换器（输入区工具栏、模型切换器右侧、方形头像 option、关闭态显示当前生效者名称）；单选/未绑定不展示（后端直接生效）
  And 每轮对话请求携带 X-Moai-Team-Agent-Id；调试面板同构复用，且切换器选项按应用草稿绑定的启用中智能体（D53 修正：调试对话按草稿装配，快照口径会出现草稿生效但不入切换器的不一致）；正式对话页按发布快照口径（userconfig，发布后生效）
```

```gherkin
@AP-S157 @auto:e2e
Scenario: 绑定不开协作的运行时语义与派发名册（D53）
  Given 应用绑定团队智能体且 executionSettings.subAgent.enabled=false（保存 200，AP-S150）
  When 对话（正式/调试）
  Then 系统提示词含「[团队智能体：{名称}]」主提示词块，并注明未开启协作、无子智能体可派发、禁止虚构委派
  And list_tools 不披露任何 subagent_ 前缀派发工具（协作关闭=仅主智能体生效）
  And X-Moai-Team-Agent-Id 切换与 last_team_agent_id 落库同 D52 口径（不依赖协作开关）
  When 开启协作重新保存并对话
  Then 系统提示词追加「[可派发子智能体]」名册（名称/工具名 subagent_{key}/职责+派发说明），具名派发工具注册（list_tools 可见）
```

```gherkin
@AP-S158 @auto:e2e
Scenario: 子智能体过程实时事件 moai.subagent（D53）
  Given 开启子代理协作且绑定含子智能体的团队智能体
  When 主代理派发任一子智能体（SSE 对话轮）
  Then 流内出现 name=moai.subagent 的 CustomEvent（媒体类型 vnd.moai.subagent+json），序列含 started →（text / tool_call / tool_result 若干）→ completed
  And started 携带 traceId/agentKey/agentName；text 为子会话正文增量（2 万字符转发预算内）；tool_call 的 toolName 已解析 call_tool 内层真实名；tool_result 携带截断结果摘要；completed 携带最终结论（截 2000 字）与耗时
  And 事件由派发器合并循环注入同一 SSE（窗口期事件先于内层块冲刷，顺序保持）；无派发轮不产生该事件
  And 子任务超时以 timeout 终态收敛（带部分结果），执行异常以 failed 终态收敛
  And 非流式管线（ACP/A2A/IM 渠道）不产生该事件（通道未登记，发布静默丢弃）
```

```gherkin
@AP-S159 @auto:vitest
Scenario: 子智能体嵌套活动卡与结论级回放（D53）
  Then 派发卡（subagent_*/spawn_subagent）渲染 SubAgentTraceCard：头像/名称/子智能体徽标/状态（执行中/已完成/超时/失败）/耗时
  And 运行中默认展开：任务（args.task）+ 嵌套工具步骤（名称+结果摘要）+ 流式正文；完成折叠为一行结论摘要可展开回看
  And 审批模式下派发卡挂起时保留批准/拒绝操作
  And 历史回放无实时 trace（事件不持久化）：role=tool 行按 toolCallId 回填结果，buildHistoryTrace 合成 trace 仅任务+最终结论折叠展示；普通工具卡渲染不变
  And 配置页团队智能体绑定区独立分区固定渲染（协作关闭仍展示、外部应用隐藏），子代理协作区仅开关+超时+结果上限（D53 布局，vitest 覆盖绑定不清空与协作关闭保存）
```


```gherkin
@AP-S160 @auto:e2e
Scenario: 应用设置候选池=应用绑定∪市场公开∪本人个人（不含团队全量）
  Given 应用配置已绑定技能目录与可选专家（app_agent_config.skills/prompts），市场上另有公开技能与提示词，团队成员自有个人技能与提示词
  When 用户打开对话端「应用设置」面板（userconfig 查询）
  Then 专家候选=绑定专家 ∪ 市场公开提示词 ∪ 本人个人提示词；技能候选=绑定技能 ∪ 市场公开技能（未禁用）∪ 本人个人技能（非系统内置）
  And 团队技能/团队提示词不因团队成员身份自动进入候选（未绑定的团队资源不在池内，接口不按团队维度读全量）
  And 池排序：绑定（配置顺序）→ 市场公开（useCount 倒序）→ 个人（useCount 倒序），按 id 去重；来源标签 市场/团队/个人
  And 无用户配置行时勾选=绑定技能全集；有配置行时勾选=用户选择 ∩ 候选池；市场/个人候选默认不勾选
  And 热门专家=池内 useCount 前 10（对话端欢迎态），前端不再单独拉团队提示词/top_used 团队口径
```

```gherkin
@AP-S161 @auto:e2e
Scenario: 管理员绑定可选专家（agent-config prompts）
  When 团队 Admin+ 保存 agent-config 携带 prompts（int 数组，prompt.id）
  Then 保存校验：须为市场公开或本团队提示词（他人个人提示词 400「包含不存在或无权使用的提示词」），去重；null/缺省保持原值（旧前端兼容）
  And agent-config 回显 prompts；配置页「提示词」分组提供可选专家多选（候选=本团队 ∪ 市场公开，团队/市场来源后缀，按 title 过滤）
  And 用户侧 userconfig 的 promptOptions 即含绑定专家（与技能目录同口径随配置行下发，发布前后均按草稿行回显，弱依赖不阻断）
```

```gherkin
@AP-S162 @auto:e2e
Scenario: 用户侧专家/技能选择与运行时校验同口径收敛
  When 用户保存 userconfig（promptId/skills）或创建会话绑定专家、会话切换专家
  Then 可用口径=应用绑定 ∪ 市场公开 ∪ 本人个人：绑定/市场/个人均放行，未绑定团队提示词 404、未绑定团队技能 400、他人个人提示词 404、他人个人技能 400
  And 技能校验容忍存量：绑定 id 按配置 JSON 原样放行（已删除/停用由读取与运行时静默收敛）
  And 运行时技能装配（AppAgentFactory）=用户勾选 ∩（绑定 ∪ 市场公开 ∪ 本人个人，排除禁用），绑定在前保持配置顺序、其余按勾选顺序追加；调试会话仍按绑定全集（应用默认视角）
  And 会话绑定专家走同一口径（SessionPromptHelper，个人/市场/绑定放行），选用专家 +1 useCount 不变
```
