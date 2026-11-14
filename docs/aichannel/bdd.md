# AI 渠道与模型目录行为规格（BDD，Gherkin）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 证据：[模型目录 E2E](../../local-dev/model-catalog-e2e.mjs)

## 模型目录同步与识别

Feature: MoPlatform 模型目录同步

  @MC-S2 @auto:e2e
  Scenario: 管理员同步 MoPlatform 最新模型目录
    Given MoPlatform 已发布有效的 schema v1 模型目录
    When 管理员触发目录同步
    Then 系统校验文件大小与 SHA-256 后更新本地目录
    And 接下来的渠道模型探测使用新目录元数据

  @MC-S3 @auto:e2e
  Scenario: 无本地目录时不使用打包的 OpenCode 目录
    Given 本地 configs 目录中没有 models.json
    When 用户探测一个未同步的渠道模型
    Then 候选模型返回未识别状态

  @MC-S4 @auto:e2e
  Scenario: 本地目录损坏时模型探测仍可继续
    Given 本地 models.json 内容不是有效 JSON
    When 用户探测一个渠道模型
    Then 候选模型返回未识别状态
    And 渠道探测请求正常完成

  @MC-S5 @auto:e2e
  Scenario: 解析 MoPlatform schema v1 元数据
    Given 本地目录包含 MoPlatform 渠道和模型记录
    When 用户探测目录中存在的模型
    Then 候选模型被识别并回显目录名称与类型
    And 上下文、模态、能力和价格元数据被映射

  @MC-S6 @manual
  Scenario: 短时效签名下载地址失败后刷新指针重试
    Given MoPlatform latest 返回有效期为十分钟的 S3 预签名下载地址
    When 下载超时并切换网络路径重试
    Then 系统重新获取 latest 指针后再发起下载
    And 下载内容按本次指针的大小与 SHA-256 校验且日志不泄露签名参数
