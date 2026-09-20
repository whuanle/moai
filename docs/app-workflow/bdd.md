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
  When 以启动参数 {"question":"yes"} 调试执行
  Then 实例终态为 completed，最终输出 answer 为「命中:sum-yes」
  And 命中分支与上游节点均为 completed，未命中分支为 skipped

@WF-S7 @auto:e2e
Scenario: 调试执行未命中分支
  When 以启动参数 {"question":"no"} 调试执行
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

@WF-S10 @auto:e2e
Scenario: 系统设置（对话开场白与全局变量，草稿/发布双轨）
  Given 团队管理员在设计器「系统设置」面板中维护应用级配置
  When 保存对话开场白（开关 + 文案，经 agent-config 接口，流程应用只写开场白字段）
  Then 保存成功且重新查询回读一致（草稿值），团队成员（Member）保存时返回 403
  And 开场白保存不触碰流程草稿定义与版本，但配置状态转为「有未发布变更」
  Given 应用已发布
  Then 应用详情仍按发布快照下发开场白（发布时未配置则为空），线上对话不受草稿影响
  When 管理员重新发布流程
  Then 开场白随编排定义一并进入发布快照：详情下发新开场白且配置状态回「已发布一致」

@WF-S11 @auto:e2e
Scenario: 知识库检索节点引用本团队知识库
  Given 团队管理员在流程中配置知识库检索节点
  When 节点引用了其他团队或不存在的知识库并保存草稿
  Then 返回 400 且错误信息指明无效的知识库 id
  When 节点仅引用本团队知识库保存草稿并发布
  Then 保存与发布成功

@WF-S12 @auto:e2e
Scenario: 知识库检索节点输出结构化检索结果
  Given 知识库检索节点从上游绑定检索问题，声明 query/count/hits/contents/text 输出
  When 调试执行到该节点（知识库未配置向量化模型时静默跳过）
  Then 节点执行完成，输出包含检索问题、命中数量 0、命中列表、内容列表与拼接文本（空命中为空数组/空串）
  And 下游节点可引用这些输出字段

@WF-S13 @auto:e2e
Scenario: 知识库检索节点未配置知识库时失败
  When 知识库检索节点在未选择知识库（空 wikiIds）的情况下执行
  Then 节点失败、实例挂起，错误信息指明缺少知识库配置

@WF-S14 @auto:e2e
Scenario: 知识库检索节点通过变量动态绑定知识库
  Given 知识库检索节点只绑定一个知识库：可在同一下拉中选择静态知识库，或绑定上游变量（如启动参数）
  When 以包含知识库 id 的启动参数调试执行（节点未静态配置知识库）
  Then 节点在运行时解析变量指定的知识库并完成检索，输出结构完整
  When 变量指定的知识库不属于本团队
  Then 运行时团队过滤兜底，该知识库被静默忽略（不越权）
  When 未提供变量且无静态配置
  Then 节点失败、实例挂起，错误信息指明缺少知识库配置

@WF-S15 @manual
Scenario: 画布右键删除节点与连线
  Given 团队管理员在流程设计器画布中
  When 右键点击任意节点
  Then 弹出右键菜单，选择「删除节点」后该节点及其连线被移除，可撤销恢复
  When 右键点击任意连线
  Then 弹出右键菜单，选择「删除连线」后仅该连线被移除，可撤销恢复

@WF-S16 @auto:e2e
Scenario: 普通节点仅允许一条输出连线且拖线即切换
  Given 流程设计器中条件/多条件节点承担分支语义
  When 普通节点（开始/AI 对话/脚本/插件/知识库检索）拖出第二条输出连线
  Then 旧连线被新连线替换（拖线即切换下游），保存时若存在多条输出连线则校验拒绝
  When 保存或发布含多条输出连线的普通节点定义
  Then 返回校验错误，提示只允许一条输出连线

@WF-S17 @manual
Scenario: 添加节点面板团队工具直接生成插件节点
  Given 团队管理员打开「添加节点」面板，面板含「节点类型」与「团队工具」两个 Tab
  When 切换到「团队工具」Tab
  Then 展示团队可用插件工具列表
  When 拖拽某个工具到画布
  Then 直接生成插件节点，且已绑定该插件，并按请求参数 schema 预填输入参数、按响应 schema 预填输出参数
  And 插件节点表单内也可重新选择团队工具，更换后输入/输出参数自动更新
  But 插件节点的输入/输出字段由插件 schema 自动生成，不可手动增加或删除字段（仅可设置输入的取值方式）

@WF-S18 @auto:e2e
Scenario: 问题分类节点配置校验
  Given 团队管理员在流程中配置问题分类节点（问题类型列表 + 用户问题绑定，每个分类端口连出出边）
  When 分类列表为空时发布流程
  Then 返回 400 且错误信息指明至少需要配置一个分类
  When 出边分类标记不指向已配置的分类时发布流程
  Then 返回 400 且错误信息指明分类标记无效
  When 补全分类配置后发布流程
  Then 保存与发布成功

@WF-S19 @auto:e2e
Scenario: 问题分类节点缺少 AI 模型时确定性失败
  Given 已发布流程中问题分类节点未配置 AI 模型
  When 调试执行
  Then 分类节点失败、实例挂起，错误信息指明未配置 AI 模型
  And 下游分类分支节点均未执行

@WF-S20 @auto:vitest
Scenario: 问题分类节点按模型输出路由分支
  Given 问题分类节点配置两个分类（AI 调用以测试桩替代）
  When 模型输出命中分类的序号数字
  Then 节点输出命中的分类 id 与分类名，对应分支执行、另一分支跳过
  When 模型输出包含分类名称但无序号
  Then 按分类名匹配命中
  When 模型输出无法解析出任何分类
  Then 默认命中第一个分类（流程继续）
  And 分类提示词包含类型列表序号、背景知识，历史消息按配置条数截取最近消息
  But 缺少用户问题或缺少分类配置时节点直接失败且不调用模型

@WF-S21 @manual
Scenario: 问题分类节点设计器表单
  Given 团队管理员从「AI 能力」分组拖入问题分类节点
  When 展开节点表单
  Then 可选择团队可用 AI 模型、填写背景知识、设置聊天记录条数、绑定用户问题与可选历史消息变量
  When 添加或删除问题类型
  Then 每个类型行带独立出边端口，从端口可拖线连出分支，保存后分类与连线往返一致

@WF-S22 @auto:e2e
Scenario: HTTP 请求节点发起 GET 请求并提取响应字段
  Given 团队管理员在流程中配置 HTTP 请求节点（GET + 查询参数 + 输出字段提取）
  When 调试执行到该节点（请求指向测试桩服务）
  Then 请求携带配置的查询参数（支持插值引用上游变量）与请求头
  And 节点输出包含状态码、按 JsonPath 提取的字段与原始响应，下游节点可引用提取结果

@WF-S23 @auto:e2e
Scenario: HTTP 请求节点发起 POST 请求
  Given HTTP 请求节点配置 POST + JSON 请求体（支持插值引用上游变量）
  When 调试执行
  Then 测试桩收到 JSON 请求体与 application/json Content-Type，节点执行完成

@WF-S24 @auto:e2e
Scenario: HTTP 请求失败默认中断流程
  When 上游返回非 2xx 状态码、请求超时或网络错误且未开启报错捕获
  Then 节点失败、实例挂起，错误信息包含状态码或超时提示

@WF-S25 @auto:e2e
Scenario: 开启报错捕获后失败不中断流程
  Given HTTP 请求节点开启报错捕获
  When 上游返回非 2xx 状态码
  Then 节点执行完成，输出 hasError=true、错误信息与状态码，下游节点可引用 hasError 分流

@WF-S26 @auto:e2e
Scenario: HTTP 请求节点配置校验
  When 保存或发布缺少请求地址、非法方法、非法超时、非法 Body 类型、重复提取变量名或非法 JsonPath 的定义
  Then 返回 400 且错误信息指明对应配置项
  And 配置中 {引用} 插值引用了非上游节点时同样被拒绝

@WF-S27 @manual
Scenario: 设计器 HTTP 节点表单与 cURL 导入
  Given 团队管理员拖入 HTTP 请求节点（节点库「集成」分组）
  When 在节点卡内配置方法/地址/超时/Params/Body/Headers/鉴权/报错捕获/输出字段提取
  Then 配置随画布保存并在重新加载后回显
  When 粘贴常见 cURL 命令执行「cURL 导入」
  Then 方法/地址/请求头/请求体/鉴权（Bearer/Basic）被解析填充
  But 无法解析（缺少地址）时提示错误且不覆盖现有配置

@WF-S28 @auto:e2e
Scenario: 发布的流程应用支持会话对话
  Given 已发布的流程应用
  When 团队成员创建对话会话并发起对话
  Then 创建成功返回会话 id，可经应用对话端点（AG-UI）发送用户消息并收到流式回复
  When 非团队成员创建会话
  Then 返回 404

@WF-S29 @auto:e2e
Scenario: 一轮对话驱动一次已发布流程执行
  Given 会话归属用户在对话端点发送用户消息
  When 服务端以该消息作为启动参数 question 驱动当前已发布流程执行（旧编排引用的 query 镜像同值）
  Then 流程完成后以结束节点输出中的 reply 字段（缺失时取唯一字符串字段，再退化为整体 JSON）作为本轮 AI 回复返回
  When 流程执行失败（如节点失败挂起）
  Then 错误信息以可见文本返回，不静默中断

@WF-S30 @auto:e2e
Scenario: 对话上下文注入 sys 系统变量
  Given 流程节点通过 sys.* 引用系统变量（使用者/应用/对话/回复/时间/历史）
  When 首轮对话执行
  Then sys.userId/sys.appId/sys.conversationId/sys.messageId/sys.currentTime 均注入真实值且 sys.history 为空数组
  And sys.conversationId 等于会话 id，sys.messageId 为本轮回复的唯一标识

@WF-S31 @auto:e2e
Scenario: 历史记录变量跨轮累积并落库
  Given 同一会话已完成第一轮对话
  When 第二轮对话执行
  Then sys.history 包含第一轮 user 与 assistant 两条消息
  And 会话消息按 user/assistant 交替持久化，可在会话消息查询中回放

@WF-S32 @auto:e2e
Scenario: 对话实例计入运行历史
  When 流程应用经对话产生执行
  Then 每轮对话生成一条非调试实例并出现在运行历史列表

@WF-S33 @manual
Scenario: 设计器展示系统变量
  Given 团队管理员打开设计器「系统设置」面板或节点输入绑定编辑器
  When 查看系统变量分区或变量提示
  Then 只读列出使用者 ID/应用 ID/当前对话 ID/AI 回复的 ID/历史记录/当前时间及其 sys.* 引用名
  And 绑定与插值的变量提示包含这些 sys.* 引用（调试运行时对话维度为空值）

@WF-S34 @auto:e2e
Scenario: 开始节点固定接收用户问题 question
  Given 流程定义的开始节点固定声明唯一启动参数 question（不可再自定义输入/输出字段）
  When 会话用户发起一轮对话
  Then 用户消息文本注入开始节点 question 字段供下游引用
  And 旧发布流程中引用的 start.query 镜像同值，旧编排不改也能继续对话

@WF-S35 @auto:e2e
Scenario: 流程对话历史经压缩组件约束有界
  Given 同一流程应用会话连续完成多轮（≥10 轮）对话
  When 服务端组装 sys.history
  Then 历史按应用执行设置的压缩策略（滑动窗口保留近 N 轮）约束条数，不再随轮次线性增长
  And 压缩组件异常时退回最近 20 条原文，对话不中断

@WF-S36 @manual
Scenario: 流程应用全屏外壳与头部 Tab 导航
  Given 团队管理员打开流程应用工作台
  Then 设计/调试/配置/运行历史共用同一全屏外壳（同款头部：返回 + 应用名/状态 + 居中 设计·调试·配置 切换，无面包屑/页头）
  When 普通成员进入
  Then 仅可见 调试 / 配置（信息）/ 运行历史

@WF-S37 @manual
Scenario: 流程应用配置分区与内部应用一致
  Given 打开流程应用「配置」Tab
  Then 左侧菜单提供 信息 / 日志 / 监控 / 外部渠道
  And 信息页可修改流程应用名称、描述与头像，日志/监控/外部渠道复用内部应用同款能力

@WF-S38 @manual
Scenario: 调试分区会话式对话
  Given 打开流程应用「调试」Tab
  Then 左侧列出本人历史会话（可新建/删除/切换），右侧为流式对话区并展示开场白
  When 发送用户消息
  Then 一轮消息驱动一次已发布流程执行并流式返回回复，消息持久化为会话历史
  And 流程未发布时给出发布前置提示

@WF-S39 @manual
Scenario: 设计器开始节点固定 question 展示
  Given 团队管理员打开设计器查看开始节点
  Then 节点卡内只读展示固定输出 question（string，必填，用户的问题），不提供输入/输出字段自定义编辑
  And 调试运行面板的启动参数模板固定为 {"question": ""}，不从画布推导
  And 旧草稿画布加载时自定义参数收敛为 question，下游对 start.query 的引用（绑定/插值/脚本）自动改写为 start.question

@WF-S40 @auto:e2e
Scenario: 调试分区按最新草稿免发布执行
  Given 流程应用尚未发布但已保存草稿
  When 团队管理员在「调试」分区发起对话（携带草稿执行标记）
  Then 按最新草稿定义执行并正常返回回复，实例在运行历史中记为「调试」类型
  And 正式对话（应用对话页，不携带标记）在未发布时仍被拒绝
  When 普通成员携带草稿执行标记发起对话
  Then 被拒绝：仅团队管理员可按最新草稿调试

@WF-S41 @auto:e2e
Scenario: 调试执行兼容旧编排 query 镜像
  Given 历史草稿的开始节点声明旧参数 query 且下游绑定 start.query
  When 团队管理员仅按固定契约传 question 调试执行
  Then 启动参数补 query 镜像（与对话链路一致），旧编排不改也能调试跑通

@WF-S42 @auto:vitest
Scenario: 流程应用正式对话页裁剪 Agent 专属能力
  Given 已发布流程应用的团队成员打开应用对话页
  Then 会话列表、开场白、快捷输入与附件能力与 Agent 应用对话一致
  And 不展示专家选择（当前专家/热门专家）、技能勾选、工具审批模式与应用设置入口
  And 不发起专家提示词与用户级应用配置请求（流程对话不装配这些能力）

@WF-S43 @auto:vitest
Scenario: 流程应用正式对话发送
  Given 已发布流程应用的团队成员在应用对话页发送消息
  When 首轮消息创建会话
  Then 会话不绑定专家提示词，消息经对话端点驱动已发布流程执行并流式回显回复

@WF-S44 @auto:vitest
Scenario: 应用广场开放流程应用对话入口
  Given 平台内存在公开且已发布的流程应用
  When 任意登录用户浏览应用广场
  Then 该应用卡片展示「进入对话」，进入后对话页按流程应用形态展示（无专家/技能设置）

@WF-S45 @manual
Scenario: 设计器结束节点输出收集编辑
  Given 团队管理员打开结束节点的节点卡
  Then 可增删改「输出收集」绑定行（字段名/取值方式/引用上游变量/必填），作为流程最终输出字段
  And 删除节点后残留在结束节点上的引用可在此删除

@WF-S46 @auto:vitest
Scenario: 校验错误指明问题所在节点
  When 画布中存在引用已删除节点的输入绑定（如结束节点残留 fallback 引用）
  Then 保存前校验报错文案包含所在节点名称与输入字段名（如 节点「结束」的输入 fallback 引用了不存在的节点），用户可定位修复

@WF-S47 @manual
Scenario: 设计器 AI 对话节点配置模型与提示词
  Given 团队管理员打开 AI 对话节点的节点卡
  Then 可从团队网关可用模型中选择「AI 模型」（空态提示先配置渠道）
  And 可填写「系统提示词」（设定角色与回答风格）与「温度」（0-2，留空用渠道默认）
  And 未选择模型时保存被校验拦截并指明节点名

@WF-S48 @auto:vitest
Scenario: AI 对话节点执行契约
  Given AI 对话节点配置了 aiModelId（旧定义的 config.model 亦兼容）与 systemPrompt、temperature
  When 节点执行
  Then 按所选模型发起对话，系统提示词与温度随请求下发；输入绑定 system/model 非空时覆盖配置；温度越界按未设置处理

@WF-S49 @manual
Scenario: AI 对话节点从专家提示词填入系统提示词
  Given 团队管理员打开 AI 对话节点的节点卡
  When 在「专家提示词」下拉中选择个人或团队提示词
  Then 提示词内容填入系统提示词文本域，可继续编辑后保存

@WF-S50 @auto:e2e
Scenario: AI 对话节点引入技能并开启沙箱
  Given AI 对话节点配置了模型并勾选技能、开启沙箱
  When 流程执行到该节点
  Then 节点按 Agent 工具链执行：技能以 skill_* 工具、沙箱以代码执行等工具暴露给模型（渐进式披露），多轮工具调用后返回最终回答
  And 未引入技能且未开启沙箱时保持直连模型的快路径

@WF-S51 @manual
Scenario: 设计器引入 Agent 应用节点
  Given 团队管理员在节点库「AI 能力」分组拖入 Agent 应用节点
  Then 可从本团队已发布 Agent 应用中选择（会与当前流程构成循环嵌套的应用标记并禁选）
  And prompt 输入为发给 Agent 的用户消息，输出 answer 为 Agent 回答；未选择应用时保存被校验拦截

@WF-S52 @auto:e2e
Scenario: Agent 应用节点执行与循环嵌套防护
  Given 流程的 agentApp 节点选择了已发布 Agent 应用
  When 调试执行到该节点
  Then 按该 Agent 的发布快照驱动一轮对话（含其插件/技能/知识库/流程工具），回复进入流程输出
  When 所选 Agent（直接或经流程工具闭包）引用了当前流程
  Then 可选项标记为循环、保存草稿与调试执行被 400 拒绝并说明链路，运行期同样拦截

@WF-S53 @auto:e2e
Scenario: 开始/结束节点必须存在（核心节点保护与空白画布自愈）
  Given 草稿画布缺失开始节点、结束节点、含重复开始节点，或画布快照为空（历史调用只写定义）
  When 设计器加载该草稿
  Then 画布自动补齐缺失的开始/结束节点并保持开始节点唯一（空快照优先用引擎定义重建画布，无定义则回退默认 start→end 编排）
  And 开始/结束节点不响应画布右键删除
  When 通过接口保存无开始节点、开始节点重复或无结束节点的流程定义
  Then 保存被 400 拒绝并提示原因
