# 动态插件（DynamicPlugin）行为规格（BDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 上游：[../aiplugin-static/bdd.md](../aiplugin-static/bdd.md) ｜ 证据：[local-dev/dynamic-plugin-e2e.mjs](../../local-dev/dynamic-plugin-e2e.mjs) ｜ [local-dev/bocha-search-e2e.mjs](../../local-dev/bocha-search-e2e.mjs) ｜ [local-dev/moji-weather-e2e.mjs](../../local-dev/moji-weather-e2e.mjs) ｜ [local-dev/ops-p1-plugins-e2e.mjs](../../local-dev/ops-p1-plugins-e2e.mjs) ｜ [local-dev/ops-p2-plugins-e2e.mjs](../../local-dev/ops-p2-plugins-e2e.mjs) ｜ [local-dev/ops-p0-plugins-e2e.mjs](../../local-dev/ops-p0-plugins-e2e.mjs) ｜ [local-dev/hot-board-e2e.mjs](../../local-dev/hot-board-e2e.mjs)
> 规范：[../DOC-STANDARD.md](../DOC-STANDARD.md)。标签：`@DYN-S<n>` 为场景主键（永久不复用）；`@auto:e2e` 由脚本验证（默认 `local-dev/dynamic-plugin-e2e.mjs`，博查成功路径与响应解析为 `local-dev/bocha-search-e2e.mjs`，墨迹天气为 `local-dev/moji-weather-e2e.mjs`），`@manual` 人工走查。

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

  @DYN-S25 @auto:e2e
  Scenario: JavaScript 执行器内置模板出现在模板下拉数据源中
    Given 动态插件模块已挂载宿主并完成程序集扫描
    When 管理员查询插件注册表
    Then 返回项中包含 javascript_executor 且为动态模板
    And 该项带 JavaScriptCode 配置示例、含 Parameters 的请求参数示例与配置类型

  @DYN-S26 @auto:e2e
  Scenario: JavaScript 执行器返回不同类型的 JS 值时被正确归一
    Given 管理员已用模板 javascript_executor 创建实例并填入合法 JavaScript 代码
    When 依次以对象/字符串/数字/布尔/数组/null/undefined 的 JS 返回值运行该实例
    Then 运行成功且 dataJson 反映各次执行的 ResultKind（object/string/number/boolean/array/null/undefined）
    And 对象/数组的 ResultJson 为对应结构的 JSON 文本
    And 标量值的 ResultJson 为字符串化的标量（数字按整数或 round-trip 输出）

  @DYN-S27 @auto:e2e
  Scenario: JavaScript 执行器配置或脚本不合规
    Given 管理员正在用模板 javascript_executor 创建实例
    When 配置中的 JavaScriptCode 为空后运行该实例
    Then 运行结果为失败并提示 JavaScript 代码不能为空
    And 配置合法但脚本未定义 run 函数时返回失败并提示必须定义 run(parameter)
    And 配置合法但脚本存在语法错误或运行时错误时返回失败并给出可读错误信息（非服务端异常）

  @DYN-S28 @auto:e2e
  Scenario: JavaScript 执行器取消与结果大小可读
    Given 管理员已用模板 javascript_executor 创建实例并填入合法 JavaScript 代码
    When 以合法 Parameters 运行该实例
    Then 运行结果成功且 Parameters 字段回显本次请求入参
    And ResultKind 与 ResultJson 一一对应；null/undefined 的 ResultJson 为 null
    And 引擎错误信息被 PluginExecutor 归一为 Success=false 的运行结果，且不抛服务端 500

  @DYN-S29 @auto:e2e
  Scenario: SQL 只读查询内置模板出现在模板下拉数据源中
    Given 动态插件模块已挂载宿主并完成程序集扫描
    When 管理员查询插件注册表
    Then 返回项中包含 postgres_query 与 mysql_query 且均为动态模板
    And 两项均带 Host/Port 等离散字段配置示例、含 Sql 的请求参数示例与各自的配置类型

  @DYN-S30 @auto:e2e
  Scenario: 只读守卫拒绝写操作与多条语句，放行合法只读语句
    Given 管理员已用模板 postgres_query 与 mysql_query 创建实例
    When 依次提交 UPDATE、DELETE、INSERT、TRUNCATE、DROP、ALTER、多条语句、WITH 内嵌 DELETE、SELECT INTO、SET 会话变量、行注释后跟写操作、MySQL 可执行注释等 SQL
    Then 每次运行结果均为失败且提示只允许执行只读 SQL
    And 拒绝发生在建立数据库连接之前，因而与数据库是否可达无关
    And MySQL 实例同样拒绝写操作
    When 依次提交 SELECT、字符串或引号标识符中含写关键字、WITH 查询、SHOW 与带尾随分号的只读语句
    Then 守卫全部放行，失败原因变为数据库连接失败而非只读拒绝

  @DYN-S31 @auto:e2e
  Scenario: SQL 只读实例的参数或配置不合规
    Given 管理员已创建 SQL 只读实例
    When 请求中的 Sql 为空后运行、或配置中的 Host 为空后运行
    Then 运行结果为失败并分别提示 SQL 不能为空、数据库主机地址 Host 不能为空
    And 均不经由实例化失败

  @DYN-S32 @auto:e2e
  Scenario: PostgreSQL 只读查询成功路径（需 PG_E2E_CONNECTION）
    Given 环境变量提供可达的 PostgreSQL 连接串，解析为 Host/Port/Database/Username/Password 离散配置后创建实例（MaxRows 为 3）
    When 查询 5 行数据
    Then 运行成功、返回 3 行且 Truncated 为 true，Columns 与行数据齐全
    And 会话处于只读事务（SHOW default_transaction_read_only 返回 on）
    And 同名列自动去重、bytea 以 Base64 返回、jsonb 与时间类型可序列化
    And 空结果集返回零行且仍带列名

  @DYN-S33 @auto:e2e
  Scenario: MySQL 只读查询成功路径（需 MYSQL_E2E_CONNECTION）
    Given 环境变量提供可达的 MySQL 连接串，解析为离散配置后创建实例
    When 执行 SELECT 1 并查询会话只读变量
    Then 运行成功且返回一行
    And 会话被设为只读

  @DYN-S34 @manual
  Scenario: SQL 只读插件的配置界面与运行抽屉
    Given 管理员在插件管理页用 postgres_query 或 mysql_query 新建实例
    When 打开配置编辑器与运行抽屉
    Then 配置示例含 Host/Port/Database/Username/Password/MaxRows/CommandTimeoutSeconds 及注释说明
    And 参数示例含 Sql 且提示仅允许单条只读语句
```

## Feature: PaddleOCR 系列内置模板

```gherkin
  @DYN-S36 @auto:e2e
  Scenario: PaddleOCR 三个内置模板出现在注册表
    Given 动态插件模块已挂载宿主并完成程序集扫描
    When 管理员查询插件注册表
    Then 返回项中包含 paddleocr_ocr / paddleocr_structure_v3 / paddleocr_vl 三个动态模板
    And 三项均带 ApiUrl+Token 配置示例、含 File 的请求参数示例与配置类型

  @DYN-S37 @auto:e2e
  Scenario: paddleocr_ocr 成功路径与响应解析
    Given 桩服务按官方 /ocr 报文返回 OcrResults
    When 管理员用模板 paddleocr_ocr 创建实例并以 File / FileType 运行
    Then 运行结果成功且 Pages 长度与样例一致
    And 每页 Text 由 prunedResult.rec_texts 按行拼接；OcrImage / InputImage 原样透传 Base64
    And 对外路径 /ocr，Authorization 头为「token {实例配置 Token}」，FileType 透传

  @DYN-S38 @auto:e2e
  Scenario: paddleocr_structure_v3 成功路径与响应解析
    Given 桩服务按官方 /layout-parsing（StructureV3 形态）报文返回带 seal_res_list 的版面结果
    When 管理员用模板 paddleocr_structure_v3 创建实例并以 File / UseSealRecognition 运行
    Then 运行结果成功且 Pages 长度与样例一致
    And 每页 SealTexts 按印章逐条抽取；PrunedResultJson 保留 prunedResult 原文；OutputImages / InputImage 透传
    And 对外路径 /layout-parsing，Authorization 头为「token {实例配置 Token}」

  @DYN-S39 @auto:e2e
  Scenario: paddleocr_vl 成功路径与响应解析
    Given 桩服务按官方 /layout-parsing（VL 形态）报文返回带 markdown 的视觉语言模型结果
    When 管理员用模板 paddleocr_vl 创建实例并以 File / UseLayoutDetection / PrettifyMarkdown 运行
    Then 运行结果成功且 Pages 长度与样例一致
    And 每页 MarkdownText / MarkdownImages / PrunedResultJson / OutputImages / InputImage 字段齐全
    And 对外路径 /layout-parsing，Authorization 头为「token {实例配置 Token}」，且 visualize=true 被强制开启

  @DYN-S40 @auto:e2e
  Scenario: PaddleOCR InitAsync 拒空 ApiUrl
    Given 管理员已用模板 paddleocr_ocr 创建实例
    When 把实例配置改为 ApiUrl="" 后运行该实例
    Then 运行结果为失败并提示 API 地址不能为空
    And 非空 ApiUrl 时仍按原配置成功运行

  @DYN-S41 @auto:e2e
  Scenario: PaddleOCR 上游错误归一
    Given 管理员已用模板 paddleocr_ocr 创建实例但 Token 为空
    When 以合法 File 运行该实例（桩服务对未带 token 的 Authorization 返回 HTTP 401）
    Then 运行结果为失败且不经由实例化失败
    And 错误信息包含上游 HTTP 状态码与响应体（桩返回的 Unauthorized）

  @DYN-S42 @auto:e2e
  Scenario: PaddleOCR 桩服务确实被三个模板访问
    Given PaddleOCR 桩服务监听 /ocr 与 /layout-parsing
    When 管理员依次运行 paddleocr_ocr / paddleocr_structure_v3 / paddleocr_vl 三个实例
    Then 桩服务收到至少一次 /ocr 与至少两次 /layout-parsing 调用
    And 三个模板的 Authorization 头均为「token mock-token」且不互相串扰

## Feature: 插件头像

  @DYN-S43 @auto:e2e
  Scenario: 管理员为插件设置头像并回读
    Given 管理员已通过公开图片直传管线完成一张图片的上传登记
    And 已创建一个动态插件实例
    When 管理员以该图片的存储标识为实例设置头像
    Then 设置成功
    And 管理列表中该实例的头像标识与登记的存储标识一致

  @DYN-S44 @auto:e2e
  Scenario: 未完成登记的文件不能作为头像
    When 管理员以存储中不存在的文件标识设置头像
    Then 设置失败并提示头像文件不存在或未完成上传

  @DYN-S45 @auto:e2e
  Scenario: 仅管理员可设置插件头像
    When 匿名请求或普通用户请求为插件设置头像
    Then 匿名请求返回未认证
    And 普通用户请求返回禁止访问
```

## Feature: 墨迹天气内置模板

```gherkin
  @DYN-S43 @auto:e2e
  Scenario: moji_weather 模板出现在注册表
    Given 动态插件模块已挂载宿主并完成程序集扫描
    When 管理员查询插件注册表
    Then 返回项中包含动态模板 moji_weather
    And 该模板带 AppCode+Token 配置示例与含 CityId/Lat/Lon 的请求参数示例

  @DYN-S44 @auto:e2e
  Scenario: moji_weather 成功路径与响应解析（城市 ID 定位）
    Given 桩服务按云市场样例报文返回实况与逐日预报
    When 管理员用模板 moji_weather 创建实例并以 CityId 运行
    Then 运行结果成功且实况字段（温度/现象/湿度/风向/更新时间）解析正确
    And 逐日预报逐条解析且无法识别的空条目被丢弃
    And 上游解析出的定位城市（省/区名）被返回
    And 请求以表单编码下发 cityId，鉴权头为「APPCODE {实例配置 AppCode}」，Token 随表单下发

  @DYN-S45 @auto:e2e
  Scenario: moji_weather 经纬度定位与可选 Token
    Given 管理员已创建未配置 Token 的 moji_weather 实例
    When 以 Lat+Lon 运行该实例
    Then 运行结果成功
    And 表单下发 lat/lon 且不带 cityId 与 token 字段

  @DYN-S46 @auto:e2e
  Scenario: moji_weather 定位参数缺失被拒
    When 不带任何定位参数运行 moji_weather 实例
    Then 运行结果为失败并提示 CityId 或 Lat+Lon 至少提供一组
    And 只给 Lat 或 Lon 其一时同样被拒
    And 两种缺失情形均不发出上游请求

  @DYN-S47 @auto:e2e
  Scenario: moji_weather 上游错误归一
    Given 桩服务对无效 AppCode 返回 HTTP 401、对特定 cityId 返回信封 code!=0
    When 分别以无效 AppCode 实例与错误 cityId 运行
    Then HTTP 401 归一为可读失败且带上游响应体
    And 信封错误归一为可读失败且带错误码与 msg
```

## Feature: P1 运维三模板（拨测 / Zabbix / Redis）

```gherkin
  @DYN-S50 @auto:e2e
  Scenario: http_probe 拨测三模式与内网防护
    Given 桩服务提供 200/500 两个拨测目标与可连通/不可连通两种端口
    When 管理员创建 AllowPrivateNetwork=false 的实例并对 127.0.0.1 目标拨测
    Then 运行成功但结论为 Ok=false 且错误信息说明内网防护拒绝
    When 以 AllowPrivateNetwork=true 的实例拨测
    Then http 200 目标返回 Ok=true、状态码、正文预览与耗时
    And http 500 目标返回 Ok=false 且状态码即结论、无传输层错误
    And tcp 模式连通端口返回 Ok=true 与远端地址、拒绝端口返回 Ok=false 与原因
    And dns 模式解析 localhost 返回地址列表
    And 非法方法（DELETE）与非法协议（ftp://）在参数校验即被拒绝

  @DYN-S51 @auto:e2e
  Scenario: zabbix_query 双鉴权形态与只读查询
    Given Zabbix JSON-RPC 桩服务同时支持根路径与 /zabbix 子路径部署
    When 以用户名密码实例（body auth）运行 problems
    Then 运行成功且问题解析出严重级名、ISO 时间、持续时长、标签并经 trigger.get 富化主机名
    And SeverityMin 过滤只返回达到级别的条目
    And 会话以 JSON-RPC auth 属性下发且命中子路径端点
    When 以 API 令牌实例（UseHeaderAuth=true）运行 problems
    Then 请求以 Authorization: Bearer 头下发且命中根路径端点
    And version 模式免鉴权返回服务端版本
    And hosts/triggers 模式解析主机接口与问题态触发器
    When 以错误密码实例运行 problems
    Then 登录失败归一为可读错误（username 参数名失败后回退 user）

  @DYN-S52 @auto:e2e
  Scenario: redis_query 只读诊断六模式
    Given Redis RESP2 桩服务实现白名单内的诊断命令
    When 以正确密码实例依次运行 info/dbsize/slowlog/client_list/config_get/key_info
    Then info 按节段解析出 memory/clients 键值且指定 Section 时仅解析该节段
    And dbsize 返回键数量、slowlog 条目含命令/参数/耗时/客户端/时间
    And client_list 每行解析为键值字典、config_get 按通配符返回键值对
    And key_info 返回类型/TTL/长度/内存占用，不存在的键返回 Exists=false
    When 缺失 Key 参数或以错误密码运行
    Then 参数缺失被 400 拒绝、错误密码归一为可读的连接失败

  @DYN-S53 @auto:e2e
  Scenario: P1 三模板出现在注册表
    Given 动态插件模块已挂载宿主并完成程序集扫描
    When 管理员查询插件注册表
    Then http_probe/zabbix_query/redis_query 均为动态模板且配置类型已解析
    And 配置示例分别含 AllowPrivateNetwork/UseHeaderAuth/MaxListItems
    And 参数示例分别含 Url/SeverityMin/Mode
```

## Feature: P2 运维三模板（SSH 处置 / Grafana / SQL Server）

```gherkin
  @DYN-S54 @auto:e2e
  Scenario: ssh_executor 白名单守卫（校验先于连接）
    Given 动态插件模块已挂载宿主并完成程序集扫描
    When 以空白名单实例运行任意命令
    Then 命令被拒绝且提示白名单为空时不执行任何命令
    When 以含分号或反引号的命令运行
    Then 命令被拒绝且提示拼接/替换符号不允许
    When 运行未命中白名单前缀的命令
    Then 命令被拒绝且错误信息含未命中的命令段
    When 白名单含 reboot/mkfs/rm 时分别运行 reboot、mkfs.ext4、rm -rf /
    Then 三者均被灾难级检查拒绝（黑名单与递归删除检查压过白名单）
    When 运行白名单内的普通命令（如 rm -rf /tmp/x、uptime）且目标不可达
    Then 守卫放行进入连接阶段，失败归一为可读的 SSH 连接失败而非参数校验错误

  @DYN-S55 @auto:e2e
  Scenario: grafana_query 注解时间线与健康检查
    Given Grafana HTTP 桩支持子路径部署、Bearer 令牌鉴权与 health 匿名端点
    When 以服务账号令牌实例运行 health
    Then 版本与数据库状态解析正确且请求以 Authorization Bearer 头命中子路径端点
    When 运行 annotations 并以 RFC3339 给定 From、逗号分隔 Tags
    Then 时间归一为毫秒时间戳下发、标签逐个重复下发
    And 注解解析出文本/标签/起止时间（毫秒转 ISO）/面板归属
    When 运行 search 并给定关键字
    Then 请求带 type=dash-db 与 limit，仪表板解析出标题/UID/地址
    When 以错误令牌运行 search
    Then 上游 401 归一为可读失败且带响应体

  @DYN-S56 @auto:e2e
  Scenario: sqlserver_query 只读守卫与连接归一
    Given 动态插件模块已挂载宿主并完成程序集扫描
    When 以 Port=0 的实例运行
    Then InitAsync 校验拒绝并提示端口取值 1-65535
    When 运行 DELETE 或多条语句
    Then SqlReadOnlyGuard 先于连接拒绝并提示只读
    When 运行 SELECT 且目标不可达
    Then 失败归一为可读的数据库连接失败

  @DYN-S57 @auto:e2e
  Scenario: P2 三模板出现在注册表
    Given 动态插件模块已挂载宿主并完成程序集扫描
    When 管理员查询插件注册表
    Then ssh_executor/grafana_query/sqlserver_query 均为动态模板且配置类型已解析
    And 配置示例分别含 CommandWhitelist/Token/TrustServerCertificate
    And 参数示例分别含 Command/Mode/Sql
```

## Feature: P0 运维五模板（Alertmanager / Loki / Kubernetes / 钉钉 / 企微）

```gherkin
  @DYN-S58 @auto:e2e
  Scenario: alertmanager_query 告警与静默
    Given Alertmanager v2 API 桩支持子路径部署与 Bearer 鉴权
    When 以令牌实例运行 alerts 并给定 State=active
    Then 请求以 Bearer 头命中子路径端点且状态过滤参数下发
    And 告警解析出标签/注解/状态/ISO 时间
    When 运行 silences
    Then 静默解析出状态与匹配器（正则匹配器值带 ~ 前缀）
    When 运行 status
    Then 版本、集群状态与成员解析正确
    When 以错误令牌运行 alerts
    Then 上游 401 归一为可读失败

  @DYN-S59 @auto:e2e
  Scenario: loki_query 日志检索
    Given Loki API 桩支持子路径部署与 Basic 鉴权
    When 运行 query_range 并以 RFC3339 给定 Start
    Then 时间归一为 Unix 纳秒下发且 limit 参数下发
    And 日志流解析出流标签与日志行（纳秒转 ISO 时间）
    When 运行 labels 与 label_values
    Then 标签名列表解析正确且 label_values 命中 /label/{name}/values 路径
    When 运行 series 并给定 match[] 选择器
    Then 标签集解析正确
    When 运行 query 且上游返回 status=error 信封
    Then 信封错误归一为可读失败

  @DYN-S60 @auto:e2e
  Scenario: kubernetes_query 只读资源与日志
    Given Kubernetes API 桩（HTTP）下发 Pod/事件/Deployment/节点列表与 Pod 日志文本
    When 运行 pods 并给定 Namespace 与 labelSelector
    Then 请求以 Bearer 头命中命名空间路径且选择器参数下发
    And Pod 解析出阶段/IP/节点/就绪容器数/重启次数
    When 运行 pod_logs 并给定尾部行数与容器名
    Then 日志纯文本透传且参数下发
    When 运行 events/deployments/nodes
    Then 事件（类型/原因/关联对象/次数）、Deployment（副本/镜像）与节点（Ready/版本/IP）解析正确
    When 以无权限令牌运行 pods
    Then 403 归一为权限不足提示

  @DYN-S61 @auto:e2e
  Scenario: dingtalk_webhook_text 加签推送
    Given 钉钉机器人桩对 access_token 与 HMAC 加签做校验
    When 以 Webhook+Secret 实例推送文本并 @ 手机号
    Then 桩验证 timestamp/sign 为正确 HMAC-SHA256 加签
    And 报文为 msgtype=text 且 content/at.atMobiles 下发正确
    When 推送时 @ 所有人
    Then at.isAtAll 下发
    When 以错误 access_token 实例推送
    Then errcode=310000 归一为关键词/加签/白名单设置提示

  @DYN-S62 @auto:e2e
  Scenario: wecom_webhook_text 推送
    Given 企业微信机器人桩按 key 校验
    When 以 Webhook 实例推送文本并 @ 手机号
    Then key 归一命中且 content/mentioned_mobile_list 下发正确
    When 推送时 @ 所有人
    Then mentioned_list 含 @all
    When 以错误 key 实例推送
    Then errcode!=0 归一为可读失败

  @DYN-S63 @auto:e2e
  Scenario: P0 五模板出现在注册表
    Given 动态插件模块已挂载宿主并完成程序集扫描
    When 管理员查询插件注册表
    Then alertmanager_query/loki_query/kubernetes_query/dingtalk_webhook_text/wecom_webhook_text 均为动态模板且配置类型已解析
    And 配置示例分别含 BearerToken/MaxLines/SkipTlsVerify/Secret/WebhookKey
    And 参数示例分别含 State/Query/TailLines/Text/AtMobiles
```

## Feature: ClickStack 查询模板（clickstack_query，HyperDX 对外 API）

> 与 `clickhouse_query`（自由只读 SQL，@DYN-S71~S72）保持分离：ClickStack 部署里内置 ClickHouse 的 8123/9000 通常不发布到宿主机，HyperDX 对外 API 是其唯一查询面，故本模板保留 sources/search/chart 三模式（@DYN-S64~S66），不走自由 SQL。

```gherkin
  @DYN-S64 @auto:e2e
  Scenario: sources 数据源列表与 Bearer 鉴权
    Given ClickStack 对外 API 桩提供 /api/v2/sources（Bearer 鉴权，含 log/metric/session 三类源）
    When 以正确 Personal API Access Key 实例运行 sources
    Then 请求以 Authorization: Bearer 头命中端点
    And log 源解析出 ID/名称/类型/库表/默认列
    And metric 源在无 from.tableName 时回退第一个指标表名
    And session 源的停用态解析为 Disabled=true
    When 以错误 Key 实例运行 sources
    Then 上游 401 归一为可读失败并提示需 Personal API Access Key（非 OTLP Ingestion Key）
    When 以 ftp:// 协议 BaseUrl 的实例运行
    Then BaseUrl 校验在运行时拒绝且提示合法 http/https 形态

  @DYN-S65 @auto:e2e
  Scenario: search 原始检索
    Given ClickStack API 桩提供 /api/v2/search（记录请求体，支持 404/400 错误形态）
    When 以 SourceId/Where/WhereLanguage/Select/显式时间窗运行
    Then 请求体透传 sourceId/where/whereLanguage/select/maxResults/offset/ISO 时间窗
    And 行集按「列名 → 值」解析（字符串/整数/小数/布尔/null/嵌套容错）且 RowCount 正确
    And 返回行数达到上限时 Truncated=true
    When 不传时间窗运行
    Then 缺省窗口为 End(=now)-15 分钟
    When 给定 MaxResults=50 与 Offset=100
    Then 覆盖配置 MaxRows 下发且未满额不置 Truncated
    When 缺 SourceId、WhereLanguage=regex、SourceId 无效（404）、Where 含桩标记（400）分别运行
    Then 分别得到可读的参数校验错误与上游错误归一

  @DYN-S66 @auto:e2e
  Scenario: chart 时间线聚合
    Given ClickStack API 桩提供 /api/v2/charts/series（记录请求体，granularity 非法返回 400 {error}）
    When 以 SourceId/Where/Granularity/AggFn/GroupBy/显式时间窗运行
    Then 请求体时间窗归一为 epoch 毫秒且 granularity/series（aggFn/where/groupBy 数组）按契约下发
    And 数据点解析出毫秒时间桶（转 ISO）/聚合值/分组值
    When 不传时间窗/AggFn/Granularity 运行
    Then 缺省窗口为 1 小时且缺省 aggFn=count、granularity=1h
    When AggFn=sum 缺 Field、Granularity/AggFn 非法、缺 SourceId 分别运行
    Then 均被 400 拒绝并给出可读提示
```

## Feature: 模板列表页（实例数与卡片新建）

```gherkin
  @DYN-S67 @auto:vitest
  Scenario: 模板列表入口与卡片实例数
    Given 动态插件页与系统插件页的工具栏提供「模板列表」入口（指向系统模板页），团队插件页的入口指向团队模板页
    When 注册表存在动态插件模板且部分模板已创建实例
    Then 模板列表页以卡片展示全部动态插件模板的 key、名称与描述
    And 每张卡片展示该模板已有实例数（系统侧为系统侧实例、不含团队自有实例，团队侧仅本团队自有实例）

  @DYN-S68 @auto:vitest
  Scenario: 卡片「新建」创建实例
    Given 用户在模板列表页点击某张模板卡片的「新建」
    When 弹出创建实例模态，已预选该模板并预填配置示例
    Then 调整实例 Key 与标题后提交，按当前上下文保存实例（管理员建系统实例、团队可管理成员建团队实例）
    And 保存成功后实例数刷新

  @DYN-S69 @auto:vitest
  Scenario: 团队模板列表门禁
    Given 团队模式模板列表按本团队自有实例计数
    When 不可管理成员或非成员访问团队模板列表
    Then 重定向回该团队插件分区
```

## Feature: 团队模板列表页路由与实例 key 自动填充

```gherkin
  @DYN-S74 @auto:vitest
  Scenario: 团队模板列表独立路由与面包屑导航
    Given 团队模板页与系统模板页是两个独立页面（/team/{团队id}/plugin/templates 与 /plugin/templates）
    When 用户在模板列表页点击头部面包屑「动态插件」
    Then 团队模式返回该团队插件分区，系统模式返回系统插件页

  @DYN-S75 @auto:vitest
  Scenario: 团队新建实例 key 自动填充
    Given 团队模式新建实例且已选定模板
    When 实例 Key 尚未被用户手改
    Then 实例 Key 自动填充为「模板key_团队id」，切换模板时联动刷新
    And 用户可手动修改实例 Key，手改后切换模板不再覆盖
    And 系统模式新建不自动填充
```

## Feature: 系统插件页与团队插件的隔离

```gherkin
  @DYN-S70 @auto:e2e
  Scenario: 系统插件页与团队插件的隔离
    Given 团队可管理成员在团队插件页创建了动态插件实例
    When 管理员查询系统插件管理列表
    Then 列表中不出现该团队自有实例
    And 团队插件列表仍可见该实例且标记为团队自有
    And 管理员在系统插件页删除该实例被拒绝
    And 管理员在系统插件页编辑该实例被拒绝
    And 拒绝后团队侧实例保持原样
```

## Feature: ClickHouse 自由只读 SQL（clickhouse_query）

```gherkin
  @DYN-S71 @auto:e2e
  Scenario: 自由只读 SQL 与库表发现工作流
    Given ClickHouse HTTP 桩（8123 形态）以 FORMAT JSON 信封响应（meta 列名/列类型 + data 行）
    When 以单条 SELECT 查询运行
    Then 行集按「列名 → 值」解析且 Columns/ColumnTypes 来自 meta（空结果集仍返回列信息）
    And 返回行数达到 MaxRows 上限时截断丢弃剩余行并置 Truncated
    And 请求强制 readonly=1、max_result_rows=MaxRows+1、result_overflow_mode=break 并带 Basic 鉴权头
    When 以 SHOW DATABASES / SHOW TABLES FROM 库名 / DESCRIBE TABLE / SHOW CREATE TABLE 逐个运行
    Then 摸库、摸表、看列结构与建表语句全部可用（SHOW CREATE 不再被全句 CREATE 关键字扫描误杀）
    When SQL 末尾自带 FORMAT 子句运行
    Then 末尾 FORMAT 被剥离，请求固定 default_format=JSON 且出参解析不受影响

  @DYN-S72 @auto:e2e
  Scenario: 只读防护与错误归一
    Given ClickHouse 实例已配置（BaseUrl/Username/Password/MaxRows）
    When 以 INSERT/UPDATE/DELETE/CREATE/DROP/SET/SYSTEM 语句、url() 等外部源表函数、多语句分别运行
    Then 均在文本守卫层被 400 拒绝且不触达上游（连接层另有 readonly=1 服务端兜底）
    When 上游返回 500（SQL 错误）或 401（凭据错误）
    Then 归一为带 HTTP 状态码与上游正文摘要的可读失败
    When 以空 BaseUrl 实例运行
    Then 运行时返回可读校验失败
```

## Feature: 动态实例的模板下线降级

```gherkin
  @DYN-S73 @auto:vitest
  Scenario: 模板已下线实例的降级展示
    Given 动态插件实例引用的模板已不在注册表（templeteKey 不在模板列表中）
    When 用户查看动态插件实例列表（系统侧与团队侧）
    Then 该行模板列在模板 key 旁显示红色「模板已下线」标记
    And 运行与编辑按钮禁用（不再打开参数示例为空的运行抽屉）
    And 删除按钮仍可用
```

## Feature: Jina 搜索与网页阅读内置模板（jina_search / jina_reader）

> 外部服务商 Jina AI（https://jina.ai/）：搜索走 Search API（s.jina.ai），网页正文提取走 Reader API（r.jina.ai）。
> 两个服务地址均允许配置覆盖（`MoAI:Jina:SearchEndpoint` / `MoAI:Jina:ReaderEndpoint`），桩服务 E2E 按官方报文回放。

```gherkin
  @DYN-S76 @auto:e2e
  Scenario: Jina 搜索成功路径与参数下发
    Given 动态插件模块已挂载宿主并完成程序集扫描
    When 管理员用模板 jina_search 创建实例并以搜索词运行
    Then 请求以 POST 送达搜索端点且请求体 q 为去空格后的搜索词
    And Authorization 头为 Bearer「实例配置中的 API Key」
    And 每条结果解析出标题、链接、摘要与页面正文，UsageTokens 为各条 usage.tokens 求和
    And ReturnFormat 缺省或非法时回落 markdown，NoCache=true 时透传 X-No-Cache 头

  @DYN-S77 @auto:e2e
  Scenario: Jina 网页阅读成功路径
    Given 管理员用模板 jina_reader 创建实例
    When 以网页地址（可带链接/图片摘要开关、ReturnFormat、NoCache、Timeout）运行
    Then 请求体 url 为去空格后的地址且 Accept 为 application/json
    And 解析出标题、地址、摘要、正文、发布时间与 token 用量
    And 摘要开关开启时 Links/Images 按「地址 → 说明」字典解析
    And X-With-Links-Summary / X-With-Images-Summary / X-Return-Format / X-No-Cache / X-Timeout 头按参数透传

  @DYN-S78 @auto:e2e
  Scenario: Jina 插件失败路径归一
    Given 管理员已创建 jina_search / jina_reader 实例
    When 配置中的 API Key 为空后运行
    Then InitAsync 拦截并提示 API Key 不能为空
    When 阅读实例以非 http/https 地址运行
    Then 运行时校验拒绝并提示 Url 必须是完整地址
    When 以无效 API Key 运行
    Then 上游 401 归一为带 HTTP 状态码与响应体的可读失败
    When 上游 HTTP 200 但业务 code 非 200（如额度耗尽）
    Then 归一为带 code 与可读信息的失败而非成功
```

## Feature: 雅虎财经内置模板（yahoo_finance）

> 数据源：雅虎财经公开端点（v8 chart 报价 / v1 search 新闻与代码候选），无需 API Key。
> 数据源地址在实例配置 `BaseUrl`（留空用官方地址，可指向镜像/代理/桩服务）；国内直连官方地址大概率被拦，建议镜像。

```gherkin
  @DYN-S79 @auto:e2e
  Scenario: quote 批量报价成功路径与解析
    Given 动态插件模块已挂载宿主并完成程序集扫描
    When 管理员用模板 yahoo_finance 创建实例（BaseUrl 指向数据源）并以逗号分隔代码运行 quote
    Then 逐代码返回报价：名称/币种/交易所/类型/最新价/昨收/日内高低/成交量/52 周高低
    And 涨跌额与涨跌幅由最新价与昨收计算（百分比保留两位）
    And 行情时间与新闻时间统一转为 UTC ISO 8601
    And 精简 meta（缺日内高低、行情时间等可选字段）容错为空而不报错
    And 请求携带浏览器 User-Agent 与 Accept: application/json（上游反爬要求）
    And IncludeHistory=true 时附带区间收盘序列：空收盘剔除、最多保留最近 30 点、Range/Interval 白名单校验后下发
    And 未开启 IncludeHistory 时不带 range/interval 参数

  @DYN-S80 @auto:e2e
  Scenario: quote 失败隔离与截断
    Given 实例配置 MaxSymbols 生效
    When 批量中混入无效代码（上游 HTTP 200 + chart.error）与上游 500 代码
    Then 两者归入 Errors[]（消息分别含 chart.error 文案与 HTTP 500），好代码照常返回 Quotes
    When 单次提供超过 MaxSymbols 的代码（含重复项）
    Then 去重后按上限截断并置 Truncated=true

  @DYN-S81 @auto:e2e
  Scenario: news 财经新闻成功路径与参数下发
    When 以关键词运行 news 并请求超额条数
    Then 以 GET /v1/finance/search 检索，newsCount 按实例上限 clamp、quotesCount=0
    And 实例配置的 Lang/Region 透传（请求级可覆盖）
    And 新闻解析出标题/发布方/链接/相关代码，空标题条目丢弃，发布时间转 UTC ISO 8601（缺省为空）

  @DYN-S82 @auto:e2e
  Scenario: search 股票代码解析成功路径
    When 以公司名或别名运行 search
    Then 以 GET /v1/finance/search 检索，quotesCount 按参数 clamp、newsCount=0
    And 候选解析出代码/名称（shortname 优先、longname 回落）/交易所（exchDisp）/类型

  @DYN-S83 @auto:e2e
  Scenario: 参数与配置校验
    When 以非法 Mode 运行
    Then 运行时拒绝并提示仅允许 quote/news/search（空白回落 quote）
    When quote 缺 Symbols、news/search 缺 Query、IncludeHistory=true 且 Range/Interval 非法
    Then 均抛 400 并指明缺失或非法字段
    When 实例配置的 BaseUrl 非 http/https 合法地址
    Then InitAsync 拦截并提示 BaseUrl 必须是合法 URL

  @DYN-S84 @auto:e2e
  Scenario: 上游错误归一
    Given news/search 模式调用数据源
    When 上游返回 HTTP 500
    Then 整次运行归一为带 HTTP 状态码与响应体的可读失败
```

## Feature: SMTP 邮件发送（smtp_email_send）

> 协议栈 MailKit；实例配置承载 SMTP 服务器与授权码，Message-ID 由插件预生成并回显。证据：`local-dev/email-plugins-e2e.mjs`（自建 SMTP 桩）。

```gherkin
  @DYN-S85 @auto:e2e
  Scenario: 模板自动进入注册表
    Given 动态插件模块完成程序集扫描
    When 查询插件列表
    Then smtp_email_send 模板出现（isDynamic=true），带 configExample 与 paramsExample

  @DYN-S86 @auto:e2e
  Scenario: 纯文本发送成功
    When 创建实例（Host/Port/Security/Username/Password）并以两个收件人运行
    Then SMTP 桩按序收到 EHLO/AUTH/MAIL FROM/2 个 RCPT TO/DATA
    And 报文含纯文本正文、From 头为「显示名 <发件地址>」
    And 运行结果回显归一化收件人列表与 Message-ID，报文 Message-ID 头与回显一致

  @DYN-S87 @auto:e2e
  Scenario: HTML 正文与抄送密送
    When 以 To(2 个)+Cc+Bcc+ReplyTo+IsHtml=true 运行
    Then SMTP 桩收到 4 个 RCPT（密送计入信封收件）
    And 报文头含 Cc/Reply-To，正文 Content-Type 为 text/html

  @DYN-S88 @auto:e2e
  Scenario: Base64 附件
    When 携带 Attachments[{FileName,ContentBase64}] 运行
    Then 运行结果回显 AttachmentCount 与 AttachmentTotalBytes
    And 报文为 multipart，含 Content-Disposition: attachment 与原始文件内容

  @DYN-S89 @auto:e2e
  Scenario: 参数校验先于网络
    When To 为空 / 含非法地址 / Subject 为空分别运行
    Then 均归一为可读失败并指明字段与非法地址
    And SMTP 桩零命中（校验不建立连接）

  @DYN-S90 @auto:e2e
  Scenario: 认证失败归一
    When 以错误密码运行
    Then 归一为「SMTP 认证失败」可读错误（桩返回 535，桩侧计数确认）

  @DYN-S91 @auto:e2e
  Scenario: 配置运行态校验
    When 实例配置缺 Host 后运行
    Then 运行时拒绝并提示 Host 不能为空（保存实例不跑 InitAsync）
```

## Feature: IMAP 邮箱读取（imap_email_query）

> 协议栈 MailKit；列表模式只取信封摘要，detail 才取全文；唯一写操作是 mark_read。证据：`local-dev/email-plugins-e2e.mjs`（自建 IMAP4rev1 桩，支持 LOGIN/LIST/SELECT/SEARCH/FETCH/STORE 与字面量续行）。

```gherkin
  @DYN-S92 @auto:e2e
  Scenario: 模板自动进入注册表
    When 查询插件列表
    Then imap_email_query 模板出现（isDynamic=true），带 configExample 与 paramsExample

  @DYN-S93 @auto:e2e
  Scenario: recent 最近邮件
    When 以 Count=2 运行 recent
    Then 返回最新 2 封（UID 降序=新→旧），TotalCount=3、Truncated=true
    And 摘要含发件人地址/主题/Seen 旗标/ISO 时间（+08:00）

  @DYN-S94 @auto:e2e
  Scenario: 按中文主题搜索
    When 以 SubjectContains=发票 运行 search
    Then MailKit 以 IMAP 字面量下发中文关键字（桩回 continuation）
    And 仅命中含该关键字的邮件

  @DYN-S95 @auto:e2e
  Scenario: 发件人与起始日期组合
    When 以 FromContains=alice、SinceDate=2026-01-06 运行 search
    Then SINCE 含当天且日期与输入一致（UTC 零点下发）
    And AND 语义仅命中同时满足两条件的邮件

  @DYN-S96 @auto:e2e
  Scenario: 仅未读搜索
    When 以 UnreadOnly=true 运行 search
    Then 仅命中未读邮件

  @DYN-S97 @auto:e2e
  Scenario: detail 单封完整内容
    When 以 Uid 读取带附件的邮件 / 纯文本邮件 / 中文 MIME 邮件
    Then 返回主题/发件人/Message-ID/已读状态/可读正文（HTML 剥标签、Base64 解码、按 MaxBodyChars 截断）
    And 附件只回元信息（文件名/类型/字节数）

  @DYN-S98 @auto:e2e
  Scenario: 标记已读与未读联动
    When mark_read 标记已读 → 未读搜索
    Then 回显 UpdatedUid 与 Unread=false，未读集合联动减少
    When Unread=true 标回未读
    Then 未读集合恢复

  @DYN-S99 @auto:e2e
  Scenario: folders 文件夹列表
    When 运行 folders
    Then 列出 INBOX 与 Archive（FullName 可作为请求 Folder 参数）

  @DYN-S100 @auto:e2e
  Scenario: 参数校验先于网络
    When 未知 Mode / detail 缺 Uid / search 无过滤条件 / 非法 SinceDate 分别运行
    Then 均归一为 400 可读失败（IMAP 桩零命中）

  @DYN-S101 @auto:e2e
  Scenario: 认证失败归一
    When 以错误密码运行 recent
    Then 归一为「IMAP 认证失败（要求授权码）」可读错误（桩 LOGIN NO，桩侧计数确认）

  @DYN-S102 @auto:e2e
  Scenario: 文件夹不存在
    When 以不存在的 Folder 运行 recent
    Then 归一为 400 并提示先用 folders 模式查看可用文件夹
```

## Feature: 秘塔搜索工具集内置模板（metaso_search / metaso_reader）

> 外部服务商秘塔 AI 搜索（https://metaso.cn/）：多范围搜索走 Search API，网页正文提取走 Reader API（同一基址 `https://metaso.cn` 下的 `/api/v1/*`）。
> 服务地址允许配置覆盖（`MoAI:Metaso:Endpoint`，默认官方地址），桩服务 E2E 按官方报文形态回放。证据：`local-dev/metaso-search-e2e.mjs`。

```gherkin
  @DYN-S103 @auto:e2e
  Scenario: 秘塔搜索成功路径与参数下发
    Given 动态插件模块已挂载宿主并完成程序集扫描
    When 管理员用模板 metaso_search 创建实例并以搜索词运行
    Then 请求以 POST 送达 /api/v1/search 且请求体 q 为去空格后的搜索词
    And Authorization 头为 Bearer「实例配置中的 API Key」
    And 网页结果解析出标题、链接、摘要、发布时间、得分、序号与来源原文，文库/视频结果并入同一结果列表
    And Scope 大小写归一（缺省 webpage）、Size 钳制 1-100，IncludeSummary/IncludeRawContent/ConciseSnippet 原样透传
    And IncludeSummary=true 时响应附带综合摘要 Summary，回显 Query/Scope/Total/Credits
    And Scope=image 时结果进 Images（标题/图片地址/缩略图地址），Results 为空

  @DYN-S104 @auto:e2e
  Scenario: 秘塔网页阅读成功路径
    Given 管理员用模板 metaso_reader 创建实例
    When 以网页地址运行且 Format 为 markdown（缺省）
    Then 请求以 POST 送达 /api/v1/reader 且 Accept 为 text/plain，正文为 Markdown 原文，Url 回显去空格后的请求地址
    When 以 Format=json 运行
    Then Accept 为 application/json，解析出标题、地址、作者、日期、正文与点数
    When 以非法 Format 运行
    Then 回落 markdown（Accept text/plain）

  @DYN-S105 @auto:e2e
  Scenario: 秘塔插件失败路径归一
    Given 管理员已创建 metaso_search / metaso_reader 实例
    When 配置中的 API Key 为空后运行
    Then InitAsync 拦截并提示 API Key 不能为空
    When 以空白搜索词 / 非法 Scope / 非 http/https 地址运行
    Then 运行时校验拒绝并指明字段（不发请求）
    When 以无效 API Key 运行
    Then 上游 401 归一为带 HTTP 状态码与响应体的可读失败
    When 上游 429（点数不足/限流）
    Then 归一为带 HTTP 429 与响应体的可读失败而非成功
```

## Feature: 新闻热榜聚合（news_hot_board）

> 无需 API Key 的多平台热榜聚合模板：渠道基类统一补浏览器指纹头（上游网页接口缺 UA 会被风控）并支持可选网关改写（`{Gateway}/https/{域名}/{路径}`，用于镜像/抓包/本地桩），16 渠道按来源并发拉取、按请求顺序合并、来源内补排名。证据：`local-dev/hot-board-e2e.mjs`。

```gherkin
  @DYN-S106 @auto:e2e
  Scenario: 实例创建与同 key 更新
    When 管理员用模板 news_hot_board 创建实例（配置 Limit 与 Gateway）
    Then 创建成功
    When 以相同 key 再次提交
    Then 走更新语义返回 200（配置整体覆盖，与实例管理更新语义一致）

  @DYN-S107 @auto:e2e
  Scenario: 参数校验先于网络
    When 以空 Sources 运行
    Then 归一为 400 并提示全部可选来源
    When Sources 含无法识别的输入（如「抖音号」）
    Then 归一为 400 并原样回显未知输入；中文别名（知乎/b站/稀土掘金等）归一为对应渠道 id 且按出现顺序去重

  @DYN-S108 @auto:e2e
  Scenario: 全渠道并发拉取与合并结构
    When 以 16 个来源（含中文别名重复）运行
    Then 16 渠道全部成功、Errors 为空，别名去重后每来源 3 条共 48 条
    And 每条目含 Source/SourceName/Rank/Title/Url，来源内排名连续 1..n

  @DYN-S109 @auto:e2e
  Scenario: JSON 渠道解析细节
    When 桩服务按各平台官方报文回放后运行
    Then 知乎 detail_text「4589 万热度」换算为数值且问题链接由 api 地址末段拼回
    And 36氪 19 位长整型 itemId 以原始文本进文章链接（精度不丢失）
    And 掘金 content_id 拼文章链接且热度取 content_counter.view
    And 微博词条拼 s.weibo.com 搜索链接并 URL 转义
    And 新浪剥「var data =」壳解析且千分位热度归一
    And 腾讯 id 以 TIP 开头的横幅条目被跳过

  @DYN-S110 @auto:e2e
  Scenario: 结构类渠道解析细节
    When 桩服务按各平台官方报文回放后运行
    Then 百度从 HTML s-data 注释抠出整榜并下钻嵌套子榜、词条拼搜索链接
    And IT之家 AngleSharp 解析 HTML 榜单且短链改写为 www 站文章地址
    And 虎嗅快讯按 br 分段（首段去句号为标题、余段为摘要）
    And B站 nav 取 WBI key 签名 ranking/v2 且 http 封面升级 https、热度取播放量

  @DYN-S111 @auto:e2e
  Scenario: 网关改写与浏览器指纹
    Given 实例配置了 Gateway 指向桩服务
    When 运行全部来源
    Then 16 个上游域名全部改写为 {Gateway}/https/{域名}/{路径} 命中桩服务
    And 请求自动补全浏览器 User-Agent（反爬要求）
    And 抖音先访问 passport 接口取 Set-Cookie 中的 csrf token 并在热榜请求回带 Cookie

  @DYN-S112 @auto:e2e
  Scenario: Limit 每来源截断
    Given 实例配置 Limit=2
    When 以两个来源运行
    Then 每来源仅保留前 2 条且排名仍从 1 连续

  @DYN-S113 @auto:e2e
  Scenario: 单渠道失败隔离
    When 某一渠道上游 500（虎扑）而其余正常
    Then 运行仍成功：正常来源照常返回，失败来源进 Errors 并带「渠道名 + HTTP 状态码」

  @DYN-S114 @auto:e2e
  Scenario: 全来源失败归一
    When 所有渠道均不可达（网关指向无监听端口）
    Then 归一为「全部来源抓取失败：…」的可读失败而非空成功

  @DYN-S115 @auto:e2e
  Scenario: 非法网关被 InitAsync 拦截
    When 实例配置 Gateway 为非 http/https 地址后运行
    Then InitAsync 拦截并提示 Gateway 必须是合法 URL（不发请求）

  @DYN-S116 @auto:e2e
  Scenario: 删除实例后不可运行
    When 删除实例后以其 key 运行
    Then 运行返回 404 插件不存在
```

## Feature: Base64 解码内置模板（base64_decode）

> 纯本地解码模板：输入 Base64 字符串（允许空白换行、支持 data URL 前缀），按内容形态三分输出——文本直接回填、图片/二进制转存公开地址。证据：`tests/MoAI.AIPlugin.Dynamic.Tests/Base64DecodePluginTests.cs`。

```gherkin
  @DYN-S117 @auto:vitest
  Scenario: 文本解码成功路径
    When 以纯 Base64（允许含空白与换行）运行
    Then 内容经严格 UTF-8 判定为文本后回填 Text，回显 ContentType 与完整字节数 SizeBytes
    When 解码文本超过实例配置的 TextMaxLength
    Then 输出被截断并置 TextTruncated（SizeBytes 仍为完整字节数）

  @DYN-S118 @auto:vitest
  Scenario: 图片解码与公开转存
    When 输入为 PNG/JPEG 等图片魔数内容、data:image/png;base64 前缀形式，或 SVG 文本
    Then 内容按内容寻址转存公开图片目录并返回 /static 公开地址与 Markdown 图片引用（可直接在对话中渲染）
    And 同一内容重复解码返回相同地址（幂等去重）

  @DYN-S119 @auto:vitest
  Scenario: 其它二进制转存
    When 输入为无魔数二进制或带 data:application/pdf 类型声明的 Base64
    Then 转存公开文件目录并返回下载链接；有类型声明时信任声明（如 .pdf 扩展名），无声明按通用二进制处理

  @DYN-S120 @auto:vitest
  Scenario: 参数校验与上限护栏
    When 空白输入 / 非法 Base64 / 解码结果超过 MaxDecodeBytes 分别运行
    Then 均归一为 400 可读失败且不发生任何转存
    And 超大输入在解码前即按长度粗估拒绝（不先占内存）
    And 实例配置越界时 InitAsync 钳制（字节数与文本长度各归其界）
```
