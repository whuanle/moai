# 静态插件（StaticPlugin）运维手册（SOP）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 证据：[local-dev/static-plugin-e2e.mjs](../../local-dev/static-plugin-e2e.mjs)
> 规范：[../DOC-STANDARD.md](../DOC-STANDARD.md)。场景编号引用 BDD，不重复步骤。

## 验收流程

按要求启动后端（:5210）后，执行 [@STP-S1](../aiplugin-static/bdd.md#stp-s1) 至 [@STP-S9](../aiplugin-static/bdd.md#stp-s9)：

1. 登录 admin / abcd123456，进入 `/plugins`，切到「静态插件」Tab（[@STP-S1](./bdd.md#stp-s1) 至 [@STP-S2](./bdd.md#stp-s2)）。
2. 确认列表展示内存发现 + DB 记录合并去重；无 DB 记录的项分类显示「未分类」，且有「运行」「编辑」操作。
3. 点击「运行」→ 右侧抽屉打开，Monaco 展示请求参数示例（[@STP-S7](./bdd.md#stp-s7)）；修改参数点击运行，展示结果。
4. 点击「编辑」→ 弹窗修改标题/描述/分类（[@STP-S3](./bdd.md#stp-s3) 至 [@STP-S4](./bdd.md#stp-s4)）；保存后刷新列表，该插件归入新分类。
5. 非管理员（member）访问被 403（[@STP-S9](./bdd.md#stp-s9)）。

## 常见问题

| 症状 | 原因 | 处理 |
|---|---|---|
| 静态插件显示在「未分类」 | DB 无该插件记录（首次发现） | 点击编辑任意字段写回，即可归入所选分类 |
| 保存报 400 | classifyId 不存在或非法 | 选择已有 plugin 分类，或留 0（未分类） |
| 保存报 404 | pluginKey 不在内存注册表 | 确认该插件已注册（重启后端加载） |
| 运行报 404「插件不存在」 | key 未注册 | 检查 `[AiPlugin]` 注解与 key |
| 运行报 400「请求参数解析失败」 | requestJson 与请求模型不匹配 | 以 Monaco 示例为准修改参数 |

## 种子说明

- 静态插件默认无 DB 记录（无实例）；仅当用户编辑写回后才在 `plugin` + `plugin_static` 表生成记录。
- 内置插件（`MoAI.AIPlugin.Static`，key 统一 `static_` 前缀）：

| key | 说明 | 请求示例 |
|---|---|---|
| `static_echo` | 回显（示例） | `{"Message":"hello"}` |
| `static_javascript_executor` | Jint 执行 JS 的 `run()` | `{"Code":"function run(){return 1;}"}` |
| `static_current_time` | 获取当前系统时间 | `{}` |
| `static_flow_wait` | 等待指定秒数 | `{"WaitTimeInSeconds":10}` |
| `static_markdown_to_html` | Markdown 转 HTML（Markdig） | `{"Markdown":"# 标题"}` |
| `static_text_extract` | 下载 http/https 文件并提取文本 | `{"FileName":"a.pdf","Url":"https://.../a.pdf"}` |
| `static_file_to_markdown` | 下载 http/https 文件转 Markdown，FileName 留空时从 Url 自动识别文件名 | `{"Url":"https://.../report.pdf"}` |
| `static_web_content_fetch` | 抓取网页（默认提取纯文本，AngleSharp） | `{"Url":"https://example.com","ExtractText":true}` |

> 文本提取与文件转 Markdown 依赖 `Maomi.ToMarkdown`（由 `WikiCoreModule` 的 `AddTextExtraction()` 注册），网页抓取依赖 `AngleSharp`，二者包引用在 `MoAI.AIPlugin.Static.csproj`。外部下载复用 infra `IPutClient`。

## 内置插件排障

| 症状 | 原因 | 处理 |
|---|---|---|
| 文本提取报「Url 必须为合法的 http/https 文件地址」 | 传了本地路径或相对地址 | 改为可下载的 http/https 文件地址 |
| 文本提取报「文本提取失败: 不支持的...」 | `FileName` 后缀与内容不符或缺扩展名 | 让 `FileName` 带真实扩展名（如 `.pdf`/`.docx`） |
| 文件转 Markdown 报「无法从 Url 识别文件扩展名」 | Url 路径末段无扩展名且未传 `FileName` | 传带扩展名的 `FileName`，或换可直接指向文件的地址 |
| 文件转 Markdown 报「不支持的文件类型: .xxx」 | 后缀不在 `Maomi.ToMarkdown` 支持范围 | 换受支持格式（pdf/docx/xlsx/pptx/html/md/txt/json）或先转格式 |
| 网页抓取报「抓取网页内容失败/超时」 | 目标站点拒绝、网络不通或超过 10 秒 | 换可达的静态页面；动态渲染页面不做脚本执行 |
| 运行报「请求参数解析失败」 | requestJson 与请求模型不匹配 | 以 Monaco 示例为准修改参数 |
