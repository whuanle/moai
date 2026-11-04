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
├── MoAI.Settings.Shared/  Commands/SaveSettingCommand({key,value})、UpdateSystemLogoCommand({objectKey})、UpdateLoginBackgroundCommand({objectKey})
│                          Queries/QuerySettingsCommand + Responses（SettingItemResponse：Key/Name/Description/Value）
│                          Models/Neo4jKnowledgeGraphSettings、SandboxLimitsSettings、EmailSmtpSettings（Enabled+ChannelId）、EmailMessage/EmailSendResult
│                          + Services/ISettingsService、IKnowledgeGraphSettingsService、IWikiSettingsService、ISandboxSettingsService、IEmailSettingsService、IEmailSenderService（实现已移交 notification 模块）
├── MoAI.Settings.Core/    Handlers/{Query,Save}SettingHandler、UpdateSystemLogoCommandHandler、UpdateLoginBackgroundCommandHandler（文件登记校验后委托 SaveSettingAsync）
│                          + Services/SettingsService（校验与读写）
│                          + Services/KnowledgeGraphSettingsService、WikiSettingsService、SandboxSettingsService（成组读取）
│                          + Services/EmailSettingsService（读 SMTP_ENABLED + SMTP_CHANNEL_ID，并核验绑定渠道存在且启用后短路 Enabled）
└── MoAI.Settings.Api/     SettingsController（[Route("/settings")]，门禁在此，含 POST /settings/logo、POST /settings/login-background；测试邮件端点已下线）
src/common/…/Queries/      QueryServerInfoCommandHandler（附加读取 SYSTEM_LOGO → logoPath、SYSTEM_NAME → name、LOGIN_BACKGROUND → loginBackgroundPath，匿名可读，空名称回退 SystemOptions.Name；邮箱验证可用性 = SMTP_ENABLED + 绑定渠道存在且启用）
src/database/…/Seed/       SettingDefinition(s)/SettingSeed（注册表 + 种子：{Id=1, key="root", value="1"}）
                           SettingConfiguration（key≤50 哈希索引、name 20、description 255、value 2000）
ui/src/                    api/settings.ts（SettingKeys 常量 + get/save + updateSystemLogo/resetSystemLogo + updateLoginBackground/resetLoginBackground）
                           layouts/useSystemLogo.ts + components/AppLogo.tsx（全局 Logo 解析与展示；useSystemName 取网站名称；useLoginBackgroundSrc 取登录页背景）
                           pages/settings/Settings.tsx（/settings，卡片自上而下：网站信息（网站名称、网站 Logo、登录页背景图三分区，可折叠）→ 知识库 → 知识图谱（图数据库）→ 邮箱验证（启用开关 + 绑定通知中心 SMTP 服务下拉 + 注册验证码开关、注册/找回密码/登录三个验证码通知模板绑定下拉）→ 沙箱资源上限）
                           pages/auth/Login.tsx（登录页整页背景：有背景图铺满 cover，无则默认渐变）
```

## API 契约

| 方法 | 路由 | 门禁 | 说明 |
|---|---|---|---|
| GET | `/api/settings` | `IsAdmin`（root 隐含满足），否则 403「只有管理员可以访问设置项」 | 返回全部内置项 `{key,name,description,value}` |
| PUT | `/api/settings` | 仅 `IsRoot`，否则 403「只有超级管理员可以修改设置项」 | `{key, value}` 均 string |
| POST | `/api/settings/logo` | 仅 `IsRoot`，否则 403「只有超级管理员可以修改网站 Logo」 | `{objectKey}`，空串恢复默认；非空须为已登记上传的文件，否则 404（[@SET-S25](./bdd.md#set-s25)、[@SET-S28](./bdd.md#set-s28)） |
| POST | `/api/settings/login-background` | 仅 `IsRoot`，否则 403「只有超级管理员可以修改登录页背景图」 | `{objectKey}`，语义同 Logo：空串恢复默认；非空须为已登记上传的文件，否则 404（[@SET-S36](./bdd.md#set-s36)、[@SET-S39](./bdd.md#set-s39)） |

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
14. **邮箱验证与 SMTP 服务绑定（2026-11-02 重构）**：SMTP 连接配置**统一在通知中心**的 SMTP 渠道维护（渠道自带 host/端口/加密/账号/发件人，设计见 [notification/sdd](../notification/sdd.md)），本模块只保留 `SMTP_ENABLED`（邮箱验证总开关）+ `SMTP_CHANNEL_ID`（绑定的渠道 id，空串未绑定）。保存校验：开关仅 true/false；渠道 id 空串或正整数（非法 400）。读取经 `IEmailSettingsService.GetAsync`（`Settings.Shared` 接口 + `EmailSettingsService` 实现）：`Enabled = 开关开启 且 绑定渠道存在（Type=Smtp 且启用，软删自动过滤）`，不可用短路 `Enabled=false`。系统邮件发送经 `IEmailSenderService.SendAsync`——接口留在 `Settings.Shared`（消费方不变），**实现移交 notification 模块** `SystemEmailSenderService`：按绑定渠道的连接配置经 `SmtpMailTransport`（MailKit）发送，未启用/未绑定 400 提示绑定。root 测试邮件专用端点 `POST /api/settings/email/test` 与 SendTestEmailCommand/Handler 已删除（连通性验证改用通知中心渠道「测试」）。前端「邮箱验证」卡片 = 启用开关 + 绑定下拉（选项来自通知中心 SMTP 渠道，停用渠道带标记）+ 注册验证码开关与模板。E2E：[@SET-S61](./bdd.md#set-s61)~[@SET-S66](./bdd.md#set-s66)（本地 SMTP 桩覆盖免认证与 AUTH PLAIN 链路）；前端 [@SET-S67](./bdd.md#set-s67)。**存量升级注意**：原 8 个 `SMTP_*` 连接键作废（`asserts/setting.sql` 已清理存量行），升级后需在通知中心重建 SMTP 渠道并在本页绑定，否则 serverinfo `passwordResetEnabled=false`、发信报 400。
15. **邮箱验证码组（2026-11-02 模板收敛到通知中心）**：`REGISTER_EMAIL_CODE_ENABLED`（默认 "false"，注册是否需要邮箱验证码）+ 三个模板绑定键 `SMTP_REGISTER_CODE_TEMPLATE_ID`/`SMTP_RESET_CODE_TEMPLATE_ID`/`SMTP_LOGIN_CODE_TEMPLATE_ID`（绑定的通知中心模板 id，空串=未绑定）。保存校验：开关仅接受 true/false（归一小写），模板 id 空串或正整数（非法 400，[@SET-S56](./bdd.md#set-s56)/[@SET-S69](./bdd.md#set-s69)）。验证码邮件模板本体**统一在通知中心 → 通知模板维护**（支持 text/html 与渲染测试，内置三条种子模板按 code 回退，渲染链路见 [auth/sdd 决策 7](../auth/sdd.md) 与 [notification/sdd 内置模板](../notification/sdd.md)），旧 6 个 `SMTP_*_CODE_SUBJECT/BODY` 键作废（`asserts/setting.sql` 已清理存量行），本模块只管绑定 id 存取。
16. **登录安全组（2026-11-01，2026-11-03 扩展）**：`LOGIN_FORCED_2FA`（默认 "false"，强制全员两步验证）+ `LOGIN_EMAIL_CODE_MODE`（默认 "off"，off/always/after_fail 三值白名单，非法 400）+ `LOGIN_CAPTCHA_MODE`（默认 "off"，图形验证码三值白名单，非法 400，覆盖登录提交与注册/找回发码）+ `LOGIN_EMAIL_CODE_FAIL_THRESHOLD`（默认 "3"，邮箱验证码 after_fail 触发阈值，1~10 整数否则 400）。保存校验归一小写（[@SET-S59](./bdd.md#set-s59)/[@SET-S60](./bdd.md#set-s60)/[@SET-S92](./bdd.md#set-s92)）；策略的消费端语义（一体化登录、图形验证码分级递进、双开关生效条件、挑战流程、强制绑定门禁）见 [auth/sdd 决策 8~10](../auth/sdd.md)，本模块只管存取。前端设置页（root）不单独设卡片，「两步验证与登录验证码」（强制 2FA 开关 + 登录邮箱验证码三选 + after_fail 时阈值输入 + 图形验证码三选，独立保存）并入「登录安全」卡片顶部，与风控策略以分隔线隔开；登录验证码邮件模板绑定下拉在「邮箱验证」卡片（[@SET-S69](./bdd.md#set-s69)）。存量库补种见 `asserts/setting.sql`。
17. **登录安全通知改版（2026-11-02）**：登录风控的通知键收敛为 `LS_ENABLED` + `LS_NOTIFY_CHANNEL_IDS`（高风险事件推送的通知中心渠道 ID，逗号分隔）+ 6 个阈值键；`LS_NOTIFY_USER_ENABLED/LS_NOTIFY_ADMIN_ENABLED/LS_ADMIN_ALERT_RECIPIENTS/LS_USER_ALERT_SUBJECT/BODY/LS_ADMIN_ALERT_SUBJECT/BODY` 7 键从 `SettingDefinitions` 与 `ValidateValue` 移除（存量行留在 setting 表不再读取），通知改走站内信+通知中心渠道（消费端语义见 [loginsafety/sdd 决策](../loginsafety/sdd.md)）。`LS_NOTIFY_CHANNEL_IDS` 校验：空通过；逗号分隔每段须正整数、≤50 个、总长 ≤ 列上限，非法 400。设置页「登录安全」卡片「风险事件通知渠道」多选下拉（通知中心已启用渠道）。
18. **沙箱出站网络管控组（2026-11-03）**：`SANDBOX_NETWORK_MODE`（app=应用自定义 / deny=强制默认拒绝 / intranet=默认拒绝并放行内网网段，三值白名单归一小写，非法 400）+ `SANDBOX_INTRANET_CIDRS`（逗号分隔 IP/CIDR，默认三类私有地址段，逐条校验非法 400）。读取并入 `SandboxLimitsSettings`（`NetworkMode` + `IntranetCidrs` 解析后列表，缺失/非法回退内置默认）；消费端为应用保存校验（deny/intranet 下默认 allow 400）与沙箱创建时策略归一（运行时兜底强制），见 [app/sdd 决策 48](../app/sdd.md)。目标格式解析器 `SandboxNetworkTarget`（域名/`*.`通配符/IP/CIDR/端口）同放 `Settings.Shared/Models`，前后端语义一致。行为见 [@SET-S93](./bdd.md#set-s93)。
19. **沙箱平台级禁用命令清单（2026-11-04）**：`SANDBOX_DENIED_COMMANDS`（换行分隔模式，默认内置基础危险命令集 18 条——shutdown/reboot/mkfs/dd 等）。**关闭语义特殊**：`GetLimitsAsync` 按「行存在性」区分——行存在时空串表示 root 显式关闭（不回退默认），仅行缺失才回退内置默认。校验：逐条 `SandboxCommandPatterns.IsValidPattern`（regex: 须可编译、≤200 字符、≤100 条，非法 400）。读取并入 `SandboxLimitsSettings.DeniedCommands`；运行时与应用级 commandFilter 取并集拦截 `sandbox_run_shell`/`sandbox_run_code`（拦截语义见 [app/sdd 决策 49](../app/sdd.md)）。设置页沙箱卡片「平台级禁用命令清单」文本域随卡片保存提交。行为见 [@SET-S94](./bdd.md#set-s94)。

## 已知问题

- 无未修复缺陷。
- 遗留观察（全局，非本模块缺陷）：GET /api/settings 为 admin 专属，member 得 403（有意设计，见 [@SET-S2](./bdd.md#set-s2)）；root 变更后权限最长滞后 1 小时（用户态缓存）。
- 遗留观察：`NEO4J_PASSWORD` 明文存储且 `GET /api/settings` 面向 admin 返回全部值，因此非 root 的 admin 亦可通过接口读到密码；前端知识图谱卡片仅 root 渲染（[@SET-S18](./bdd.md#set-s18)）、设置页整页仅 root，后续如需严格隔离应在查询层按角色裁剪敏感值（`SMTP_PASSWORD` 已随 SMTP 配置收敛到通知中心渠道，见决策 14；通知中心渠道配置含密钥的明文存储问题见 [notification/sdd](../notification/sdd.md)）。

## 增量设计（2026-10-28：下线 AGENT_COMPACTION_DEFAULTS 设置项）

- `AGENT_COMPACTION_DEFAULTS` 整体移除（用户决策：压缩默认参数不作为系统配置暴露）：`SettingDefinitions` 注册、`SettingsService.ValidateValue` 范围校验、读取服务 `IAgentSettingsService`/`AgentSettingsService` 与模型 `AgentCompactionDefaultsSettings` 全部删除；管理页「Agent 上下文压缩默认参数」折叠卡下线。
- 压缩默认参数改为内置常量（应用级 execution_settings 覆盖保留），运行时合并与预填端点口径见 [../ai/sdd.md](../ai/sdd.md) 与 [../app/sdd.md](../app/sdd.md)；存量库行清理见 `asserts/setting.sql`。
