# 系统设置（Settings）行为规格（BDD，Gherkin）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md)（场景→验证映射） ｜ [SOP](./sop.md)（操作与验收流程）
> 编号规则与标签语义见 [../DOC-STANDARD.md](../DOC-STANDARD.md) 第 3 节。术语：root=超级管理员；admin=管理员（root 隐含）；member=普通用户。

```gherkin
Feature: 查询设置项
  Background:
    Given 系统内置设置项 OPEN_NEO4J（默认值 "false"）

  @SET-S1 @auto:e2e
  Scenario: 管理员查询设置项
    Given admin 已登录
    When 请求设置项列表
    Then 返回全部内置项，每项含 key/名称/描述/当前值
    And OPEN_NEO4J 的值为数据库当前值（无记录时为默认值）

  @SET-S2 @auto:e2e
  Scenario: 普通用户无权查询
    Given member 已登录
    When 请求设置项列表
    Then 返回禁止访问（403）提示只有管理员可以访问设置项

  @SET-S3 @auto:e2e
  Scenario: 未登录访问
    When 不带登录凭证请求设置项列表
    Then 返回未授权（401）

Feature: 保存设置项
  Background:
    Given root 已登录

  @SET-S4 @auto:e2e
  Scenario: root 保存设置项并回读生效
    When 保存 OPEN_NEO4J 为 "true"
    Then 操作成功
    And 再次查询时该项值为 "true"

  @SET-S5 @manual
  Scenario: root 关闭开关
    When 保存 OPEN_NEO4J 为 "false"
    Then 操作成功且回读为 "false"

  @SET-S6 @auto:e2e
  Scenario: 系统级 root key 受保护
    When 保存 key 为 "root" 的配置
    Then 返回请求错误（400）提示无效的配置项
    And 超级管理员指向未发生变化

  @SET-S7 @auto:e2e
  Scenario: 非法 key 被拒绝
    When 保存不存在的配置项 key
    Then 返回请求错误（400）提示无效的配置项

  @SET-S8 @manual
  Scenario: 首次写入自动建行
    Given 设置表中尚无该内置项记录
    When root 保存该 key
    Then 以内置定义的 key/名称/描述插入新记录且值为提交值

  @SET-S9 @manual
  Scenario: 管理员（非 root）不能保存
    Given admin 已登录但不是 root
    When 保存任一设置项
    Then 返回禁止访问（403）提示只有超级管理员可以修改设置项
    And 设置值未被修改

Feature: 前端设置页（/settings）
  @SET-S11 @manual
  Scenario: root 进入设置页
    Given root 登录并进入系统设置页
    Then 展示知识图谱开关的名称、描述
    And 开关状态与后端值一致

  @SET-S12 @manual
  Scenario: 保存按钮脏检查
    When 未修改任何值
    Then 保存按钮置灰不可点
    When 切换开关或修改连接字段
    Then 保存按钮可用

  @SET-S13 @manual
  Scenario: root 保存成功
    Given root 登录
    When 切换开关并保存
    Then 提示成功且刷新页面后状态保持

  @SET-S14 @manual
  Scenario: 保存失败回滚
    Given root 登录（后端将拒绝保存）
    When 修改开关或字段并保存
    Then 前端重新加载，恢复为数据库真实值

  @SET-S15 @manual
  Scenario: 普通用户访问设置页被重定向
    Given member 登录
    When 直接访问设置页
    Then 被重定向到仪表盘
    And 接口层同时返回 403

Feature: 知识图谱设置（Neo4j）
  Background:
    Given 系统内置设置项 OPEN_NEO4J（默认值 "false"）与 NEO4J_URI/NEO4J_USERNAME/NEO4J_PASSWORD（默认空串）

  @SET-S16 @auto:vitest
  Scenario: root 开启知识图谱并保存连接信息
    Given root 登录并进入系统设置页
    When 打开「开启 Neo4j 知识图谱」并填写连接地址、用户名、密码后保存
    Then 分别保存 OPEN_NEO4J="true" 与三项连接设置
    And 回读时开关与连接信息与提交值一致

  @SET-S17 @auto:vitest
  Scenario: 关闭知识图谱不提交连接信息
    Given root 在系统设置页
    When 保持/切回「开启 Neo4j 知识图谱」为关闭并保存
    Then 仅保存 OPEN_NEO4J="false"
    And 不提交 NEO4J_URI/NEO4J_USERNAME/NEO4J_PASSWORD

  @SET-S18 @auto:vitest
  Scenario: 非 root 管理员访问设置页被重定向
    Given admin 已登录但不是 root
    When 打开系统设置页
    Then 被重定向到仪表盘且不渲染知识图谱卡片与连接字段

  @SET-S19 @manual
  Scenario: 业务读取知识图谱配置
    Given OPEN_NEO4J 为 "false"
    When 业务模块调用 IKnowledgeGraphSettingsService
    Then 返回 Enabled=false 且不携带连接信息
    When OPEN_NEO4J 为 "true" 且已填写连接信息
    Then 返回 Enabled=true 与该连接信息
```
