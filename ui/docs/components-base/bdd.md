# 前端基础组件（components-base）行为规格（BDD，Gherkin）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md)（场景→验证映射） ｜ [SOP](./sop.md)（操作与验收流程）
> 编号规则与标签语义见 [DOC-STANDARD](../../../docs/DOC-STANDARD.md) 第 3 节。

```gherkin
Feature: Page 页面容器
  @FE-CB-S1 @auto:vitest
  Scenario: 渲染标题、副标题与内容
    Given 未提供面包屑与右侧扩展区
    When 传入标题、副标题与页面内容
    Then 三者同时出现，标题为页级标题字号

  @FE-CB-S2 @auto:vitest
  Scenario: 渲染面包屑与右侧扩展区
    When 传入两级面包屑与一个操作按钮
    Then 面包屑与操作按钮均出现
    And 操作按钮位于头部右侧

  @FE-CB-S3 @manual
  Scenario: 无头部信息时不渲染页头
    Given 标题/副标题/面包屑/扩展区全部未传
    When 渲染页面容器
    Then 只输出宽度撑满的容器与内容，无任何头部
    And 页面内容撑满可用宽度（容器无最大宽度收口、不居中）

Feature: Card 通用卡片
  @FE-CB-S4 @manual
  Scenario: 统一样式的卡片
    When 使用卡片渲染任意内容
    Then 卡片为无边框变体叠加浅色描边与浅阴影
    And 内容区内边距固定，带标题时标题栏有底边框与加粗字重

  @FE-CB-S5 @manual
  Scenario: 调用方覆盖样式
    When 通过样式类 props 传入自定义样式
    Then 覆盖默认边框/阴影/内边距
    And 其余卡片能力原样生效

Feature: StatCard 统计指标卡
  @FE-CB-S6 @auto:vitest
  Scenario: 渲染指标名与数值
    When 传入指标名与数值
    Then 两者均出现，数值为大号加粗样式

  @FE-CB-S7 @manual
  Scenario: 趋势角标
    When 趋势值分别为 正数 / 负数 / 零
    Then 分别显示 上升 / 下降 / 持平 图标与绝对值百分数
    And 趋势文字使用次要色

  @FE-CB-S8 @manual
  Scenario: 加载骨架
    When 传入加载态为真
    Then 卡片内容以骨架屏占位

Feature: DataTable 数据表格
  Background:
    Given 国际化文案已初始化

  @FE-CB-S9 @auto:vitest
  Scenario: 渲染列与数据行
    When 传入列定义与数据源且关闭分页
    Then 表头与数据行正确出现
    And 行键默认取数据的 id 字段

  @FE-CB-S10 @auto:vitest
  Scenario: 提供刷新回调
    When 传入刷新回调
    Then 工具区出现「刷新」按钮
    When 点击「刷新」
    Then 回调被触发一次
    And 工具区为左对齐布局而非两端对齐

  @FE-CB-S11 @auto:vitest
  Scenario: 未提供刷新回调
    When 不传刷新回调
    Then 不渲染「刷新」按钮

  @FE-CB-S12 @auto:vitest
  Scenario: 自定义工具区
    When 传入自定义工具节点
    Then 该节点渲染在表格上方左侧

  @FE-CB-S13 @manual
  Scenario: 分页默认值
    When 传入分页配置对象
    Then 分页器自带每页条数切换与「共 N 条」总数字样
    When 传入分页关闭标记
    Then 不渲染分页器

Feature: QueryBar 独立查询表单
  Background:
    Given 国际化文案已初始化

  @FE-CB-S14 @auto:vitest
  Scenario: 触发查询
    When 点击「查询」按钮或在输入框内回车提交
    Then 查询回调收到当前表单字段值对象

  @FE-CB-S15 @auto:vitest
  Scenario: 重置筛选
    When 点击「重置」按钮
    Then 表单字段被清空
    And 重置回调被触发（由调用方重新拉取列表）

  @FE-CB-S16 @manual
  Scenario: 受控表单实例
    When 调用方传入表单实例
    Then 查询/重置操作作用于该实例
    When 未传实例
    Then 组件内部自建实例

Feature: PageToolbar 页头工具行
  @FE-CB-S17 @auto:vitest
  Scenario: 筛选在左、操作在右
    When 同时传入筛选区与操作按钮组
    Then 两者同一行渲染，筛选占左侧伸展区（可换行），操作按钮靠右不被压缩

  @FE-CB-S18 @auto:vitest
  Scenario: 无筛选时操作左对齐
    When 仅传入操作按钮组
    Then 操作按钮直接靠左排列
```
