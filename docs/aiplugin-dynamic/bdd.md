# 动态插件（DynamicPlugin）行为规格（BDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 上游：[../aiplugin-static/bdd.md](../aiplugin-static/bdd.md) ｜ 证据：[local-dev/dynamic-plugin-e2e.mjs](../../local-dev/dynamic-plugin-e2e.mjs) ｜ [local-dev/bocha-search-e2e.mjs](../../local-dev/bocha-search-e2e.mjs)
> 规范：[../DOC-STANDARD.md](../DOC-STANDARD.md)。标签：`@DYN-S<n>` 为场景主键（永久不复用）；`@auto:e2e` 由脚本验证（默认 `local-dev/dynamic-plugin-e2e.mjs`，博查成功路径与响应解析为 `local-dev/bocha-search-e2e.mjs`），`@manual` 人工走查。

## Feature: 动态插件实例列表

```gherkin
  @DYN-S1 @auto:e2e
  Scenario: 已创建的实例出现在动态插件 Tab
    Given 管理员已用动态模板 dynamic_greet 创建实例
    When 查询动态插件实例列表
    Then 列表中出现该实例
    And 该行展示模板 key、描述与配置

  @DYN-S2 @auto:e2e
  Scenario: 未创建实例时列表不含目标实例
    Given 管理员尚未创建任何动态插件实例
    When 查询动态插件实例列表
    Then 列表中不出现任何未创建的实例 key
```

## Feature: 创建与编辑动态插件实例

```gherkin
  @DYN-S3 @auto:e2e
  Scenario: 用动态模板创建实例
    Given 管理员选中模板 dynamic_greet
    When 填入合法实例 key、标题、描述与配置并提交
    Then 创建成功且无报错
    And 实例出现在动态插件列表中

  @DYN-S4 @auto:e2e
  Scenario: 实例 key 与注册表模板 key 冲突
    Given 注册表中已存在模板 dynamic_greet
    When 管理员用实例 key dynamic_greet 新建实例
    Then 保存失败并提示实例 Key 已被使用

  @DYN-S5 @auto:e2e
  Scenario: 重复提交同一实例 key 走更新
    Given 已存在实例 dyn_dup
    When 管理员用同一个实例 key 再次提交标题与配置
    Then 保存成功且不新建实例
    And 列表中该 key 仍只有一行，标题与配置为最后一次提交的值

  @DYN-S6 @auto:e2e
  Scenario: 实例 key 不符合命名规则
    Given 管理员正在新建实例
    When 实例 key 使用大写字母、数字开头或超过 30 个字符
    Then 保存被参数校验拒绝
    And 提示实例 Key 只能是小写字母、数字和下划线

  @DYN-S7 @auto:e2e
  Scenario: 模板 key 未注册或不是动态模板
    Given 管理员正在新建实例
    When 提交一个注册表中不存在的模板 key
    Then 保存失败并提示动态插件模板不存在

  @DYN-S8 @auto:e2e
  Scenario: 编辑实例的标题、描述、分类与配置
    Given 已存在实例 dyn_greet
    When 管理员修改其标题与配置后保存
    Then 更新成功
    And 再次运行该实例时使用新的配置

  @DYN-S9 @auto:e2e
  Scenario: 编辑时实例 key 不可修改
    Given 已存在实例 dyn_greet
    When 管理员在编辑弹窗中修改标题后保存
    Then 实例 key 保持不变
    And 仍以原实例 key 定位该实例
```

## Feature: 运行动态插件实例

```gherkin
  @DYN-S10 @auto:e2e
  Scenario: 运行实例使用实例存储的配置
    Given 已存在实例 dyn_greet，配置中 Prefix 为 Hello
    When 以请求参数 {"Name":"MoAI"} 运行该实例
    Then 返回 Hello MoAI
    And 运行期间无需前端传入配置

  @DYN-S11 @auto:e2e
  Scenario: 运行不存在的实例
    Given 注册表中与数据库中都不存在目标 key
    When 以该 key 运行插件
    Then 返回插件不存在
```

## Feature: 删除动态插件实例

```gherkin
  @DYN-S12 @auto:e2e
  Scenario: 删除已有实例
    Given 已存在实例 dyn_greet
    When 管理员确认删除该实例
    Then 删除成功
    And 列表中不再出现该实例

  @DYN-S13 @auto:e2e
  Scenario: 删除不存在的实例
    Given 实例已被删除
    When 管理员再次删除该实例
    Then 返回实例不存在
```

## Feature: 动态插件门禁

```gherkin
  @DYN-S14 @auto:e2e
  Scenario: 非管理员访问动态插件管理
    Given 当前登录用户为普通成员
    When 该用户查询插件列表或调用新建、删除、运行接口
    Then 请求被拒绝
    And 提示只有管理员可以管理插件
```

## Feature: 内置动态模板

```gherkin
  @DYN-S15 @auto:e2e
  Scenario: 内置模板出现在模板下拉数据源中
    Given 动态插件模块已挂载宿主并完成程序集扫描
    When 管理员查询插件注册表
    Then 返回项中包含 dynamic_greet 与 bocha_web_search 且均为动态模板
    And bocha_web_search 带 ApiKey 配置示例、检索参数示例与配置类型

  @DYN-S16 @auto:e2e
  Scenario: 博查全网搜索成功路径
    Given BoCha 桩服务按 Web Search 非流式报文返回网页与图片
    When 管理员用模板 bocha_web_search 创建实例并以搜索词与返回条数运行
    Then 返回成功的网页与图片结果
    And 每条结果包含标题、链接、摘要、站点与发布时间
    And 请求以 Bearer「实例配置中的 API Key」发出，且 Count 越界被收敛到 1-50

  @DYN-S17 @auto:e2e
  Scenario: 博查搜索配置或参数不合规
    Given 管理员已用模板 bocha_web_search 创建实例
    When 配置中的 API Key 为空后运行该实例
    Then 运行结果为失败并提示 API Key 不能为空
    And API Key 非空但搜索词为空时同样得到失败结果而非服务端异常

  @DYN-S18 @auto:e2e
  Scenario: 博查搜索对外调用失败可读
    Given 管理员已用模板 bocha_web_search 创建实例并填入无效 API Key
    When 以合法搜索词与条数运行该实例
    Then 运行结果为失败且不经由实例化失败
    And 错误信息包含博查返回的 HTTP 状态码与响应内容

  @DYN-S19 @auto:e2e
  Scenario: 内置 AI 搜索模板出现在模板下拉数据源中
    Given 动态插件模块已挂载宿主并完成程序集扫描
    When 管理员查询插件注册表
    Then 返回项中包含 bocha_ai_search 且为动态模板
    And 该项带 ApiKey 配置示例、含 Query 与 Answer 的检索参数示例与配置类型

  @DYN-S20 @auto:e2e
  Scenario: 博查 AI 搜索配置或参数不合规
    Given 管理员已用模板 bocha_ai_search 创建实例
    When 配置中的 API Key 为空后运行该实例
    Then 运行结果为失败并提示 API Key 不能为空
    And API Key 非空但搜索词为空时同样得到失败结果而非服务端异常

  @DYN-S21 @auto:e2e
  Scenario: 博查 AI 搜索对外调用失败可读
    Given 管理员已用模板 bocha_ai_search 创建实例并填入无效 API Key
    When 以合法搜索词运行该实例
    Then 运行结果为失败且不经由实例化失败
    And 错误信息包含博查返回的 HTTP 状态码与响应内容

  @DYN-S22 @auto:e2e
  Scenario: 博查 AI 搜索响应解析
    Given BoCha 桩服务按 AI Search 非流式报文返回参考网页、图片、模态卡、总结答案与追问问题
    When 管理员以搜索词、条数、Answer、Freshness 与 Include 运行该实例
    Then 总结答案汇总为 Markdown 文本、追问问题汇总为列表、会话 ID 透传
    And 参考网页与图片被逐条展开（兼容 value 包装与裸对象两种形态），模态卡按 content_type 归类并解析通用字段
    And 恒为空的 video 结果不产出条目、someResultsRemoved 被提取
    And 对外请求为非流式，且参数经兜底：Count 收敛到 1-50、空白 Freshness 回落 noLimit、Answer 与 Include 原样透传
    And 当上游按 Answer=false 省略答案与追问时，答案为 null、追问列表为空

  @DYN-S23 @auto:e2e
  Scenario: 飞书文本推送内置模板出现在模板下拉数据源中
    Given 动态插件模块已挂载宿主并完成程序集扫描
    When 管理员查询插件注册表
    Then 返回项中包含 feishu_webhook_text 且为动态模板
    And 该项带 WebhookKey 配置示例、含 Text 的请求参数示例与配置类型

  @DYN-S24 @auto:e2e
  Scenario: 飞书文本推送配置或参数不合规
    Given 管理员已用模板 feishu_webhook_text 创建实例
    When 配置中的 WebhookKey 为空后运行该实例
    Then 运行结果为失败并提示 WebhookKey 不能为空
    And WebhookKey 非空但 Text 为空时同样得到失败结果而非服务端异常
    And 用占位 token 调用真实 endpoint 时，运行结果为失败且不经由实例化失败
    And 错误信息按飞书文档码表归一（19001/19007/19021/19022/19024/9499 等映射到合适的 HTTP 状态码）
```
