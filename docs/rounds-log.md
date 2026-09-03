# 代码熟悉轮次台账（SDD/TDD/BDD/SOD 闭环）

目标：对 moai 代码库逐模块走「熟悉代码 → 生成 SDD/TDD/BDD/SOP 四件套 → 自检闭环」循环，至少 20 轮。每轮自检必须用真实证据（命令输出/测试结果/HTTP 实测）确认文档与代码一致，才算闭环。

规则：
- 后端模块文档放 `docs/<模块>/`，前端模块文档放 `ui/docs/<模块>/`，每轮四件：sdd.md / tdd.md / bdd.md / sop.md。
- 每轮的 tdd.md 末尾附「自检记录」：跑过的命令与结果。
- 台账状态：✅ 闭环 / 🔄 进行中 / ⬜ 未开始。

| 轮 | 模块 | 文档目录 | 状态 | 自检证据 |
|---|---|---|---|---|
| 1 | 用户管理（后端+前端） | docs/user-management/ | ✅ | E2E 34/34、vitest 42/42、lint/tsc 绿（2026-09-01） |
| 2 | auth 领域（后端） | docs/auth/ | ✅ | 锁定自检 8/8（local-dev/auth-lockout-check.mjs）；E2E 34/34；发现缺陷：redirectUrl 校验死代码（已记录） |
| 3 | account 领域（后端自助） | docs/account/ | ✅ | 终审实测（audit-345.mjs）：userinfo/update 缓存失效/unbind 404/bound_accounts 全过 |
| 4 | settings 领域（后端） | docs/settings/ | ✅ | 终审实测：GET/PUT 生效、非法 key 400 |
| 5 | oauthconnect 领域（后端） | docs/oauthconnect/ | ✅ | 终审实测：create/list/软删除/重名拒绝；**PUT 缺陷已修复**（实测 200，文档已同步） |
| 6 | common 领域（后端） | docs/common/ | ✅ | 实测 serverinfo 五字段/publicStoreUrl=/static；build_guid 匿名 401（修正初稿"匿名"说法）、带 token 200 v7；补强：RSA 密钥机制（configs/rsa_private.key 首启生成 2048 PKCS8、一钥三用），node 派生公钥与 serverinfo rsaPublic 逐字节一致（2026-09-02） |
| 7 | storage 领域（后端） | docs/storage/ | ✅ | 终审全链路实测（audit-storage.mjs，跑两次均 7/7）：预上传→PUT 预签名直传 MinIO→完成→匿名 /static 访问字节一致；秒传复用 fileId；fileSize=0 400；私有前缀 404 |
| 8 | hangfire 领域（后端） | docs/hangfire/ | ✅ | Redis 实证：hangfireservers 1 实例、recurring-job:counter 定义/执行链 LastJobId 一致；发现缺陷：counter cron 为 `* * * * ? *`（每秒语义，被 10s 轮询钳制约 10s 一次），源码注释"每分钟"是错的（2026-09-02） |
| 9 | infra 基础设施（后端） | docs/infra/ | ✅ | dotnet build 0 错；MAI_FILE 覆盖链路实测（serverinfo serviceUrl=5210 而非 configs 的 5000）；SystemOptions 全字段对照源码（2026-09-02） |
| 10 | database + PostgresScaffold（后端） | docs/database-scaffold/ | ✅ | psql 实证：6 表/vector 0.8.6/唯一复合索引/种子 admin+root+classify 99 行；序列 setval last_value=max+1；Redis moai:* 键命中；工具构建 0 错（2026-09-02） |
| 11 | 前端认证流（登录/注册/OAuth/token 续期） | ui/docs/auth-flow/ | ✅ | jwt 宽限 60s 核对；/login、/oauth_login 均 200；E2E 34/34 覆盖 RSA 链路；前端三件套绿 |
| 12 | 前端 Kiota API 层 | ui/docs/api-layer/ | ✅ | typecheck 0 错；kiota=1.27.0；usermanage 路径齐全；Feedback 测试 15/15 |
| 13 | 前端主题系统（design-system/theme） | ui/docs/theme/ | ✅ | theme 单测 6/6；主色实测 #2970FF（旧文档 #4A9EFF 已过时并记录）；index.css 无 CSS 变量确认 |
| 14 | 前端基础组件（Page/Card/DataTable/QueryBar/PageToolbar） | ui/docs/components-base/ | ✅ | 定向 vitest 5 文件 11 用例全过（Page2/StatCard1/DataTable4/QueryBar2/PageToolbar2）；全量 42/42 + lint 0 警告；props 逐项对照源码，i18n 键实测取值（2026-09-02） |
| 15 | 前端表单与反馈（FormPage/DetailPage/Feedback/Chat） | ui/docs/components-form/ | ✅ | FormPage/DetailPage/Chat 7/7 + Feedback 15/15 单测通过；导出口径比对一致 |
| 16 | 前端布局导航与路由 | ui/docs/layout-routing/ | ✅ | 路由表 11 条逐一比对；6 个 SPA 路由 200；未实现菜单项兜底行为确认并记录 |
| 17 | 前端状态管理与 i18n | ui/docs/store-i18n/ | ✅ | store 三路 persist 键名核对（moai-web-store/theme/locale）；i18next 12 业务前缀清点；vitest 42/42 + tsc 0 错（2026-09-02） |
| 18 | 前端账号设置页 AccountSettings | ui/docs/page-account/ | ✅ | 头像直传管线（SHA-256→pre_upload→PUT→complate→/account/avatar）逐环节对照源码；两处历史样式偏差如实记录（2026-09-02） |
| 19 | 前端 Settings/OauthConnect 管理页 | ui/docs/page-admin/ | ✅ | dirty 跟踪/回滚与渠道 CRUD 对照源码；发现后端存量缺陷：渠道编辑 PUT 固定 400（SharpGrip），已记录规避法；**终审更新 2026-09-02：该缺陷已修复**（Validate 移除路由回填字段规则，audit-345 实测 PUT 200） |
| 20 | 前端 Dashboard 与测试基建 | ui/docs/dashboard-testing/ | ✅ | Dashboard 静态假数据/占位路由行为确认并记录；13 测试文件 42 用例分布清点；新页面写测试五步模板（2026-09-02） |
| 21 | 部署与本地环境（Docker/entrypoint/local-dev） | docs/deployment/ | ✅ | docker compose config -q 通过；serverinfo 200（serviceUrl=5210 证实 MAI_FILE 覆盖）；发现阻断缺陷 D1/D2 并推动修复；**2026-09-03 容器全链路验收 ✅**：镜像构建（arm64）+ serverinfo 200 + e2e 41/41（UM 34 + 存储 7 容器内跑）；D3 修复（entrypoint S3 段）；移除 apt 死重；新增 D6/Apple Silicon 注意事项 |
| 22 | 文档体系重构（分层+Gherkin+互链） | docs/DOC-STANDARD.md + 全部 21 模块 | ✅ | doc-audit.py 全过：385 场景全部编号化（@缩写-Sn+@auto/@manual）、四件互链 21×4、篇幅达标、链接/锚点零悬空；回归 63/63 E2E + 42/42 vitest + 15/15 浏览器点击（2026-09-02） |
| 23 | 团队模块（数据库+API+前端） | docs/team/ | ✅ | team/team_user 建表（bool 软删除+partial 唯一索引，psql 冒烟）；team-e2e.mjs **35/35**；前端 Teams 页 3 测试 + vitest 45/45 + tsc/eslint 绿；全量回归 UM34+ACC/SET/OC14+STO7+AUTH8+BDD36+deep68 全过；浏览器走查建团/成员/解散（2026-09-02）；另修复台账 P4（Dockerfile ui/moai→ui + net10 镜像 + MAI_FILE）与 P5（OTLP 空配置容错） |
| 24 | 团队模式二期（转让所有权+团队头像） | docs/team/ | ✅ | 新端点 PUT owner / POST avatar；team-e2e.mjs 47/47（TM-13 转让角色互换、TM-14 头像存储全链路+防伪造）；前端设置弹窗+转让入口，vitest 46/46；浏览器走查通过；全量回归 UM34+14+7+8+BDD36+deep68 全过（2026-09-02） |
| 25 | 知识库模块（数据库+API+前端）+ 团队模式上下文 | docs/wiki/ | ✅ | wiki 建表（bool 软删除+partial (team_id,name) 唯一）；wiki-e2e.mjs **23/23**；前端 store.currentTeamId+侧边栏切换器+/wiki 页（4 测试），vitest **50/50**；全量回归 UM34+14+7+8+team47+BDD36+deep68 全过；浏览器走查通过（2026-09-02）；另解决并行 stash-pop 冲突（aichannel vs team 三方保留） |
| 25 | 知识库模块（数据库+API+前端）+ 团队模式上下文 | docs/wiki/ | ✅ | wiki 建表（bool 软删除+partial (team_id,name) 唯一）；wiki-e2e.mjs **23/23**；前端 store.currentTeamId+侧边栏切换器+/wiki 页（4 测试），vitest **50/50**；全量回归 UM34+14+7+8+team47+BDD36+deep68 全过；浏览器走查通过（2026-09-02）；另解决并行 stash-pop 冲突（aichannel vs team 三方保留） |
| 26 | 知识库文档层（内容协作） | docs/wiki/ | ✅ | wiki_document 建表（text 正文）；文档 5 端点（Member 可增改、Admin+ 删）；wiki-doc-e2e.mjs **15/15**；前端 /wiki/:id 文档页+编辑器（2 测试），vitest **52/52**；全量回归 252 项后端断言全过；浏览器走查建文档/编辑器回显（2026-09-03） |
| 27 | 变量管理模块（数据库+API+前端） | docs/variable/ | ✅ | team_variable 建表（私密值 AES 加密+partial (team_id,key) 唯一）；variable-e2e.mjs **26/26**（私密值全程未泄露）；前端 /variable 页+侧边栏入口（4 测试），vitest **56/56**；IVariableService 供插件运行时 ${key} 服务端替换；全量回归全过；浏览器走查+截图（2026-09-03） |
