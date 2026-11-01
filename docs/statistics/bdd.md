# 统计模块 BDD（资源使用统计）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 证据：[local-dev/statistics-usage-e2e.mjs](../../local-dev/statistics-usage-e2e.mjs)

Feature: 资源使用统计统一收集与落库
  各资源（插件/应用/技能/知识库/AI 模型用量）调用时把增量写入 Redis 缓冲，
  统计模块每分钟批量取出，交给各资源写入器写自身表，并同步累加统一日表与总量表；
  管理员可查询统一统计。

  @ST-S1 @auto:e2e
  Scenario: 统计查询仅管理员可用
    Given 一个普通登录用户与一个未登录访客
    When 分别访问统一统计的日表/总量表/类型名清单查询
    Then 普通用户一律 403，未登录一律 401
    And 管理员访问全部 200

  @ST-S2 @auto:e2e
  Scenario: 业务动作触发计数进入缓冲
    When 创建一个应用并连续创建 3 个正式会话
    Then 每次会话创建都向 app-usage 计数累加 1（Redis 缓冲，无直接落库）
    And 装载技能等其它资源动作同理写入各自 name 的计数

  @ST-S3 @auto:e2e
  Scenario: 定期批量落库到统一统计表与资源表
    Given 计数缓冲中已有该应用的增量
    When 批量落库周期到达（每分钟）
    Then 统一日表出现 (今天, app-usage, 该应用 id) 记录且 count≥3
    And 统一总量表出现该应用记录且 total≥3
    And 类型名清单包含 app-usage
    And 应用列表的 useCount（资源自身表）同步 ≥3

  @ST-S4 @auto:e2e
  Scenario: 统计查询支持过滤与分页
    When 管理员按 name=app-usage 与今天日期区间查询日表
    Then 命中该应用记录且分页字段（pageNo/pageSize/total）正确回显
    When 按不存在的日期区间查询
    Then 返回空集合且 total=0
    When 查询类型名清单
    Then 返回去重升序的资源类型名集合

  @ST-S5 @auto:e2e
  Scenario: 查询参数校验
    When 管理员以 startDate 晚于 endDate 查询日表
    Then 返回 400

  @ST-S6 @manual
  Scenario: 落库失败不丢计数
    Given 某资源写入器或统一表落库抛出异常
    When 批量任务处理该资源类型失败
    Then 该批增量被原样回补 Redis，下个周期重新取出重试
    And 计数不丢失（至多因部分成功而轻微重复累加）

  @ST-S7 @auto:e2e
  Scenario: 六类资源写入器各自落库
    Then 插件增量写 plugin.counter，应用增量写 app.use_count，技能增量写 skills.use_count，
    And 知识库增量写 wikis.counter，AI 模型用量增量按维度 upsert ai_model_token_audit，
    And 提示词增量写 prompts.use_count
    And 全部同步累加统一日表/总量表

  @ST-S8 @auto:e2e
  Scenario: 提示词专家被选用时计入统计
    When 会话创建或切换时选用了某专家提示词
    Then 该专家的 prompt-usage 增量进入缓冲并批量落库
    And 日表出现该提示词记录且 count≥1

  @ST-S9 @manual
  Scenario: 一轮对话多次工具调用只推送一次统计
    When 一轮对话内模型连续调用多个（或同一个）工具/技能/知识库检索
    Then 各资源的使用量在内存按资源类型聚合
    And 对话作用域结束时每个资源类型只向 Redis 批量推送一次（而非逐次推送）

  @ST-S10 @auto:e2e
  Scenario: 统计粒度由管理员配置
    When 管理员把统计粒度设置为 hour
    Then 之后的落库写入 (当天, 当前 UTC 小时) 桶，日表记录携带 usageHour
    When 设置为非法值（如 week）
    Then 保存被拒绝返回 400
    When 设置回 day
    Then 之后的落库回到整日一个桶（usageHour=-1）

  @ST-S11 @manual
  Scenario: 领域服务支持增与减
    When 业务模块调用领域服务 Record/Remove
    Then Redis 计数原子增/减，落库按正负增量累加

  @ST-S12 @auto:e2e
  Scenario: 趋势分析接口按日聚合
    When 管理员触发一次插件调用并等待批量落库
    Then 趋势接口按日返回序列（缺省近 30 天，无数据日期补 0）
    And 当日聚合数包含该次调用
    And 不带资源类型名时统计全部类型合计
    And 起始日期晚于结束日期返回 400

