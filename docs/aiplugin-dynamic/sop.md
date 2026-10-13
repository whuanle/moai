# 动态插件（DynamicPlugin）运维手册（SOP）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 证据：[local-dev/dynamic-plugin-e2e.mjs](../../local-dev/dynamic-plugin-e2e.mjs) ｜ [local-dev/bocha-search-e2e.mjs](../../local-dev/bocha-search-e2e.mjs) ｜ [local-dev/moji-weather-e2e.mjs](../../local-dev/moji-weather-e2e.mjs) ｜ [local-dev/jina-search-e2e.mjs](../../local-dev/jina-search-e2e.mjs) ｜ [local-dev/metaso-search-e2e.mjs](../../local-dev/metaso-search-e2e.mjs)
> 规范：[../DOC-STANDARD.md](../DOC-STANDARD.md)。场景编号引用 BDD，不重复步骤。

## 验收流程

启动后端（默认 `:5000`，`cd src/MoAI && dotnet run`）后，二选一：

- 自动（实例管理与失败路径）：`node local-dev/dynamic-plugin-e2e.mjs`（覆盖 [@DYN-S1](./bdd.md#dyn-s1)~[@DYN-S21](./bdd.md#dyn-s21) 中的大部分；`DYN_BASE` 可改地址）。
- 自动（博查成功路径与响应解析，**无需真实 Key**）：`node local-dev/bocha-search-e2e.mjs`。脚本自带 BoCha 桩服务，并拉起一个指向桩服务的独立后端（默认 `:5199`，桩 `:5198`）后跑完整链路。可用 `BSE_BACKEND_PORT`/`BSE_MOCK_PORT` 改端口，`BSE_KEEP_BACKEND=1` 保留后端排查。
- 自动（墨迹天气成功路径与响应解析，**无需真实 AppCode**）：`node local-dev/moji-weather-e2e.mjs`。脚本自带墨迹桩服务（云市场报文形态），并拉起一个指向桩服务的独立后端（默认 `:5197`，桩 `:5196`）后跑完整链路。可用 `MWE_BACKEND_PORT`/`MWE_MOCK_PORT` 改端口，`MWE_KEEP_BACKEND=1` 保留后端排查；后端基础设施连接取自 `local-dev/system.local.json` 拍平的环境变量。
- 自动（Jina 搜索/网页阅读成功路径与响应解析，**无需真实 Key**）：`node local-dev/jina-search-e2e.mjs`。脚本自带 Jina 桩服务（按请求体含 `q`/`url` 区分搜索与阅读端点），并拉起一个指向桩服务的独立后端（默认 `:5201`，桩 `:5200`）后跑完整链路。可用 `JSE_BACKEND_PORT`/`JSE_MOCK_PORT` 改端口，`JSE_KEEP_BACKEND=1` 保留后端排查。
- 自动（秘塔搜索工具集成功路径与响应解析，**无需真实 Key**）：`node local-dev/metaso-search-e2e.mjs`。前置 `dotnet build src/MoAI/MoAI.csproj -o .builds/metaso`；脚本自带秘塔桩服务（按请求体含 `q`/`url` 区分搜索与阅读端点、按 Accept 头区分阅读 markdown/json 形态），并从 `.builds/metaso` 独立输出拉起一个指向桩服务的独立后端（默认 `:5203`，桩 `:5202`）后跑完整链路。可用 `MSE_BACKEND_PORT`/`MSE_MOCK_PORT` 改端口，`MSE_KEEP_BACKEND=1` 保留后端排查。
- 人工：[@DYN-S1](./bdd.md#dyn-s1) 至 [@DYN-S22](./bdd.md#dyn-s22)，按下列步骤走查：

1. 登录 admin / abcd123456，进入 `/plugin?tab=dynamic`（[@DYN-S1](./bdd.md#dyn-s1) 至 [@DYN-S2](./bdd.md#dyn-s2)）。
2. 点击「新建实例」→ 选择模板、填实例 key/标题/描述/分类、Monaco 填写配置（[@DYN-S3](./bdd.md#dyn-s3)）；实例 key 与模板 key 重名被 409（[@DYN-S4](./bdd.md#dyn-s4)）、同 key 重复提交视为更新（[@DYN-S5](./bdd.md#dyn-s5)）；不合规 key/不存在模板被拒（[@DYN-S6](./bdd.md#dyn-s6) 至 [@DYN-S7](./bdd.md#dyn-s7)）。
3. 编辑实例标题/配置（[@DYN-S8](./bdd.md#dyn-s8)）；确认实例 key 不可改（[@DYN-S9](./bdd.md#dyn-s9)）。
4. 点击「运行」→ 抽屉运行，传入请求参数，返回正确结果（[@DYN-S10](./bdd.md#dyn-s10)）；运行不存在实例提示不存在（[@DYN-S11](./bdd.md#dyn-s11)）。
5. 删除实例成功（[@DYN-S12](./bdd.md#dyn-s12)）；删除不存在实例被 404（[@DYN-S13](./bdd.md#dyn-s13)）。
6. 非管理员（member）访问被 403（[@DYN-S14](./bdd.md#dyn-s14)）。
7. 内置模板走查（[@DYN-S15](./bdd.md#dyn-s15) 至 [@DYN-S24](./bdd.md#dyn-s24)）：模板下拉可见博查两个模板与飞书模板，按「博查搜索排障」「飞书推送排障」建实例运行。

## 常见问题

| 症状 | 原因 | 处理 |
|---|---|---|
| 新建实例报 409 | 实例 key 与注册表模板 key 重名 | 更换实例 key（同 key 重复提交是更新，不会报冲突） |
| 新建实例报 400 | 实例 key 大小写/开头不合规，或分类不存在 | 实例 key 全小写+下划线、≤30；分类留 0 或选已有 |
| 新建/编辑报 404「模板不存在」 | templeteKey 未注册或非动态 | 选择模板下拉中的动态插件 |
| 运行报「插件不存在」 | key 不是已创建实例 key | 先从列表选择实例运行 |
| 无法编辑实例 key | 实例 key 是主键，不可变 | 需删除重建 |
| 运行报「插件实例化失败」 | 插件构造依赖未注册（如 `bocha_web_search` 需 `IBoChaClient`） | 确认宿主已挂载 `InfraExternalHttpModule`（经 `InfraCoreModule`）并重启后端 |
| 博查运行报 HTTP 401 | API Key 无效或未填 | 到 https://open.bocha.cn 的「API KEY 管理」核对后重填实例配置 |
| 博查运行报 HTTP 403 | 博查账号余额不足 | 前往开放平台充值 |
| 博查运行报 HTTP 429 | 触发博查请求频率限制（与充值金额相关） | 降低运行频率或提升配额 |
| 博查返回结果为空但成功 | `freshness` 限定过窄或 `include` 限定了无内容的域名 | 改用 `noLimit` 并清空 `Include`/`Exclude` |
| AI 搜索成功但没有 `Answer`/`FollowUps` | 请求里 `Answer` 传了 `false`（只返回参考源） | 传 `{"Answer":true}` 重新运行 |
| AI 搜索成功但 `ModelCards` 为空 | 该搜索词没有对应垂域模态卡（只有天气/百科/股票等特定词才触发） | 属正常，参考 `WebPages`/`Images` 即可 |
| AI 搜索报错含 `no mock for http://.../v1/ai-search` | 后端被 `MoAI:BoCha:Endpoint` 指向了桩服务但桩未覆盖该路径 | 仅出现在桩服务脚本调试时；正常环境不会出现 |
| AI 搜索用真实 Key 首次调用明显变慢 | `Answer=true` 需博查侧大模型先生成答案 | 需要更快可传 `{"Answer":false}` 只取参考源 |
| 飞书运行报「WebhookKey 不能为空」 | 配置未填或只填了空白字符 | 在实例编辑里填完整 Webhook 地址或 token |
| 飞书运行报 `19001` 地址无效 | 配置里带了查询串、路径斜杠或粘贴时残留换行 | 用「编辑」清空配置重填；plugin 内部已自动从完整地址里截取 token、剔除 `?#/` |
| 飞书运行报 `19021` 签名校验失败 | 机器人开启了「签名校验」但 `SignKey` 没填/填错 | 复制机器人安全设置里的密钥到实例配置 `SignKey`；服务端时间和本地差异过大也会触发 |
| 飞书运行报 `19022` 当前来源 IP 不在白名单 | 服务器出口 IP 没在机器人安全设置里加白 | 把后端服务器出口 IP 加到机器人「IP 白名单」 |
| 飞书运行报 `19024` 未包含关键词 | 机器人开启了「关键词校验」而 Text 里没命中关键词 | 在 Text 里带上机器人配置的关键词，或把关键词校验关掉 |
| 飞书运行报 `9499` 触发限频 | 单机器人推送超过 100 次/分钟（飞书侧硬限） | 降低推送频率、合并消息 |
| Jina 运行报 `code=429` | 免费额度耗尽或触发限流 | 到 https://jina.ai/ 控制台查看 Key 用量与套餐 |
| Jina 搜索成功但 `Results` 为空 | 搜索词过于生僻或限定了过窄的条件 | 换常规关键词或自然语言问题重试 |
| Jina 阅读正文为空但标题正常 | 目标页面无可提取正文（纯脚本渲染失败/反爬） | 加 `Timeout` 重试，或换 `ReturnFormat`；仍不行说明该站不可抓取 |
| 秘塔运行报 HTTP 401 | API Key 无效或未填 | 到 https://metaso.cn/search-api/api-keys 核对后重填实例配置（Key 形如 `mk-xxxx`，只填裸值，插件自动拼 `Bearer `） |
| 秘塔运行报 HTTP 402 | 账户点数不足 | 前往秘塔控制台充值或降低调用频率 |
| 秘塔运行报 HTTP 429 | 触发限流或额度耗尽 | 降低运行频率；`Size` 调小减少消耗 |
| 秘塔搜索报 Scope 仅允许…… | `Scope` 填了合法六项之外的值 | 只能填 webpage/document/paper/image/video/podcast（留空用 webpage） |
| 秘塔搜索图片范围 `Results` 为空 | image 范围结果在 `Images` 字段 | 按 `Images`（标题/图片地址/缩略图）读取 |
| 秘塔搜索没有 `Summary` | 请求 `IncludeSummary` 传了 `false`（不做摘要召回增强） | 传 `{"IncludeSummary":true}` 重新运行 |
| 秘塔阅读 `Title`/`Author` 为空 | `Format` 用了默认 markdown（纯文本形态不带结构化字段） | 传 `{"Format":"json"}` 获取标题/作者/日期结构化字段 |

## 内置模板

- `dynamic_greet`（`DynamicGreetPlugin`）：配置 `{"Prefix":"Hello"}`，请求 `{"Name":"MoAI"}`，用于验证实例化/配置链路。
- `bocha_web_search`（`BoChaWebSearchPlugin`）：配置 `{"ApiKey":"sk-xxxx"}`，请求 `{"Query":"...","Freshness":"noLimit","Summary":true,"Count":10}`；走基础设施层 `IBoChaClient`（Refit）访问 `{MoAI:BoCha:Endpoint}/v1/web-search`。
- `bocha_ai_search`（`BoChaAiSearchPlugin`）：配置同上，请求 `{"Query":"...","Freshness":"noLimit","Include":null,"Count":10,"Answer":true}`；访问 `/v1/ai-search`，返回总结答案、追问问题、参考网页/图片与模态卡。
- `feishu_webhook_text`（`FeishuWebhookTextPlugin`）：配置 `{"WebhookKey":"https://open.feishu.cn/open-apis/bot/v2/hook/<token>","SignKey":""}`，请求 `{"Text":"..."}`；走基础设施层 `IFeishuWebHookClient`（Refit）访问 `https://open.feishu.cn/open-apis/bot/v2/hook/{token}`。
- `moji_weather`（`MojiWeatherPlugin`）：配置 `{"AppCode":"...","Token":""}`，请求 `{"CityId":"285"}` 或 `{"Lat":"39.90598","Lon":"116.39139"}`；走基础设施层 `IMojiWeatherClient`（Refit，表单编码 + `Authorization: APPCODE ...`）访问 `{MoAI:MojiWeather:Endpoint}/whapi/json/aliweather/broadcast`。
- `jina_search`（`JinaSearchPlugin`）：配置 `{"ApiKey":"jina_xxxx"}`，请求 `{"Query":"...","ReturnFormat":"markdown","NoCache":false}`；走基础设施层 `IJinaSearchClient`（Refit）访问 `{MoAI:Jina:SearchEndpoint}/`（默认 `https://s.jina.ai`），返回每条命中的标题/链接/摘要与页面正文。
- `jina_reader`（`JinaReaderPlugin`）：配置同上，请求 `{"Url":"https://...","ReturnFormat":"markdown","WithLinksSummary":false,"WithImagesSummary":false,"NoCache":false,"Timeout":null}`；走 `IJinaReaderClient` 访问 `{MoAI:Jina:ReaderEndpoint}/`（默认 `https://r.jina.ai`），把网页（含 JS 渲染页）转成 LLM 友好正文。
- `metaso_search`（`MetasoSearchPlugin`）：配置 `{"ApiKey":"mk-xxxx"}`，请求 `{"Query":"...","Scope":"webpage","Size":10,"IncludeSummary":false,"IncludeRawContent":false,"ConciseSnippet":false}`；走基础设施层 `IMetasoClient`（Refit）访问 `{MoAI:Metaso:Endpoint}/api/v1/search`（默认 `https://metaso.cn`），按范围检索网页/文库/论文/图片/视频/播客。
- `metaso_reader`（`MetasoReaderPlugin`）：配置同上，请求 `{"Url":"https://...","Format":"markdown"}`；走 `IMetasoClient` 访问 `{MoAI:Metaso:Endpoint}/api/v1/reader`，把网页转成 LLM 友好 Markdown 正文（json 格式附标题/作者/日期）。
- `base64_decode`（`Base64DecodePlugin`）：配置 `{"MaxDecodeBytes":2097152,"TextMaxLength":20000}`，请求 `{"Base64":"...","FileName":"可选.png"}`；纯本地解码（构造注入 `IStorageService`）：文本直接返回，图片/二进制转存 `/static` 公开地址并附 Markdown 引用，支持 data URL 前缀。

### 博查搜索排障

1. 配置示例：`{"ApiKey":"sk-xxxxxxxx"}`（只填 Key 本体，前端会自动拼 `Bearer `；重复带前缀也不会报错）。
2. 运行示例（全网搜索）：`{"Query":"阿里巴巴2024年的ESG报告","Freshness":"noLimit","Summary":true,"Count":10}`。
3. 运行示例（AI 搜索）：`{"Query":"西瓜的功效与作用","Freshness":"noLimit","Count":10,"Answer":true}`。
4. 失败时结果面板给出 `HTTP <状态码>` 与博查响应体，按上表定位（401 密钥 / 403 余额 / 429 限流）。
5. 两个模板都依赖外网与真实 Key，**不纳入 CI**；不带 Key 验证成功路径请用 `node local-dev/bocha-search-e2e.mjs`（桩服务）。
6. 需要把请求指向代理或桩服务时，用配置覆盖上游地址（默认官方地址）：
   `MoAI__BoCha__Endpoint=http://127.0.0.1:5198 dotnet run --project src/MoAI/MoAI.csproj`。

### 飞书推送排障

1. 配置示例：`{"WebhookKey":"https://open.feishu.cn/open-apis/bot/v2/hook/<token>","SignKey":""}`。`WebhookKey` 也可只填最后的 token，plugin 会自动归一；只填 token 时若机器人开启了「签名校验」则必须再配 `SignKey`，否则会得到 `19021`。
2. 运行示例：`{"Text":"MoAI 提醒：本轮任务已全部完成"}`。Text 不能为空。
3. 失败时结果面板给出 `HTTP <状态码>` 或飞书业务码（如 `19001`/`19021`/`9499`），按上表定位；Refit 对非 2xx 的报文会拼到错误信息里，便于核对飞书侧返回内容。
4. 模板不依赖真实可用机器人也能跑失败路径（空 WebhookKey / 空 Text / 占位 token → 飞书返回 `19001`），**纳入 CI**：见 `node local-dev/dynamic-plugin-e2e.mjs` 的 `@DYN-S23`/`@DYN-S24`。

### 墨迹天气排障

1. 配置示例：`{"AppCode":"xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx","Token":""}`（AppCode 只填裸值，插件会自动拼 `APPCODE ` 前缀；重复带前缀也不会报错）。AppCode 在阿里云云市场「已购买服务」控制台查看；部分服务规格要求的访问令牌填 `Token`，不要求的规格留空。
2. 运行示例（城市 ID）：`{"CityId":"285"}`；运行示例（经纬度）：`{"Lat":"39.90598","Lon":"116.39139"}`。`CityId` 与 `Lat`+`Lon`（成对）二选一，全缺或只给其一会得到 400「CityId 或 Lat+Lon 至少提供一组」。
3. 失败时结果面板给出 `HTTP <状态码>` 与上游响应体（401 AppCode 无效、403 未购买/欠费、429 限流），或信封错误 `墨迹天气返回错误（code=…）：…`；按提示核对 AppCode/Token 与购买规格。
4. 模板依赖外网与真实 AppCode，**不纳入 CI**；不带 AppCode 验证成功路径请用 `node local-dev/moji-weather-e2e.mjs`（桩服务）。
5. 需要把请求指向代理或桩服务时，用配置覆盖上游地址（默认阿里云云市场网关）：
   `MoAI__MojiWeather__Endpoint=http://127.0.0.1:5196 dotnet run --project src/MoAI/MoAI.csproj`。
6. 上游实况/预报字段以购买规格的实际返回为准；若发现字段缺失或形态不同（插件已做多形态容错），按 [sdd.md](./sdd.md) `moji_weather` 细节核对解析字段表。

### Jina 搜索/网页阅读排障

1. 配置示例：`{"ApiKey":"jina_xxxxxxxxxxxx"}`（Key 在 https://jina.ai/ 控制台获取，只填 Key 本体，插件会自动拼 `Bearer `；重复带前缀也不会报错）。
2. 运行示例（搜索）：`{"Query":"latest developments in quantum computing"}`；运行示例（阅读）：`{"Url":"https://jina.ai/reader","WithLinksSummary":true}`。`Query`/`Url` 不能为空，`Url` 必须是 http/https 完整地址。
3. `ReturnFormat` 支持 markdown/text/html/screenshot/pageshot，填其它值回落 markdown；`Timeout` 为抓取超时秒数，不填用服务端默认。
4. 失败时结果面板给出 `HTTP <状态码>` 与 Jina 响应体（401 密钥无效、429 限流/额度耗尽），或信封错误 `Jina xxx 调用失败（code=…）：…`，按提示到 Jina 控制台核对 Key 与额度。
5. 两个模板依赖外网与真实 Key，**不纳入 CI**；不带 Key 验证成功路径请用 `node local-dev/jina-search-e2e.mjs`（桩服务）。
6. 需要把请求指向代理或桩服务时，用配置覆盖上游地址（默认官方地址）：
   `MoAI__Jina__SearchEndpoint=http://127.0.0.1:5200 MoAI__Jina__ReaderEndpoint=http://127.0.0.1:5200 dotnet run --project src/MoAI/MoAI.csproj`。

### 秘塔搜索/网页阅读排障

1. 配置示例：`{"ApiKey":"mk-xxxxxxxxxxxxxxxxxxxxxxxx"}`（Key 在 https://metaso.cn/search-api/api-keys 获取，只填 Key 本体，插件会自动拼 `Bearer `；重复带前缀也不会报错）。
2. 运行示例（搜索）：`{"Query":"量子计算最新进展","IncludeSummary":true,"IncludeRawContent":true}`；运行示例（图片）：`{"Query":"雪山日出","Scope":"image"}`；运行示例（阅读）：`{"Url":"https://example.com/article","Format":"json"}`。`Query`/`Url` 不能为空，`Url` 必须是 http/https 完整地址。
3. `Scope` 支持 webpage/document/paper/image/video/podcast，留空或大小写混排归一后使用，其它值直接 400；`Size` 自动钳制 1-100。
4. 失败时结果面板给出 `HTTP <状态码>` 与秘塔响应体（401 密钥无效、402 点数不足、429 限流/额度耗尽），按提示到秘塔控制台核对 Key 与点数。
5. 两个模板依赖外网与真实 Key，**不纳入 CI**；不带 Key 验证成功路径请用 `node local-dev/metaso-search-e2e.mjs`（桩服务，前置 `dotnet build src/MoAI/MoAI.csproj -o .builds/metaso`）。
6. 需要把请求指向代理或桩服务时，用配置覆盖上游地址（默认官方地址）：
   `MoAI__Metaso__Endpoint=http://127.0.0.1:5202 dotnet run --project src/MoAI/MoAI.csproj`。

### 新闻热榜排障

1. 配置示例：`{"Limit":20,"Gateway":""}`（无需任何 API Key；`Limit` 为每来源条数上限 1-50；`Gateway` 仅调试/镜像用，留空直连各平台官方地址）。
2. 运行示例：`{"Sources":["zhihu","weibo","36kr"]}`；也接受中文名（如 `["知乎","微博","36氪","掘金","头条"]`）。全量可选值见运行参数描述，未知输入会 400 回显。
3. 响应 `NewsList[]` 每条含 Source/SourceName/Rank/Title/Url/Desc/Cover/HotValue；`Errors[]` 非空表示有来源失败（渠道名 + HTTP 状态/原因），其余来源照常返回；全部失败才整体报错（502）。
4. 单渠道报错多为平台风控或榜单结构变化（这些是网页端接口，无官方契约）：先看 `Errors` 里的渠道名与 HTTP 状态（403/429 多为风控），其余来源不受影响；持续失败再对照 `HotBoard/Channels/<渠道>.cs` 核对上游结构。
5. 成功路径验证（含 16 渠道解析/截断/失败隔离）用桩服务：`node local-dev/hot-board-e2e.mjs`（前置 `dotnet build src/MoAI/MoAI.csproj -o .builds/hotboard`）。真实上游直连已验证可用（国内网络），无需 Key、无费用。

### Base64 解码排障

1. 配置示例：`{"MaxDecodeBytes":2097152,"TextMaxLength":20000}`（无需任何 Key；字节数钳制 1024-32MB、文本长度钳制 100-200000，越界自动归界）。
2. 运行示例（文本）：`{"Base64":"SGVsbG8sIE1vQUkh"}`；运行示例（图片）：`{"Base64":"data:image/png;base64,iVBORw0KGgo...","FileName":"shot.png"}`。输入允许含空白换行，data URL 前缀可省可留。
3. 文本形态回 `Text`（超长截断置 `TextTruncated`）；图片/二进制回 `Url`（/static 公开地址）与 `Markdown` 引用，同内容重复解码地址不变（内容寻址幂等）；`Kind` 区分 text/image/binary。
4. 报「不是有效的 Base64」多为粘入了非编码内容或长度不对（须 4 的倍数）；报「超过上限」可在实例配置调大 `MaxDecodeBytes`（上限 32MB）；解码失败不会产生任何转存文件。
5. 成功路径验证（单元级，无需后端）：`dotnet test tests/MoAI.AIPlugin.Dynamic.Tests --filter Base64DecodePluginTests`。

## 种子说明

- 动态实例默认无 DB 记录；创建实例后才在 `plugin_dynamic` + `plugin` 表生成记录。
- 内置动态模板：`dynamic_greet`、`bocha_web_search`、`bocha_ai_search`、`feishu_webhook_text`、`moji_weather`、`jina_search`、`jina_reader`、`metaso_search`、`metaso_reader`、`news_hot_board`、`base64_decode`（均在 `MoAI.AIPlugin.Dynamic`）。
