> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 上游：[../app/sdd.md](../app/sdd.md)、[../ai/sdd.md](../ai/sdd.md) ｜ 证据：[local-dev/agent-memory-e2e.mjs](../../local-dev/agent-memory-e2e.mjs)

# Agent 应用长期记忆（BDD）

Feature: 应用记忆配置与门禁
  @MEM-S1
  Scenario: 开启记忆必须选择向量化模型
    When 管理员保存执行参数且 memory.enabled=true 未携带 embeddingModelId
    Then 保存被拒绝并提示必须选择向量化模型
  @MEM-S2
  Scenario: 记忆配置保存与回显
    When 管理员保存合法记忆配置（模型/维度/自动提取/检索参数）
    Then 保存成功且应用配置详情回显 memory 节
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

Feature: 向量化模型锁定（不同模型/维度的向量不可比，写入第一条记忆后不可更换）
  @MEM-S18
  Scenario: 已有用户记忆时更换向量化模型被拒绝
    Given 应用内已存在用户记忆
    When 管理员保存记忆配置且更换了向量化模型
    Then 保存被拒绝并提示需先清空应用全部记忆
  @MEM-S19
  Scenario: 已有用户记忆时更换向量维度被拒绝
    Given 应用内已存在用户记忆
    When 管理员保存记忆配置且修改了向量维度
    Then 保存被拒绝并提示需先清空应用全部记忆
  @MEM-S20
  Scenario: 关闭记忆不解除锁定且同模型重新开启放行
    Given 应用内已存在用户记忆且向量化模型与维度已锁定
    When 管理员关闭长期记忆后再重新开启
    Then 关闭期间配置保留生效模型与维度作为锁定参照系，期间换成其他模型仍被拒绝，选回原模型与维度可保存
  @MEM-S21
  Scenario: 清空应用全部记忆后解锁可更换
    Given 管理员已清空应用内全部用户记忆
    When 管理员更换向量化模型与维度并重新发布
    Then 新记忆按新模型与维度写入并可检索

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

Feature: 上下文压缩参数
  @MEM-S16
  Scenario: 应用压缩阈值约束落库视图
    Given 应用配置 toolResultTriggerMessages=2 并完成多轮工具调用
    When 会话落库为压缩后视图
    Then 保留的工具结果消息不超过触发阈值
