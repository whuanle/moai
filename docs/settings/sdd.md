# 系统设置（Settings）设计规格（SDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 上游：[../account/sdd.md](../account/sdd.md)（用户态与门禁依赖） ｜ 下游：[../wiki/sdd.md](../wiki/sdd.md)（知识图谱能力消费方，规划中） ｜ 规范：[../settings.md](../settings.md)（设置项编写规范） ｜ 证据：[local-dev/audit-345.mjs](../../local-dev/audit-345.mjs)
> 规范：[../DOC-STANDARD.md](../DOC-STANDARD.md)。行为场景见 BDD（@SET-Sxx），本文不重复。

## 目标

一组**固定**的系统配置项（当前内置知识图谱组：`OPEN_NEO4J`/`NEO4J_URI`/`NEO4J_USERNAME`/`NEO4J_PASSWORD`，默认 `"false"`/空）。定义集中在 `SettingDefinitions` 注册表（单一事实来源），可调整的只有**值**。提供查询全部设置项与保存单个设置项两个接口，供前端 `/settings` 使用。

知识图谱组面向 Neo4j 知识图谱能力：超级管理员在系统设置页开启 `OPEN_NEO4J` 后方可填写 `NEO4J_URI`/`NEO4J_USERNAME`/`NEO4J_PASSWORD`；关闭时不提交连接信息。业务模块（知识库）通过 `IKnowledgeGraphSettingsService` 感知开关与连接配置，不直接查表。

角色门禁（只在 Controller 层判断，禁止下沉 Handler）：**查询**要求 admin+；**保存**仅 root。root 判定为 `setting` 表 `key="root"` 的 value = 用户 id（种子 id=1 即 admin）。

## 组件

```
src/settings/
├── MoAI.Settings.Shared/  Commands/SaveSettingCommand({key,value})、UpdateSystemLogoCommand({objectKey})、UpdateLoginBackgroundCommand({objectKey})、SendTestEmailCommand({to})
│                          Queries/QuerySettingsCommand + Responses（SettingItemResponse：Key/Name/Description/Value）
│                          Models/Neo4jKnowledgeGraphSettings、SandboxLimitsSettings、EmailSmtpSettings（含 EmailSmtpSecurity 枚举）、EmailMessage/EmailSendResult
│                          + Services/ISettingsService、IKnowledgeGraphSettingsService、IWikiSettingsService、ISandboxSettingsService、IEmailSettingsService、IEmailSenderService
├── MoAI.Settings.Core/    Handlers/{Query,Save}SettingHandler、UpdateSystemLogoCommandHandler、UpdateLoginBackgroundCommandHandler（文件登记校验后委托 SaveSettingAsync）、SendTestEmailCommandHandler（委托 IEmailSenderService）
│                          + Services/SettingsService（校验与读写）
│                          + Services/KnowledgeGraphSettingsService、WikiSettingsService、SandboxSettingsService（成组读取）
│                          + Services/EmailSettingsService（SMTP 八项成组读取，未启用/未配置短路 Enabled=false）+ EmailSenderService（MailKit 发送，15s 超时护栏，认证可选）
└── MoAI.Settings.Api/     SettingsController（[Route("/settings")]，门禁在此，含 POST /settings/logo、POST /settings/login-background、POST /settings/email/test）
src/common/…/Queries/      QueryServerInfoCommandHandler（附加读取 SYSTEM_LOGO → logoPath、SYSTEM_NAME → name、LOGIN_BACKGROUND → loginBackgroundPath，匿名可读，空名称回退 SystemOptions.Name）
src/database/…/Seed/       SettingDefinition(s)/SettingSeed（注册表 + 种子：{Id=1, key="root", value="1"}）
                           SettingConfiguration（key≤50 哈希索引、name 20、description 255、value 2000）
ui/src/                    api/settings.ts（SettingKeys 常量 + get/save + updateSystemLogo/resetSystemLogo + updateLoginBackground/resetLoginBackground + sendTestEmail）
                           layouts/useSystemLogo.ts + components/AppLogo.tsx（全局 Logo 解析与展示；useSystemName 取网站名称；useLoginBackgroundSrc 取登录页背景）
                           pages/settings/Settings.tsx（/settings，卡片自上而下：网站信息（网站名称、网站 Logo、登录页背景图三分区，可折叠）→ 知识库 → 知识图谱（图数据库）→ 邮件服务（SMTP，含注册验证码开关、四个验证码邮件模板与测试邮件入口）→ 沙箱资源上限）
                           pages/auth/Login.tsx（登录页整页背景：有背景图铺满 cover，无则默认渐变）
```

## API 契约

| 方法 | 路由 | 门禁 | 说明 |
|---|---|---|---|
| GET | `/api/settings` | `IsAdmin`（root 隐含满足），否则 403「只有管理员可以访问设置项」 | 返回全部内置项 `{key,name,description,value}` |
| PUT | `/api/settings` | 仅 `IsRoot`，否则 403「只有超级管理员可以修改设置项」 | `{key, value}` 均 string |
| POST | `/api/settings/logo` | 仅 `IsRoot`，否则 403「只有超级管理员可以修改网站 Logo」 | `{objectKey}`，空串恢复默认；非空须为已登记上传的文件，否则 404（[@SET-S25](./bdd.md#set-s25)、[@SET-S28](./bdd.md#set-s28)） |
| POST | `/api/settings/login-background` | 仅 `IsRoot`，否则 403「只有超级管理员可以修改登录页背景图」 | `{objectKey}`，语义同 Logo：空串恢复默认；非空须为已登记上传的文件，否则 404（[@SET-S36](./bdd.md#set-s36)、[@SET-S39](./bdd.md#set-s39)） |
| POST | `/api/settings/email/test` | 仅 `IsRoot`，否则 403「只有超级管理员可以发送测试邮件」 | `{to}` 收件地址；使用**已保存**的 SMTP 配置发送测试邮件，未启用/未配置 400、连接或认证失败 502/504（[@SET-S50](./bdd.md#set-s50)~[@SET-S53](./bdd.md#set-s53)） |

公开读取：网站 Logo 经 `GET /api/common/serverinfo` 的 `logoPath` 字段暴露（匿名可读，登录/注册页依赖，[@SET-S24](./bdd.md#set-s24)）；登录页背景图经同端点 `loginBackgroundPath` 字段暴露（[@SET-S35](./bdd.md#set-s35)）。

认证由 `ApiApplicationModelConvention` 自动追加 `[Authorize]`。

知识图谱组为逐 key 读写（无批量接口）：前端保存时先写 `OPEN_NEO4J`，开启状态下再依次写 `NEO4J_URI`/`NEO4J_USERNAME`/`NEO4J_PASSWORD`；关闭时不写连接项（[@SET-S17](./bdd.md#set-s17)）。

## 关键决策

1. **查询合并默认值**：以 `SettingDefinitions.All` 为基准遍历，库有记录用记录值、无记录返回 `DefaultValue`——GET 永远返回全部内置项，不因种子缺失漏项。
2. **保存校验**：`SettingDefinitions.Find(key)`（忽略大小写）未命中 400「无效的配置项.」；库中无记录时自动以内置 Key/Name/Description 插入新行（首次写入自动建行，[@SET-S8](./bdd.md#set-s8)），有记录仅更新 value。
3. **`key="root"` 系统级保护**：不在白名单内，不能经设置接口读写（保存得 400），只能改库维护（[@SET-S6](./bdd.md#set-s6)）。
4. **value 约定**：字符串存储（可承载 JSON），布尔统一 `"true"`/`"false"`；`setting` 表无应用层缓存，**写入即刻生效于下一个读请求**。
5. 门禁依赖 `IUserAccountService.GetUserStateAsync`（Redis `userstate:{userId}` 1h，[../account/sdd.md](../account/sdd.md)）；root/admin 变更后需失效缓存才即时生效（user-management 写操作已负责）。
6. 前端**固定字段渲染**（非动态表单）：新增设置项须同步改 `Settings.tsx` 与 i18n（步骤见 [../settings.md](../settings.md)）；当前页面唯一内容为 root 专属知识图谱卡片，非 root（含普通 admin）整页重定向 /apps 应用广场（后端 PUT 亦仅 root，为最终防线）；脏检查控制保存按钮，保存失败重新加载回滚为库值。
7. **知识图谱组语义**：`OPEN_NEO4J="true"` 才认为能力开启；读取经 `KnowledgeGraphSettingsService` 归一（未开启直接返回 `Enabled=false`，不回传连接信息），连接项值经 `SettingDefinitions.Find` 兜底默认空串。密码明文存 `setting.value`（≤2000），与现有设置项一致，不做加解密。
8. **能力暴露而非实现**：本模块只提供开关与连接配置的读写及 `IKnowledgeGraphSettingsService` 读取出口；Neo4j 驱动、图谱抽取与检索属知识库后续迭代，不在本模块内。
9. **沙箱资源上限组（2026-09-19）**：`SANDBOX_MAX_TTL_SECONDS`（60~604800，默认 86400）/`SANDBOX_MAX_CPU`（K8s 数量，默认 "4"）/`SANDBOX_MAX_MEMORY`（默认 "8Gi"）。保存时 `SaveSettingAsync` 对这三项做格式校验（非法 400，防止脏上限卡住全部应用的沙箱配置保存）；业务经 `ISandboxSettingsService.GetLimitsAsync` 读取（含解析后的毫核/字节，缺失或非法回退内置默认）。K8s 数量解析器 `SandboxQuantity` 放在 `Settings.Shared/Models`，供 app 模块保存校验复用。强校验行为见 [@AP-S48](../app/bdd.md#ap-s48)。
10. **网站 Logo（2026-09-20）**：`SYSTEM_LOGO` 存图片 ObjectKey（复用头像上传管线 `pre_upload_image` → 直传 → `complate_url`）。写路径走专用 `POST /api/settings/logo`（root 门禁）：非空 ObjectKey 须在 `file` 表已登记 `IsUploaded`（防伪造，404），空串恢复默认；落库复用 `SaveSettingAsync`（白名单 + 首写建行）。读路径为**匿名公开**的 `serverinfo.logoPath`（登录/注册页在认证前也要展示 Logo），Common 模块直查 `setting` 表，不经本模块接口；缺失回退默认空串。前端 `AppLogo` 组件（`useSystemLogoSrc`）统一解析展示：有自定义 Logo 用 `{serviceUrl}/static/{objectKey}`，否则回退 `/logo.svg`；设置页上传/恢复成功后即时刷新全局 serverInfo（应用每次加载也会刷新一次，保证 Logo 变更不滞留本地快照）。
11. **网站名称（2026-09-20，仅前端展示）**：`SYSTEM_NAME` 为纯展示配置，走通用 `PUT /api/settings`（root 门禁 + 白名单 + 首写建行），不建专用端点；保存校验去空白后 ≤50 字符（`SettingsService.ValidateValue`，超长 400，[@SET-S32](./bdd.md#set-s32)）。读路径同 Logo：`serverinfo.name` 匿名公开，Common 直查 `setting` 表，**空值回退配置文件 `SystemOptions.Name`**（前端再兜底 i18n `app.name`）。前端 `useSystemName` 统一取值，消费方（2026-10-28 扩展）：侧边栏标题、**登录/注册页卡片主标题（[@SET-S43](./bdd.md#set-s43)）**、浏览器标签页 `document.title`——登录/注册页在 AppLayout 之外，各自同步标题；设置页保存成功后刷新 serverInfo 即时生效（[@SET-S31](./bdd.md#set-s31)、[@SET-S34](./bdd.md#set-s34)）。存量库补种见 `asserts/setting.sql`。
12. **登录页背景图（2026-10-28）**：`LOGIN_BACKGROUND` 存图片 ObjectKey，全链路复刻网站 Logo——复用 `pre_upload_image` 上传管线与文件登记防护（伪造 404），专用端点 `POST /api/settings/login-background`（root 门禁，空串恢复默认），读路径为匿名公开的 `serverinfo.loginBackgroundPath`（登录页在认证前渲染背景）。前端 `useLoginBackgroundSrc` 解析后由登录页整页渲染（`url() center/cover` 铺满），空值回退默认渐变（token `colorBgLayout → colorPrimaryBg`）；设置页卡片提供宽幅预览即上传入口与「恢复默认」，上传/恢复后刷新 serverInfo 即时生效（[@SET-S35](./bdd.md#set-s35)~[@SET-S42](./bdd.md#set-s42)）。存量库补种见 `asserts/setting.sql`。
13. **知识库允许上传的文件类型（2026-10-29）**：`WIKI_ALLOWED_FILE_TYPES`（逗号分隔扩展名，默认 `.pdf,.docx,.xlsx,.pptx,.html,.htm,.md,.markdown,.txt,.json`——按 Maomi.ToMarkdown 1.0.0-alpha.2 实际提取能力对齐：pdf/office 三件套 x 版/html/markdown/txt/json，图片暂不支持）。保存时 `SettingsService.ValidateValue` 经 `WikiFileTypes` 严格校验并**归一落库**（小写去重、每项 `.` 开头 1~16 位字母数字、≤100 项，非法 400）；读取经 `IWikiSettingsService.GetAllowedFileTypesAsync`（设置缺失、为空或全部非法回退内置默认，永不允许空白名单卡死上传）。消费端：wiki 网页端与外部开放接口两个 PreUpload Handler 以设置值替换原 `FileStoreHelper.DocumentFormats` 硬编码（对话附件等其他上传口不受影响，`DocumentFormats` 保留）；`GET /api/wiki/upload-limit` 响应新增 `allowedFileTypes`，前端 `WikiDocuments` 动态生成 accept 与选择预检（读取失败回退内置默认）。配套提取兜底 `WikiTextFileReader`（wiki.Core）：包不识别的扩展名（管理员放开的 .csv/.sh/.cs 等文本类文件）按 UTF-8 文本读取（自动识别 BOM），起始 8KB 含 NUL 字节视为二进制拒绝提取；**源流不要求可 Seek**（AWS SDK `GetObjectResponse.ResponseStream` 不可 Seek，兜底整体 `CopyToAsync` 进内存后处理）。E2E：[@SET-S44](./bdd.md#set-s44)~[@SET-S46](./bdd.md#set-s46)、wiki 侧 [@WK-S43](../wiki/bdd.md#wk-s43)（WK-12/WX-11）。存量库补种见 `asserts/setting.sql`。
14. **SMTP 邮件服务组（2026-10-29）**：`SMTP_ENABLED`（默认 "false"）/`SMTP_HOST`/`SMTP_PORT`（默认 "465"）/`SMTP_SECURITY`（ssl/starttls/none，默认 ssl）/`SMTP_USERNAME`/`SMTP_PASSWORD`/`SMTP_FROM_ADDRESS`/`SMTP_FROM_NAME`。保存时 `SettingsService.ValidateValue` 做格式校验并归一（开关与加密方式小写落库，端口 1~65535，发件人经 `MailAddress.TryCreate` 校验，主机/账号/密码/显示名长度上限，非法 400）。读取经 `IEmailSettingsService.GetAsync`（`Settings.Shared` 接口 + `EmailSettingsService` 实现）：未启用**或**服务器地址为空时短路返回 `Enabled=false`；端口/加密缺失或非法回退默认；发件人留空回退登录账号。通知发送经 `IEmailSenderService.SendAsync`（`EmailSenderService`，MailKit 4.18.1，Settings.Core 新增包引用）：按加密方式映射 `SecureSocketOptions`，**账号非空才认证**（内网免认证中继可留空），连接/认证/发送统一 15s 超时，MailKit 异常翻译为业务异常（认证 502、TLS 握手 502、超时 504），Message-ID 预生成便于收件侧核对；业务模块只需引用 `MoAI.Settings.Shared` 注入两个接口即可接入通知，实现经组合宿主 DI 解析。验证入口：root 专用 `POST /api/settings/email/test`（SendTestEmailCommand，`{to}` 邮箱格式校验）使用**已保存**配置发送测试邮件——前端测试按钮在有未保存修改时提示先保存。前端卡片提供八项配置与测试邮件行。E2E：[@SET-S48](./bdd.md#set-s48)~[@SET-S53](./bdd.md#set-s53)（本地 SMTP 桩覆盖免认证与 AUTH PLAIN 两条链路）；前端 [@SET-S54](./bdd.md#set-s54)。存量库补种见 `asserts/setting.sql`。
15. **邮箱验证码组（2026-10-29）**：`REGISTER_EMAIL_CODE_ENABLED`（默认 "false"，注册是否需要邮箱验证码）+ 四个模板项 `SMTP_REGISTER_CODE_SUBJECT`/`SMTP_REGISTER_CODE_BODY`/`SMTP_RESET_CODE_SUBJECT`/`SMTP_RESET_CODE_BODY`（默认值即内置默认模板，留空发送时同样回退内置默认）。保存校验：开关仅接受 true/false（归一小写），主题 ≤500（与发送侧主题上限一致）、正文 ≤2000（`setting.value` 列上限），非法 400（[@SET-S56](./bdd.md#set-s56)~[@SET-S58](./bdd.md#set-s58)）。模板支持 `{code}`/`{siteName}`/`{expireMinutes}` 变量，由 auth 模块 `EmailCodeTemplate` 以 SmartFormat 白名单插值（消费端语义见 [auth/sdd 决策 7](../auth/sdd.md)），本模块只管存取。

## 已知问题

- 无未修复缺陷。
- 遗留观察（全局，非本模块缺陷）：GET /api/settings 为 admin 专属，member 得 403（有意设计，见 [@SET-S2](./bdd.md#set-s2)）；root 变更后权限最长滞后 1 小时（用户态缓存）。
- 遗留观察：`NEO4J_PASSWORD`、`SMTP_PASSWORD` 明文存储且 `GET /api/settings` 面向 admin 返回全部值，因此非 root 的 admin 亦可通过接口读到密码；前端知识图谱卡片仅 root 渲染（[@SET-S18](./bdd.md#set-s18)）、设置页整页仅 root（邮件服务卡片同），后续如需严格隔离应在查询层按角色裁剪敏感值。

## 增量设计（2026-10-28：下线 AGENT_COMPACTION_DEFAULTS 设置项）

- `AGENT_COMPACTION_DEFAULTS` 整体移除（用户决策：压缩默认参数不作为系统配置暴露）：`SettingDefinitions` 注册、`SettingsService.ValidateValue` 范围校验、读取服务 `IAgentSettingsService`/`AgentSettingsService` 与模型 `AgentCompactionDefaultsSettings` 全部删除；管理页「Agent 上下文压缩默认参数」折叠卡下线。
- 压缩默认参数改为内置常量（应用级 execution_settings 覆盖保留），运行时合并与预填端点口径见 [../ai/sdd.md](../ai/sdd.md) 与 [../app/sdd.md](../app/sdd.md)；存量库行清理见 `asserts/setting.sql`。
