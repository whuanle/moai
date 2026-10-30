> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 上游：[../app/sdd.md](../app/sdd.md)、[../ai/sdd.md](../ai/sdd.md) ｜ 证据：[local-dev/agent-memory-e2e.mjs](../../local-dev/agent-memory-e2e.mjs)、[local-dev/memory-management-e2e.mjs](../../local-dev/memory-management-e2e.mjs)

# Agent 应用长期记忆（BDD）

Feature: 应用记忆配置与门禁（2026-10-30 起模型/维度统一在系统设置，应用配置不接收）
  @MEM-S1
  Scenario: 平台未开启长期记忆时应用不可开启记忆
    Given 系统设置 MEMORY_ENABLED=false（或未选统一向量化模型）
    When 管理员保存执行参数且 memory.enabled=true
    Then 保存被拒绝并提示先在系统设置中开启长期记忆
    And 平台开启后同一保存成功；用户记忆偏好调整在平台未开启时同样被拒
  @MEM-S2
  Scenario: 记忆配置保存与回显不携带模型字段
    When 管理员保存合法记忆配置（自动提取/检索参数/允许全局接入）
    Then 保存成功且应用配置详情回显 memory 节
    And 请求携带 embeddingModelId/embeddingDimensions 时被剥离，回显不含这两个字段
  @MEM-S3
  Scenario: 记忆接口团队门禁
    Given 非应用所属团队成员
    When 查询本人的该应用记忆
    Then 返回 404
  @MEM-S4
  Scenario: 清空应用全部记忆仅团队管理员
    When Member 调用清空应用记忆接口
    Then 返回 403

Feature: 记忆写入（工具显式 + 自动提取）
  @MEM-S5
  Scenario: AI 通过工具显式保存记忆
    When 用户在对话中要求记住一条信息且应用开启记忆
    Then AI 调用 memory_save 写入成功并可在本人的记忆列表中查到
  @MEM-S6
  Scenario: 对话结束后自动提取并合并记忆
    Given 应用开启自动提取且用户未关闭记忆
    When 一轮对话落库完成
    Then 后台异步抽取重要事实并与既有记忆合并决策后写入
  @MEM-S7
  Scenario: 记忆按用户隔离
    Given 用户 A 在应用内已有记忆
    When 用户 B 查询本人记忆或对话触发检索
    Then B 不可见 A 的任何记忆

Feature: 记忆激活与用户控制
  @MEM-S8
  Scenario: 相关记忆按需注入对话上下文
    Given 用户存在与当前问题相关的记忆
    When 用户发起新一轮对话
    Then 相关记忆被自动注入模型上下文
  @MEM-S9
  Scenario: 用户关闭记忆后不读写
    When 用户将本人记忆模式设为关闭
    Then 对话不再注入记忆且不注册记忆工具，已有数据保留
  @MEM-S10
  Scenario: 用户记忆模式仅开启与关闭两态
    Given 应用开启长期记忆
    When 用户在对话设置面板选择开启或关闭
    Then 未设置或历史遗留值一律按开启生效，仅有关闭会停用本人记忆
  @MEM-S11
  Scenario: 记忆管理页维护本人记忆
    When 用户从对话页设置面板进入「我的记忆」页
    Then 可分页查看本人记忆并删除单条或清空本应用，不能编辑修改
  @MEM-S12
  Scenario: 对话设置面板不展示记忆明细
    When 用户打开对话页应用设置面板
    Then 记忆区仅提供 开启/关闭 与「管理记忆」入口，不展示记忆列表

Feature: 记忆管理（删除与清空）
  @MEM-S13
  Scenario: 用户删除本人单条记忆
    When 用户删除本人的一条记忆
    Then 删除成功；重复删除同一 id 返回不存在
  @MEM-S14
  Scenario: 用户清空本应用记忆
    When 用户清空本人在该应用的全部记忆
    Then 本人记忆列表清空
  @MEM-S15
  Scenario: 管理员清空应用全部用户记忆
    When 团队管理员清空应用记忆
    Then 应用内全部用户的记忆被删除

Feature: 应用记忆统计
  @MEM-S17
  Scenario: 应用配置区展示记忆条数统计
    Given 团队管理员展开应用「配置」分区的「长期记忆」收缩卡片
    When 应用内已有用户记忆
    Then 展示全部用户的记忆总条数与用户数，清空后归零

Feature: 统一向量化模型锁定（2026-10-30 起锁定上移到平台设置层：任一记忆存在即锁，须清空全部记忆）
  @MEM-S18
  Scenario: 平台已有记忆时更换统一向量化模型被拒绝
    Given 平台已配置统一模型且存在任意记忆（应用记忆或全局记忆）
    When 超级管理员更换系统设置中的统一向量化模型
    Then 保存被拒绝并提示先清空全部记忆
  @MEM-S19
  Scenario: 平台已有记忆时更换向量维度被拒绝
    Given 平台已配置统一模型且存在任意记忆
    When 超级管理员更换系统设置中的统一向量维度
    Then 保存被拒绝并提示先清空全部记忆
  @MEM-S20
  Scenario: 应用关闭再开启不受平台锁定影响
    Given 应用内已存在用户记忆
    When 管理员关闭应用长期记忆后再重新开启
    Then 保存均成功（应用配置不携带模型字段，口径随平台设置）；关闭态草稿回显不含模型/维度
  @MEM-S21
  Scenario: 清空全部记忆后平台模型解锁
    Given 超级管理员已通过「清空全部记忆」清空所有应用记忆与全局记忆
    When 超级管理员更换统一向量化模型与维度
    Then 保存成功，应用与全局新记忆按新口径写入并可检索

Feature: 记忆提示词（追加为记忆工具描述，指导 AI 如何管理与使用记忆）
  @MEM-S22 @auto:vitest
  Scenario: 提示词留空用平台默认并回显前端
    Given 管理员未配置 memory.prompt
    When 打开「配置」分区的「长期记忆」卡片
    Then 平台默认提示词自动展示为输入框占位符，保存时不携带 prompt 字段
    And 记忆工具描述按平台默认提示词追加「记忆管理约定」，AI 据此了解如何调用 memory_search/memory_save/memory_forget
  @MEM-S23 @auto:vitest
  Scenario: 自定义提示词保存回显并替换默认生效
    When 管理员填写自定义提示词（不超过 2000 字）并随「保存设置」提交
    Then 提示词随 memory 节落库并回显，记忆工具描述改用自定义内容
    When 保存超过 2000 字的提示词
    Then 请求被拒绝并提示长度超限

Feature: 记忆管理页（个人跨应用视图，2026-10-30）
  @MEM-S24 @auto:e2e
  Scenario: 记忆管理页总览
    When 用户打开侧边栏「记忆管理」页
    Then 可见自己已有记忆的应用列表（应用/所属团队/记忆条数/最近活跃）与个人全局记忆概况
    And 平台未开启全局记忆时全局页签展示引导提示且不可新建
  @MEM-S25 @auto:e2e
  Scenario: 应用记忆维护
    When 用户在记忆管理页进入某应用的记忆列表
    Then 可编辑单条记忆（保留原记忆标识与创建时间，内容重嵌向量）、删除单条、清空本应用
    And 可把一条应用记忆存为全局记忆
  @MEM-S26 @auto:e2e
  Scenario: 应用记忆维护门禁
    Given 非应用所属团队成员
    When 编辑该应用记忆或把该应用记忆存为全局
    Then 返回 404
    When 编辑不存在的记忆或提交空内容
    Then 返回 404 或 400

Feature: 个人全局记忆（跨应用，2026-10-30；平台设置已并入统一口径 D-M10）
  @MEM-S27 @auto:e2e
  Scenario: 平台开启长期记忆（统一设置门禁）
    Given 超级管理员在系统设置中配置长期记忆（MEMORY_*）
    When 未选择向量化模型直接开启、填写非法开关值/模型/维度或非管理员保存
    Then 保存被拒绝
    When 选择公开启用中的向量化模型与维度后开启总开关
    Then 应用记忆与全局记忆能力对全部用户可用
  @MEM-S28 @auto:e2e
  Scenario: 全局记忆维护与用户隔离
    Given 平台已开启全局记忆
    When 用户在记忆管理页维护个人全局记忆
    Then 可新建、编辑（保留原标识）、删除单条与清空本人全部
    And 全局记忆按用户隔离，其他用户不可见
  @MEM-S29 @auto:e2e
  Scenario: 应用记忆存为全局
    When 用户把一条应用记忆存为全局
    Then 全局记忆列表出现该内容，原应用记忆保留
    And 重复存入高相似内容自动合并不增条数
  @MEM-S30 @auto:e2e
  Scenario: 全局记忆跨应用注入
    Given 用户已有与当前问题相关的全局记忆且应用开启长期记忆
    When 用户在任一应用发起新一轮对话
    Then 全局记忆与本应用记忆按相似度归并注入，全局条目带「全局」标记
    And 注入发生后模型仍能看到用户问题与可用工具，其他用户的全局记忆不可见
  @MEM-S31 @auto:e2e
  Scenario: 统一向量化模型锁定（应用或全局任一记忆存在即锁）
    Given 平台已配置统一模型且已有任意记忆
    When 超级管理员更换统一向量化模型或维度
    Then 保存被拒绝并提示先清空全部记忆
  @MEM-S32 @auto:e2e
  Scenario: 超级管理员清空平台全部记忆
    When 非超级管理员清空平台全部记忆
    Then 返回 403
    When 超级管理员清空平台全部记忆
    Then 所有应用记忆与全局记忆一并清空，统一向量化模型解锁，新记忆按新模型写入

Feature: 全局记忆三级开关（平台 → 管理员 → 用户，2026-10-30）
  @MEM-S33 @auto:e2e
  Scenario: 管理员允许用户接入全局记忆
    Given 平台已开启全局记忆且应用开启长期记忆
    When 管理员保存记忆配置开启「允许用户接入全局记忆」并发布
    Then 保存成功且用户配置回显允许接入；未开启记忆的应用该开关不可设置
    And 该开关不受向量化模型锁定约束，可随保存自由切换
  @MEM-S34 @auto:e2e
  Scenario: 管理员未允许时用户记忆仅在本应用内
    Given 平台已开启全局记忆但应用未允许接入全局记忆
    When 用户在本应用对话且已有相关全局记忆
    Then 不注入也不检索全局记忆，用户配置不展示全局接入开关
  @MEM-S35 @auto:e2e
  Scenario: 用户接入全局记忆开关
    Given 应用开启记忆且允许接入全局记忆
    When 用户在对话设置面板关闭「接入全局记忆」
    Then 本应用不再注入与检索全局记忆，应用内记忆不受影响；恢复接入后全局记忆重新生效
  @MEM-S36 @auto:e2e
  Scenario: 全局保存与遗忘工具
    Given 应用允许接入全局记忆且用户开启记忆与接入
    When 用户在对话中要求「全局记住」某条信息
    Then AI 通过记忆保存工具按全局范围写入个人全局记忆（应用内不落库）
    When 用户要求忘掉该条全局记忆
    Then AI 通过记忆遗忘工具删除该全局记忆（应用内未命中时按全局兜底）
    And 应用未开放全局接入时全局保存被拒绝并提示改用应用内记忆

Feature: 上下文压缩参数
  @MEM-S16
  Scenario: 应用压缩阈值约束落库视图
    Given 应用配置 toolResultTriggerMessages=2 并完成多轮工具调用
    When 会话落库为压缩后视图
    Then 保留的工具结果消息不超过触发阈值

Feature: 记忆设置统一到系统设置（2026-10-30，D-M10）
  @MEM-S37 @auto:e2e
  Scenario: 首次配置统一模型不受存量分片锁定
    Given 平台尚未配置统一向量化模型（历史存量应用分片可能仍有计数）
    When 超级管理员首次选择统一向量化模型与维度并开启总开关
    Then 保存成功（锁定仅在统一模型已配置后生效，存量分片按直切策略由管理员按需清空）
