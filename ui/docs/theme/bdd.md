# 前端主题系统（theme）行为规格（BDD，Gherkin）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md)（场景→验证映射） ｜ [SOP](./sop.md)（操作与验收流程）
> 编号规则与标签语义见 [DOC-STANDARD](../../../docs/DOC-STANDARD.md) 第 3 节。

```gherkin
Feature: 主题预设
  @FE-TH-S1 @auto:vitest
  Scenario: 默认预设为亮色
    Given 应用未指定主题
    When 取默认主题配置
    Then 得到亮色预设，主色为品牌蓝 #2970FF
    And 主题算法已定义

  @FE-TH-S2 @auto:vitest
  Scenario: 预设集合恰为亮/暗两套
    When 枚举可用主题预设
    Then 预设键恰为 亮色 与 暗色 两项（类型层面收口）
    And 暗色预设同样使用品牌主色 #2970FF（对比度由暗色算法保证）

Feature: 主题初始化
  @FE-TH-S3 @manual
  Scenario: 无历史选择时跟随系统偏好
    Given 本地存储中没有主题记录
    And 系统偏好为暗色
    When 应用启动
    Then 初始主题为暗色

  @FE-TH-S4 @manual
  Scenario: 历史选择优先于系统偏好
    Given 本地存储中已记录主题为亮色
    And 系统偏好为暗色
    When 应用启动
    Then 初始主题仍为亮色

Feature: 主题切换与语言联动
  @FE-TH-S5 @manual
  Scenario: 切换主题并持久化
    Given 用户在侧边栏底部看到主题选择器
    When 选择暗色
    Then 全站组件即时切换为暗色预设
    And 本地存储记录该选择
    And 刷新页面后仍保持暗色

  @FE-TH-S6 @manual
  Scenario: 语言切换联动
    When 界面语言在 简体中文 与 English 间切换
    Then antd 内置文案（分页、弹窗按钮等）同步切换
    And 页面文案与页面语言标记同步更新

Feature: 设计令牌
  @FE-TH-S7 @auto:vitest
  Scenario: 令牌值约束
    When 读取设计令牌
    Then 主色为品牌蓝 #2970FF，品牌色 primary 与主色同值
    And 间距符合 8px 栅格（8/16/24）
    And 默认圆角为 8，字体族含 sans-serif 兜底

  @FE-TH-S8 @manual
  Scenario: 业务组件统一引用令牌
    Given 业务组件需要颜色/间距/圆角
    When 取样式值
    Then 一律来自设计系统的令牌导出，不出现魔法数

Feature: antd 装配
  @FE-TH-S9 @manual
  Scenario: 全局唯一注入点
    When 应用渲染
    Then ConfigProvider 以当前主题配置与语言包裹全局
    And 页面内 message/notification/modal 均经上下文取实例，无静态调用
```
