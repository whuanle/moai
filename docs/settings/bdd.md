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
    Then 被重定向到应用市场
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
    Then 被重定向到应用市场且不渲染知识图谱卡片与连接字段

  @SET-S19 @manual
  Scenario: 业务读取知识图谱配置
    Given OPEN_NEO4J 为 "false"
    When 业务模块调用 IKnowledgeGraphSettingsService
    Then 返回 Enabled=false 且不携带连接信息
    When OPEN_NEO4J 为 "true" 且已填写连接信息
    Then 返回 Enabled=true 与该连接信息
```

```gherkin
Feature: 沙箱资源上限设置（每个应用）

  @SET-S20 @auto:e2e
  Scenario: root 配置沙箱上限并回读生效
    Given root 已登录
    When 保存 SANDBOX_MAX_TTL_SECONDS="600"、SANDBOX_MAX_CPU="2000m"、SANDBOX_MAX_MEMORY="1Gi"
    Then 三项保存成功，再次查询返回提交值

  @SET-S21 @auto:e2e
  Scenario: 沙箱上限保存格式校验
    Given root 已登录
    When 保存存活时间 "30"（低于 60）或 "700000"（高于 604800）或 CPU "fast" 或内存 "8gi"
    Then 返回请求错误（400）提示范围或格式无效

  @SET-S22 @auto:unit
  Scenario: 业务读取沙箱上限
    When 业务模块调用 ISandboxSettingsService.GetLimitsAsync
    Then 返回上限（秒 / CPU 原文与毫核 / 内存原文与字节），存活时间收敛到 60~604800
    And 设置缺失或值非法时回退内置默认 86400 / 4 / 8Gi

  @SET-S23 @auto:vitest
  Scenario: 设置页沙箱上限卡片
    Given root 登录并进入系统设置页
    Then 「沙箱资源上限」卡片默认折叠，回显三项当前值
    When 修改后保存
    Then 逐项提交三个 key
    When 填入非法数量格式保存
    Then 前端拦截且不发起保存请求
```

```gherkin
Feature: 网站 Logo（全局品牌）
  Background:
    Given 系统内置设置项 SYSTEM_LOGO（默认空串，空表示使用前端默认 Logo）

  @SET-S24 @auto:e2e
  Scenario: 匿名可读网站 Logo
    When 未登录查询服务器信息
    Then 返回 logoPath 字段（string，当前 Logo 的存储标识）

  @SET-S25 @auto:e2e
  Scenario: root 上传图片并替换网站 Logo
    Given root 已登录并完成一张图片的上传登记
    When 提交该图片作为网站 Logo
    Then 操作成功

  @SET-S26 @auto:e2e
  Scenario: Logo 替换后全局生效
    Given root 已上传并提交网站 Logo
    Then 设置项列表中 SYSTEM_LOGO 为该图片标识
    And 未登录查询服务器信息返回该图片标识

  @SET-S27 @auto:e2e
  Scenario: 非 root 不能替换网站 Logo
    Given member 已登录
    When 提交任一网站 Logo
    Then 返回禁止访问（403）
    When 未登录提交网站 Logo
    Then 返回未授权（401）

  @SET-S28 @auto:e2e
  Scenario: 未登记的文件被拒绝
    Given root 已登录
    When 提交未完成上传登记的图片标识
    Then 返回未找到（404）提示 Logo 文件不存在或未完成上传

  @SET-S29 @auto:e2e
  Scenario: 恢复默认 Logo
    Given 当前已设置自定义网站 Logo
    When root 提交空的图片标识
    Then 操作成功
    And 未登录查询服务器信息返回空 Logo

  @SET-S30 @auto:vitest
  Scenario: 设置页网站信息卡片（网站 Logo 分区）
    Given root 登录并进入系统设置页
    Then 「网站信息」卡片默认展开（含网站名称、网站 Logo、登录页背景图三个分区）
    And Logo 分区回显当前 Logo（无自定义时为默认）
    When 点击图片选择合法图片文件
    Then 上传并提交后刷新全局 Logo，出现「恢复默认」入口
    When 选择非图片文件
    Then 前端拦截不发起上传
    When 确认「恢复默认」
    Then 清空 Logo 并恢复默认展示
```

```gherkin
Feature: 网站名称（仅前端展示）
  Background:
    Given 系统内置设置项 SYSTEM_NAME（默认空串，空表示使用配置文件默认名称，去空白后最长 50 字符）

  @SET-S31 @auto:e2e
  Scenario: root 修改网站名称并全局生效
    Given root 已登录
    When 保存 SYSTEM_NAME 为新名称
    Then 操作成功
    And 未登录查询服务器信息返回 name 为新名称
    And 设置项列表中 SYSTEM_NAME 为新名称

  @SET-S32 @auto:e2e
  Scenario: 超长网站名称被拒绝
    Given root 已登录
    When 保存去空白后超过 50 字符的名称
    Then 返回请求无效（400）提示长度超限

  @SET-S33 @auto:e2e
  Scenario: 门禁与恢复默认名称
    Given member 已登录
    When 保存任一网站名称
    Then 返回禁止访问（403）
    Given root 已保存自定义网站名称
    When root 提交空名称
    Then 操作成功
    And 未登录查询服务器信息返回 name 回退为配置文件默认名称（非空）

  @SET-S34 @auto:vitest
  Scenario: 设置页网站信息卡片（网站名称分区）
    Given root 登录并进入系统设置页
    Then 「网站信息」卡片默认展开，网站名称分区回显当前设置值（最长 50 字符）
    When 修改名称并保存
    Then 去首尾空白后提交 SYSTEM_NAME 并刷新全局 serverInfo（侧边栏标题与浏览器标签页立即生效）
    When 清空名称并保存
    Then 提交空串恢复默认名称

  @SET-S43 @auto:vitest
  Scenario: 登录/注册页展示网站名称
    Given 超级管理员已设置自定义网站名称
    When 未登录打开登录页
    Then 登录卡片主标题展示该名称且浏览器标签页标题同步
    When 未登录打开注册页
    Then 注册卡片主标题与标签页同样展示该名称
    When 清空网站名称后打开登录页
    Then 主标题与标签页回退默认名称
```

```gherkin
Feature: 登录页背景图
  Background:
    Given 系统内置设置项 LOGIN_BACKGROUND（默认空串，空表示使用前端默认渐变背景）

  @SET-S35 @auto:e2e
  Scenario: 匿名可读登录页背景
    When 未登录查询服务器信息
    Then 返回 loginBackgroundPath 字段（string，当前背景图的存储标识）

  @SET-S36 @auto:e2e
  Scenario: root 上传图片并替换登录页背景
    Given root 已登录并完成一张图片的上传登记
    When 提交该图片作为登录页背景
    Then 操作成功

  @SET-S37 @auto:e2e
  Scenario: 背景替换后全局生效
    Given root 已上传并提交登录页背景
    Then 设置项列表中 LOGIN_BACKGROUND 为该图片标识
    And 未登录查询服务器信息返回该图片标识

  @SET-S38 @auto:e2e
  Scenario: 非 root 不能替换登录页背景
    Given member 已登录
    When 提交任一登录页背景
    Then 返回禁止访问（403）
    When 未登录提交登录页背景
    Then 返回未授权（401）

  @SET-S39 @auto:e2e
  Scenario: 未登记的文件被拒绝
    Given root 已登录
    When 提交未完成上传登记的图片标识
    Then 返回未找到（404）提示背景图文件不存在或未完成上传

  @SET-S40 @auto:e2e
  Scenario: 恢复默认背景
    Given 当前已设置自定义登录页背景
    When root 提交空的图片标识
    Then 操作成功
    And 未登录查询服务器信息返回空背景

  @SET-S41 @auto:vitest
  Scenario: 设置页网站信息卡片（登录页背景分区）
    Given root 登录并进入系统设置页
    Then 「网站信息」卡片默认展开，登录页背景分区回显当前背景（无自定义时显示默认渐变占位）
    When 点击预览图选择合法图片文件
    Then 上传并提交后刷新全局背景，出现「恢复默认」入口
    When 选择非图片文件
    Then 前端拦截不发起上传
    When 确认「恢复默认」
    Then 清空背景并恢复默认展示

  @SET-S42 @manual
  Scenario: 登录页渲染背景
    Given 已设置自定义登录页背景
    When 打开登录页
    Then 整页以该图片铺满展示（居中 cover）
    When 清空背景后打开登录页
    Then 回退默认渐变背景
```

```gherkin
Feature: 知识库允许上传的文件类型
  Background:
    Given 系统内置设置项 WIKI_ALLOWED_FILE_TYPES（逗号分隔扩展名，默认 ".pdf,.docx,.xlsx,.pptx,.html,.htm,.md,.markdown,.txt,.json"，与文档/文本提取能力对齐，图片暂不支持）

  @SET-S44 @auto:e2e
  Scenario: root 配置允许上传的文件类型并全局生效
    When 保存 WIKI_ALLOWED_FILE_TYPES 为 ".txt,.sh"
    Then 保存成功，upload-limit 查询接口回显 allowedFileTypes（成员可读，网页端与外部开放接口共用同一份白名单）
    When 成员预上传不在允许类型内的文件（如 .pdf，网页端与外部接口同一入口）
    Then 返回请求错误（400）且错误信息携带允许类型清单
    When 恢复默认允许类型
    Then 原被拒绝的类型预上传重新通过

  @SET-S45 @auto:e2e
  Scenario: 允许类型保存格式校验
    Given root 已登录
    When 保存不含点号开头项、大小写归一后重复项或空白的类型串
    Then 返回请求错误（400）提示格式无效
    And 合法值以小写去重后的形式落库

  @SET-S46 @auto:e2e
  Scenario: 放开的未知文本类型按纯文本兜底提取
    Given 允许类型包含 Maomi.ToMarkdown 不识别的文本类扩展名（如 .sh）
    When 成员上传该类型文件并完成上传登记
    Then 自动提取按纯文本读取内容入库
    But 二进制文件（起始 8KB 含 NUL 字节）提取失败不产出乱码文本

  @SET-S47 @auto:vitest
  Scenario: 设置页知识库卡片文件类型配置
    Given root 登录并进入系统设置页
    Then 「知识库」卡片默认折叠，紧邻「知识图谱（图数据库）」卡片上方
    And 展开后回显允许类型标签（设置缺失或非法时回退内置默认）
    When 增删标签并保存
    Then 归一为小写去重逗号串提交 WIKI_ALLOWED_FILE_TYPES
    When 填入非法扩展名保存
    Then 前端拦截且不发起保存请求
```

```gherkin
Feature: SMTP 邮件服务
  Background:
    Given 系统内置设置项 SMTP_ENABLED（默认 "false"）与 SMTP_HOST/SMTP_PORT/SMTP_SECURITY/SMTP_USERNAME/SMTP_PASSWORD/SMTP_FROM_ADDRESS/SMTP_FROM_NAME（默认空串，端口默认 "465"、加密默认 "ssl"）

  @SET-S48 @auto:e2e
  Scenario: root 配置 SMTP 邮件服务并回读生效
    When 保存 SMTP_ENABLED="true" 与服务器地址、端口、加密方式、登录账号、密码、发件人地址、发件人显示名
    Then 八项保存成功，再次查询返回提交值
    And 开关与加密方式的大小写输入归一为小写落库

  @SET-S49 @auto:e2e
  Scenario: SMTP 配置保存格式校验
    Given root 已登录
    When 保存端口 "0"、"70000" 或 "abc"，或加密方式 "tls"，或开关 "yes"，或非法的发件人邮箱
    Then 返回请求错误（400）提示格式无效

  @SET-S50 @auto:e2e
  Scenario: 非 root 不能修改或发送测试邮件
    Given member 已登录
    When 修改任一 SMTP 设置
    Then 返回禁止访问（403）
    When member 或未登录发送测试邮件
    Then 分别返回禁止访问（403）与未授权（401）

  @SET-S51 @auto:e2e
  Scenario: 未启用或未配置服务器时测试邮件被拒绝
    Given SMTP_ENABLED 为 "false"，或已启用但服务器地址为空
    When root 发送测试邮件
    Then 返回请求错误（400）提示未启用或未配置服务器地址

  @SET-S52 @auto:e2e
  Scenario: 发送测试邮件全链路
    Given SMTP 指向本地 SMTP 桩服务器（不加密、免认证）
    When root 发送测试邮件到指定收件地址
    Then 操作成功，桩服务器收到一封邮件
    And 信封发件人为配置的发件人地址、收件人为目标地址，DATA 含 Subject 与 Message-Id 头

  @SET-S53 @auto:e2e
  Scenario: 配置账号密码时先认证再发送
    Given SMTP 配置了登录账号与密码，桩服务器要求认证
    When root 发送测试邮件
    Then 桩服务器记录到 AUTH 凭据与配置一致，认证后收到邮件

  @SET-S54 @auto:vitest
  Scenario: 设置页邮件服务卡片
    Given root 登录并进入系统设置页
    Then 「邮件服务（SMTP）」卡片默认折叠，未启用时不显示服务器字段与测试邮件入口
    When 启用并填写连接信息保存
    Then 先提交 SMTP_ENABLED 再提交七项连接设置
    When 配置未保存时点击发送测试邮件
    Then 被提示先保存；保存后填写收件地址可成功调用发送

  @SET-S55 @manual
  Scenario: 业务读取 SMTP 配置与通知发送
    When 业务模块调用 IEmailSettingsService
    Then 未启用时 Enabled=false 且不携带连接信息；启用时返回解析后的端口/加密方式与发件人（发件人留空回退登录账号）
    When 业务模块调用 IEmailSenderService 发送通知邮件
    Then 使用该配置经 MailKit 发送；未启用或未配置时抛出业务异常

Feature: 邮箱验证码设置（注册 / 找回密码）
  @SET-S56 @auto:e2e
  Scenario: 注册验证码开关保存校验
    When 保存 REGISTER_EMAIL_CODE_ENABLED 为非布尔的非法值
    Then 返回请求错误（400）提示必须为 true 或 false
    When 保存合法值
    Then 归一为小写并生效（注册验证码需 SMTP 邮件服务已启用才真正开启）

  @SET-S57 @auto:e2e
  Scenario: 注册验证码邮件主题模板保存
    When 保存 SMTP_REGISTER_CODE_SUBJECT 超过 500 字符
    Then 返回请求错误（400）
    When 保存带首尾空白的内容
    Then 去空白后落库回读

  @SET-S58 @auto:e2e
  Scenario: 验证码邮件正文模板保存
    When 保存 SMTP_REGISTER_CODE_BODY 或 SMTP_RESET_CODE_BODY 超过 2000 字符
    Then 返回请求错误（400）
    And 模板渲染行为见 [@AUTH-S31](../auth/bdd.md#auth-s31)
```
