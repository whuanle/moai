# infra 基础设施（INF）行为规格（BDD，Gherkin）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md)（场景→验证映射） ｜ [SOP](./sop.md)（操作与验收流程）
> 编号规则与标签语义见 [../DOC-STANDARD.md](../DOC-STANDARD.md) 第 3 节。验证物为构建/配置命令与代码走查（见 TDD）。

```gherkin
Feature: 系统配置加载
  Background:
    Given 应用目录为 {AppPath}
    And 默认配置文件位于 {AppPath}/configs/system.json

  @INF-S1 @manual
  Scenario: 未设置 MAI_FILE 时加载默认配置
    When 进程启动且未定义环境变量 MAI_FILE
    Then 追加配置源 configs/system.json（文件缺失不报错）
    And "MoAI" 节绑定到 SystemOptions 单例

  @INF-S2 @manual
  Scenario: MAI_FILE 指定 json 文件且优先级最高
    Given MAI_FILE 指向存在的 json 文件
    When 进程启动
    Then 该文件追加为最后一个配置源，同名键覆盖默认源
    And Kestrel 监听 MoAI:Port 与 MoAI:Port+1 两个端口

  @INF-S3 @manual
  Scenario: yaml 与 conf 后缀路由
    When MAI_FILE 扩展名为 .yaml（或 .conf）
    Then 分别以 yaml / ini 配置提供程序加载（内容等价时行为一致）

  @INF-S4 @manual
  Scenario: MAI_FILE 指向不存在的文件
    When 文件不存在（无论 Debug 或 Release）
    Then 静默回落默认配置且不抛异常（缺陷记录：易误以为配置已生效）

  @INF-S5 @manual
  Scenario: 不支持的扩展名
    When MAI_FILE 为 .xml 且编译为 Release
    Then 启动抛异常 "The current file type cannot be imported,`MAI_FILE=...`"
    When 编译为 DEBUG
    Then 静默忽略该文件仅加载默认源（缺陷记录：与 Release 行为不一致）

  @INF-S6 @manual
  Scenario: 配置缺少 MoAI 节
    When 配置绑定失败
    Then 启动抛 "The system configuration cannot be loaded."

  @INF-S7 @manual
  Scenario: 未知键被忽略
    Given 配置含 DBType / Wiki / Storage.LocalPath（不在强类型上）
    Then 绑定忽略未知键，应用正常启动

Feature: RSA 密钥生命周期
  @INF-S8 @manual
  Scenario: 首次启动生成密钥
    Given configs/rsa_private.key 不存在
    When 配置模块初始化
    Then 生成 2048 位 RSA 导出 PKCS8 PEM 写入该文件并注册提供者

  @INF-S9 @manual
  Scenario: 复用已有密钥
    Given rsa_private.key 已存在
    Then 直接读取注册，不重新生成

  @INF-S10 @manual
  Scenario: 公钥下发
    When 请求服务器信息接口
    Then 返回 rsaPublic（Base64 SPKI），前端以此做 RSA PKCS1 加密密码

  @INF-S11 @manual
  Scenario: 轮换密钥的后果
    When 删除 rsa_private.key 并重启
    Then 新密钥生成，所有旧 JWT 失效
    And 前端重新拉取公钥后方可登录

Feature: 模型验证自动注册
  @INF-S12 @manual
  Scenario: 实现 IModelValidator 的命令被自动注册
    Given 类型 T 实现 IModelValidator<T>（泛型参数为自身）
    When 模块扫描
    Then 注册验证器与 T 自身为 Scoped
    And Validate 中的规则在 Controller 模型绑定时自动执行

  @INF-S13 @manual
  Scenario: 继承不重复注册
    Given 类型 S 继承 T 而验证器泛型参数是 T
    Then S 不被重复注册（防继承判断）

Feature: 用户上下文注入
  @INF-S14 @manual
  Scenario: Controller 注入操作者
    Given 用户已认证
    When Controller 对实现 IUserIdContext 的命令注入操作者
    Then 命令的操作者 id 与类型被写入（init 属性）
    And Handler 内直接读取，无需再注入 Provider

  @INF-S15 @manual
  Scenario: 匿名上下文
    Given 请求无认证信息
    Then 用户上下文返回匿名（UserId 为 0），消费方自行判空

Feature: 外部 HTTP 客户端
  @INF-S16 @manual
  Scenario: 固定第三方客户端解析
    When 注入任一固定地址的外部客户端
    Then BaseAddress 为注册的固定地址
    And JSON 序列化统一 camelCase、忽略 null、允许多余逗号与注释

  @INF-S17 @manual
  Scenario: 出站请求观测
    When 任一外部客户端发起调用
    Then 拦截器记录方法/URL/状态码/耗时/请求响应头与内容
    And 二进制内容以 [Binary Content] 打码，异常路径同样记录

  @INF-S18 @manual
  Scenario: 动态 OAuth 客户端
    When 以任意 authority 创建 OAuth 客户端
    Then 基于命名 HttpClient 动态创建 Refit 客户端（复用连接池与日志拦截器）
    And 可请求任意 IdP 的 well-known / token / userinfo 端点

Feature: ID 与加密助手
  @INF-S19 @manual
  Scenario: 雪花 ID
    When 调用 ID 提供者
    Then 返回雪花 ID（序列位 10、WorkerId=0）
    And 键生成方法返回 16 位十六进制字符串

  @INF-S20 @manual
  Scenario: AES 加解密
    When 以配置密钥（补齐/截断到 32 字节）加解密
    Then 密文为 Base64(IV(16 字节)+密文)，解密还原原文

  @INF-S21 @manual
  Scenario: 密码哈希
    When 对密码做 PBKDF2 哈希
    Then 输出 128 字节哈希与 128 字节盐（10000 次迭代 SHA256）
    And 校验对非法 Base64 输入返回 false 而非抛异常

Feature: 消息队列装配
  @INF-S22 @manual
  Scenario: RabbitMQ 统一装配
    When 模块装配完成
    Then MQ 连接使用 MoAI:RabbitMQ 连接串并自动声明队列
    And 消费并发 100、AppName 取系统名、客户端标识 moai
