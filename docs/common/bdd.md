# 公共领域（Common）行为规格（BDD，Gherkin）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md)（场景→验证映射） ｜ [SOP](./sop.md)（操作与验收流程）
> 编号规则与标签语义见 [../DOC-STANDARD.md](../DOC-STANDARD.md) 第 3 节。

```gherkin
Feature: 服务器公开信息（serverinfo）
  @COM-S1 @auto:e2e
  Scenario: 匿名获取服务器信息
    When 未携带登录凭证请求服务器信息
    Then 返回服务名、服务地址、公有文件直链前缀、RSA 公钥与上传大小上限
    And 公有文件直链前缀为服务地址下的 /static 路径

  @COM-S2 @auto:e2e
  Scenario: 下发的公钥可用于密码加密
    Given 已从服务器信息取得 RSA 公钥
    When 用其以 PKCS1 方式加密密码后发起登录
    Then 服务端可解密并完成密码比对（登录成功）

  @COM-S3 @auto:e2e
  Scenario: 下发公钥与运行时私钥伴生一致
    When 从服务器私钥文件派生公钥并与下发值比对
    Then 两者逐字节一致（证明下发公钥即该私钥的伴生公钥）

Feature: 全局唯一 id（build_guid）
  @COM-S4 @auto:e2e
  Scenario: 匿名访问被拒
    When 未携带登录凭证请求生成全局 id
    Then 返回未授权（401）

  @COM-S5 @auto:e2e
  Scenario: 登录后获取全局 id
    Given 用户已登录
    When 请求生成全局 id
    Then 返回合法 GUID（v7，时间前缀）
    And 连续两次调用取值不同

Feature: 前端消费
  @COM-S6 @manual
  Scenario: 登录页首帧拉取服务器信息
    Given 用户打开登录页
    Then 前端调用服务器信息（有本地缓存则跳过请求）
    And 密码提交前用缓存的公钥加密

  @COM-S7 @manual
  Scenario: 上传前体积校验（实际行为：未统一接入，缺陷记录）
    Given 选择的文件大于上传大小上限
    Then 设计上应阻止上传
    And 实际当前上传入口未统一接入该校验（见 [SDD 已知问题](./sdd.md)，计划 ui/docs 轮 12）
```
