# 前端 Kiota API 层（api-layer）行为规格（BDD，Gherkin）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md)（场景→验证映射） ｜ [SOP](./sop.md)（操作与验收流程）
> 编号规则与标签语义见 [../../../docs/DOC-STANDARD.md](../../../docs/DOC-STANDARD.md) 第 3 节。错误归一化有单测 → @auto:vitest；其余为命令/走查 → @manual；已知问题如实写成场景并标注（缺陷记录）。

```gherkin
Feature: 请求中间件（FilterRequestHandler，位于默认中间件最前）
  @FE-API-S1 @manual
  Scenario: 业务接口返回 401
    Given 已登录，某接口返回 401 且 URL 不含 "login"
    When 响应经过中间件
    Then 清空登录态并整页跳转 /login

  @FE-API-S2 @manual
  Scenario: 登录接口本身 401（密码错误）
    Given 登录接口返回 401（后端文案"用户名或密码错误"）
    When 响应经过中间件
    Then 因 URL 含 "login" 不清态不跳转，仅展示后端文案

  @FE-API-S3 @auto:vitest
  Scenario: 业务错误归一化提示
    Given 响应非 2xx 且 body 含 detail 或字段级 errors
    When 响应经过中间件
    Then 归一化为标准错误对象并按严重度提示（业务 4xx→消息条，5xx→通知）
    And 展示优先级：detail 优先，其次第一条字段错误，最后过滤后的 message
    And 向调用方抛出归一化错误

  @FE-API-S4 @auto:vitest
  Scenario: 网络断连
    Given 请求抛异常且无 HTTP 状态码（网络错误）
    When 异常被中间件捕获
    Then 弹网络错误提示（通知渠道）并重抛
    And 登录态保留

  @FE-API-S5 @manual
  Scenario: 非 HTTP 层异常（如下游中间件抛错）
    Given 抛出的异常带响应状态码（非网络错误）
    When 异常被中间件捕获
    Then 提示后清空登录态再重抛（防御性）

Feature: 客户端工厂
  @FE-API-S6 @manual
  Scenario: 登录后的业务请求
    Given store 中存在 accessToken
    When 获取鉴权客户端发起请求
    Then 携带 Authorization: Bearer <token>，baseUrl 为环境配置的服务地址

  @FE-API-S7 @manual
  Scenario: 未登录请求公开接口
    When 获取匿名客户端发起请求
    Then 无 Authorization 头，可访问登录/注册/serverinfo 等匿名端点

Feature: 客户端再生成（syncapi）
  @FE-API-S8 @manual
  Scenario: 后端新增接口后同步
    Given 后端运行中且 OpenAPI 文档已含新端点
    When 以实际后端地址执行客户端再生成
    Then 生成物出现对应目录，lock 文件更新描述 hash 与地址
    And typecheck 通过（kiota CLI=1.27.0 前提）

  @FE-API-S9 @manual
  Scenario: 用错 kiota 版本
    When 以非 1.27.0 的 kiota CLI 生成后执行 typecheck
    Then 生成代码与运行时依赖签名不兼容，typecheck 失败

  @FE-API-S10 @manual
  Scenario: 默认文档源指向 5000 遗留值（缺陷记录：syncapi 默认地址过时）
    Given 本地后端实际运行于 5210
    When 不传参直接执行客户端再生成
    Then 默认请求 5000 的 OpenAPI 文档，拉取失败（须显式传实际地址）

Feature: 封装约定
  @FE-API-S11 @manual
  Scenario: 新增领域封装
    When 要为某后端领域加前端调用
    Then 封装文件放 ui/src/api/<domain>.ts，仅依赖两个客户端工厂
    And 需要加密的密码字段在封装内用服务器公钥加密，页面只传明文
    And int64 id 在封装层按 string 透传，页面不直接引用生成物

  @FE-API-S12 @manual
  Scenario: 含 "login" 字样的其它接口被误豁免（缺陷记录：401 豁免用子串粗匹配）
    Given 未来出现路径含 "login" 的非登录接口
    When 该接口返回 401
    Then 被误判为登录接口而不清态不跳转
```
