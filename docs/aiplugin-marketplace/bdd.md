# 在线插件市场行为规格（BDD，Gherkin）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 证据：后端构建与 `tests/MoAI.AIPlugin.Dynamic.Tests`

```gherkin
Feature: MoPlatform 插件市场
  Background:
    Given MoPlatform:Endpoint 指向可用的平台服务
    And 当前操作者为系统管理员

  @PLGM-S1 @auto:unit
  Scenario: 首次生成安装指纹并在后续请求中复用
    Given configs 目录中没有平台客户端指纹文件
    When MoAI 初始化平台插件客户端
    Then 系统生成并持久化 256 位随机指纹
    And 后续平台插件请求都携带相同指纹

  @PLGM-S2 @manual
  Scenario: 查询平台已发布插件并回显本地安装状态
    Given MoPlatform 已发布一个可兼容的插件
    When 管理员搜索在线插件目录
    Then 返回插件元数据及是否已安装状态

  @PLGM-S3 @manual
  Scenario: 下载并安装平台插件包
    Given 管理员选择一个尚未安装的已发布插件
    When 系统检查版本清单、申请下载地址并获取完整插件包
    Then 系统校验大小和 SHA-256 后安全解压并登记安装
    And 插件运行时注册该插件

  @PLGM-S4 @manual
  Scenario: 拒绝重复安装或在线覆盖
    Given 相同插件 key 已安装
    When 管理员再次安装相同或新版本插件
    Then 系统返回冲突且不覆盖本地已安装包

  @PLGM-S5 @manual
  Scenario: 下架不卸载或删除本地包
    Given 在线插件已安装并加载
    When 管理员将其设为下架
    Then 数据库状态变为禁用
    And 本地包文件保留且当前进程不执行卸载

  @PLGM-S6 @auto:unit
  Scenario: 安装时导入包内语义规则
    Given 平台插件 ZIP 包含合法 semantic.json v1 和文字 category
    When MoAI 下载、校验并安装该插件
    Then 插件级使用须知、语义条目和工具元数据写入本地语义表
    And 本地同名分类被绑定，不存在的分类自动创建
    And 语义配置查询接口可直接返回这些预置规则
    And 不含 semantic.json 的旧包仍可按空语义兼容安装

  @PLGM-S7 @auto:vitest
  Scenario: 在线插件目录按 MoPlatform 分类以卡片展示
    Given MoPlatform 已提供插件分类和已发布插件
    When 管理员打开在线插件目录并选择一个分类
    Then 目录按分类筛选并以卡片展示插件
    And 页面不显示插件市场或已安装子页签、已安装列表和刷新按钮

  @PLGM-S8 @auto:vitest
  Scenario: 批量安装本页选中的在线插件
    Given 在线插件目录当前页有多个尚未安装的插件
    When 管理员选择多个插件并执行批量安装
    Then 系统按顺序逐个安装所选插件并继续处理单项失败
    And 页面反馈成功与失败数量并保留失败项供重试
```
