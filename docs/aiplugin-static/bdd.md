# 静态插件（StaticPlugin）行为规格（BDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 上游：[../database-scaffold/bdd.md](../database-scaffold/bdd.md) ｜ 证据：[local-dev/static-plugin-e2e.mjs](../../local-dev/static-plugin-e2e.mjs)
> 规范：[../DOC-STANDARD.md](../DOC-STANDARD.md)。设计论证见 SDD，本文只写场景。

## Feature: 静态插件列表（管理员）

### Scenario: 查询静态插件列表合并内存与 DB 并去重

- Given 我是管理员，且内存注册表存在静态插件「static_echo」，DB 无对应记录
- When 我请求查询 plugin kind = static 的列表
- Then 返回「static_echo」，且 classifyName 为空（未分类）、paramsExample 非空、pluginKey 为「static_echo」

@STP-S1 @auto:e2e

### Scenario: DB 有记录时以 DB 信息为准

- Given 「static_echo」已在 DB 存在，且分类为「测试分类」
- When 我请求查询 plugin kind = static 的列表
- Then 返回「static_echo」的 classifyName 为「测试分类」，且不与内存项重复

@STP-S2 @auto:e2e

## Feature: 编辑静态插件写回（管理员）

### Scenario: 首次编辑未入库的静态插件创建记录

- Given 我是管理员，且内存存在静态插件「static_echo」，DB 无记录
- When 我提交{pluginKey:"static_echo",title:"新标题",description:"新描述",classifyId:0}
- Then 返回成功，且刷新列表「static_echo」标题为新标题、分类为未分类

@STP-S3 @auto:e2e

### Scenario: 再次编辑已入库的静态插件更新记录

- Given 我是管理员，且「static_echo」已在 DB 存在
- When 我提交{pluginKey:"static_echo",title:"再改",description:"描述",classifyId:1}
- Then 返回成功，且刷新列表「static_echo」标题为「再改」、分类名称为对应分类名

@STP-S4 @auto:e2e

### Scenario: 指定不存在分类被拒

- Given 我是管理员，且分类「9999」不存在
- When 我提交{pluginKey:"static_echo",classifyId:9999}
- Then 返回 400，提示「分类不存在」

@STP-S5 @auto:e2e

### Scenario: 编辑不存在的静态插件 key 被拒

- Given 我是管理员
- When 我提交{pluginKey:"no_such_plugin",title:"x",classifyId:0}
- Then 返回 404，提示「静态插件不存在」

@STP-S6 @auto:e2e

## Feature: 运行静态插件（管理员）

### Scenario: 运行静态插件成功

- Given 我是管理员，且存在可运行静态插件「static_echo」
- When 我请求运行{key:"static_echo",requestJson:"{\"Message\":\"hi\"}"}
- Then 返回 success=true，且 dataJson 含「hi」

@STP-S7 @auto:e2e

### Scenario: 运行不存在的静态插件被拒

- Given 我是管理员
- When 我请求运行{key:"no_such_plugin",requestJson:"{}"}
- Then 返回 404，提示「插件不存在」

@STP-S8 @auto:e2e

## Feature: 权限与校验

### Scenario: 非管理员访问被拒

- Given 我是普通成员
- When 我请求查询静态插件列表或保存/运行
- Then 返回 403，提示「只有管理员可以管理插件」

@STP-S9 @auto:e2e

## Feature: 内置静态插件

### Scenario: 获取当前时间

- Given 我是管理员，且内置静态插件「static_current_time」已注册
- When 我请求运行{key:"static_current_time",requestJson:"{}"}
- Then 返回 success=true，且 dataJson 的 currentTime 为当前系统时间字符串

@STP-S10 @manual

### Scenario: 流程等待拒绝负数

- Given 我是管理员，且内置静态插件「static_flow_wait」已注册
- When 我请求运行{key:"static_flow_wait",requestJson:"{\"WaitTimeInSeconds\":-1}"}
- Then 返回 success=false，错误信息含「等待时间不能为负数」

@STP-S11 @manual

### Scenario: Markdown 转 HTML

- Given 我是管理员，且内置静态插件「static_markdown_to_html」已注册
- When 我请求运行{key:"static_markdown_to_html",requestJson:"{\"Markdown\":\"# 标题\"}"}
- Then 返回 success=true，且 dataJson 的 html 含「<h1」

@STP-S12 @manual

### Scenario: 文本提取仅接受 http/https 地址

- Given 我是管理员，且内置静态插件「static_text_extract」已注册
- When 我请求运行{key:"static_text_extract",requestJson:"{\"FileName\":\"a.pdf\",\"Url\":\"not-a-url\"}"}
- Then 返回 success=false，错误信息含「Url 必须为合法的 http/https 文件地址」

@STP-S13 @manual

### Scenario: 网页内容抓取默认提取纯文本

- Given 我是管理员，且内置静态插件「static_web_content_fetch」已注册
- When 我请求运行{key:"static_web_content_fetch",requestJson:"{\"Url\":\"https://example.com\"}"}
- Then 返回 success=true，且 dataJson 的 content 为网页纯文本

@STP-S14 @manual

### Scenario: 文件转 Markdown 且自动识别文件名

- Given 我是管理员，且内置静态插件「static_file_to_markdown」已注册，且文件地址路径末段带扩展名
- When 我请求运行{key:"static_file_to_markdown",requestJson:"{\"Url\":\"https://example.com/docs/report.pdf\"}"}
- Then 返回 success=true，且 dataJson 的 fileName 为「report.pdf」、fileType 为对应 MIME 类型、markdown 为转换后的内容

@STP-S15 @manual

### Scenario: 文件转 Markdown 无法识别扩展名时要求补 FileName

- Given 我是管理员，且文件地址路径末段无扩展名且未传 FileName
- When 我请求运行{key:"static_file_to_markdown",requestJson:"{\"Url\":\"https://example.com/download/12345\"}"}
- Then 返回 success=false，错误信息含「无法从 Url 识别文件扩展名」

@STP-S16 @manual

### Scenario: 文件转 Markdown 拒绝不支持的文件类型

- Given 我是管理员，且文件地址文件名后缀为不受支持的类型
- When 我请求运行{key:"static_file_to_markdown",requestJson:"{\"Url\":\"https://example.com/app.exe\"}"}
- Then 返回 success=false，错误信息含「不支持的文件类型」

@STP-S17 @manual
