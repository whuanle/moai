# 动态插件（DynamicPlugin）运维手册（SOP）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 证据：[local-dev/dynamic-plugin-e2e.mjs](../../local-dev/dynamic-plugin-e2e.mjs) ｜ [local-dev/bocha-search-e2e.mjs](../../local-dev/bocha-search-e2e.mjs)
> 规范：[../DOC-STANDARD.md](../DOC-STANDARD.md)。场景编号引用 BDD，不重复步骤。

## 验收流程

启动后端（默认 `:5000`，`cd src/MoAI && dotnet run`）后，二选一：

- 自动（实例管理与失败路径）：`node local-dev/dynamic-plugin-e2e.mjs`（覆盖 [@DYN-S1](./bdd.md#dyn-s1)~[@DYN-S21](./bdd.md#dyn-s21) 中的大部分；`DYN_BASE` 可改地址）。
- 自动（博查成功路径与响应解析，**无需真实 Key**）：`node local-dev/bocha-search-e2e.mjs`。脚本自带 BoCha 桩服务，并拉起一个指向桩服务的独立后端（默认 `:5199`，桩 `:5198`）后跑完整链路。可用 `BSE_BACKEND_PORT`/`BSE_MOCK_PORT` 改端口，`BSE_KEEP_BACKEND=1` 保留后端排查。
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

## 内置模板

- `dynamic_greet`（`DynamicGreetPlugin`）：配置 `{"Prefix":"Hello"}`，请求 `{"Name":"MoAI"}`，用于验证实例化/配置链路。
- `bocha_web_search`（`BoChaWebSearchPlugin`）：配置 `{"ApiKey":"sk-xxxx"}`，请求 `{"Query":"...","Freshness":"noLimit","Summary":true,"Count":10}`；走基础设施层 `IBoChaClient`（Refit）访问 `{MoAI:BoCha:Endpoint}/v1/web-search`。
- `bocha_ai_search`（`BoChaAiSearchPlugin`）：配置同上，请求 `{"Query":"...","Freshness":"noLimit","Include":null,"Count":10,"Answer":true}`；访问 `/v1/ai-search`，返回总结答案、追问问题、参考网页/图片与模态卡。
- `feishu_webhook_text`（`FeishuWebhookTextPlugin`）：配置 `{"WebhookKey":"https://open.feishu.cn/open-apis/bot/v2/hook/<token>","SignKey":""}`，请求 `{"Text":"..."}`；走基础设施层 `IFeishuWebHookClient`（Refit）访问 `https://open.feishu.cn/open-apis/bot/v2/hook/{token}`。

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

## 种子说明

- 动态实例默认无 DB 记录；创建实例后才在 `plugin_dynamic` + `plugin` 表生成记录。
- 内置动态模板：`dynamic_greet`、`bocha_web_search`、`bocha_ai_search`、`feishu_webhook_text`（均在 `MoAI.AIPlugin.Dynamic`）。
