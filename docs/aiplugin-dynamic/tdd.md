# 动态插件（DynamicPlugin）验证映射（TDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 证据：[local-dev/dynamic-plugin-e2e.mjs](../../local-dev/dynamic-plugin-e2e.mjs) ｜ [local-dev/bocha-search-e2e.mjs](../../local-dev/bocha-search-e2e.mjs) ｜ [local-dev/moji-weather-e2e.mjs](../../local-dev/moji-weather-e2e.mjs)
> 规范：[../DOC-STANDARD.md](../DOC-STANDARD.md)。场景复述见 BDD，本文只做编号→验证物映射。

## 后端 E2E

三个脚本分工：`dynamic-plugin-e2e.mjs` 针对**已运行的真实后端**做实例管理与失败路径断言；`bocha-search-e2e.mjs`
自带 BoCha 桩服务并拉起一个独立后端（`MoAI__BoCha__Endpoint` 指向桩），覆盖**成功路径与响应解析**，无需真实 API Key、不消耗额度；
`moji-weather-e2e.mjs` 同为桩服务模式（`MoAI__MojiWeather__Endpoint` 指向桩），覆盖墨迹天气模板，无需真实 AppCode；
`ops-p1-plugins-e2e.mjs` 自带 HTTP 桩（拨测目标 + Zabbix JSON-RPC）与 Redis RESP2 桩并拉起独立后端（优先 `.builds/ops-p1` 独立输出，无则回退 `dotnet run`），覆盖 P1 运维三模板（http_probe/zabbix_query/redis_query），无需真实运维系统；
`ops-p2-plugins-e2e.mjs` 同为桩模式（Grafana HTTP 桩 + `.builds/ops-p2` 独立输出），覆盖 P2 运维三模板（ssh_executor/grafana_query/sqlserver_query）：守卫用例（校验先于连接）无条件运行，SSH 真机执行与 SQL Server 真库由 `SSH_E2E_CONNECTION` / `SQLSERVER_E2E_CONNECTION` 门控（未设置 SKIP）；
`ops-p0-plugins-e2e.mjs` 多路桩模式（Alertmanager v2 + Loki + Kubernetes API + 钉钉/企微机器人 + `.builds/ops-p0` 独立输出，`MoAI__DingTalk__RobotEndpoint` / `MoAI__WeixinWork__RobotEndpoint` 指向桩），覆盖 P0 运维五模板（alertmanager_query/loki_query/kubernetes_query/dingtalk_webhook_text/wecom_webhook_text），其中钉钉桩复算 HMAC-SHA256 验签。

```bash
# 1) 实例管理与失败路径（需后端 :5000 运行中）
node local-dev/dynamic-plugin-e2e.mjs

# 2) 博查成功路径与解析（脚本自建桩服务 + 独立后端 :5199，无需真实 Key）
node local-dev/bocha-search-e2e.mjs

# 3) 墨迹天气成功路径与解析（脚本自建桩服务 + 独立后端 :5197，无需真实 AppCode）
node local-dev/moji-weather-e2e.mjs
```

| 场景 | 验证物 | 结果（日期） |
|---|---|---|
| @DYN-S1 | local-dev/dynamic-plugin-e2e.mjs（列表出现实例并带模板 key/配置） | PASS（2026-09-11） |
| @DYN-S2 | local-dev/dynamic-plugin-e2e.mjs（未创建时列表不含目标 key） | PASS（2026-09-11） |
| @DYN-S3 | local-dev/dynamic-plugin-e2e.mjs（创建实例 200） | PASS（2026-09-11） |
| @DYN-S4 | local-dev/dynamic-plugin-e2e.mjs（实例 key = 模板 key → 409） | PASS（2026-09-11） |
| @DYN-S5 | local-dev/dynamic-plugin-e2e.mjs（同 key 重复提交走更新，列表仍单行） | PASS（2026-09-11） |
| @DYN-S6 | local-dev/dynamic-plugin-e2e.mjs（大写/数字开头/超长 → 400） | PASS（2026-09-11） |
| @DYN-S7 | local-dev/dynamic-plugin-e2e.mjs（模板不存在 → 404） | PASS（2026-09-11） |
| @DYN-S8 | local-dev/dynamic-plugin-e2e.mjs（改配置后运行使用新 Prefix） | PASS（2026-09-11） |
| @DYN-S9 | local-dev/dynamic-plugin-e2e.mjs（更新后 key 不变、标题变更） | PASS（2026-09-11） |
| @DYN-S10 | local-dev/dynamic-plugin-e2e.mjs（运行返回 Hello MoAI） | PASS（2026-09-11） |
| @DYN-S11 | local-dev/dynamic-plugin-e2e.mjs（运行不存在实例 → 提示不存在） | PASS（2026-09-11） |
| @DYN-S12 | local-dev/dynamic-plugin-e2e.mjs（删除 200 + 列表消失） | PASS（2026-09-11） |
| @DYN-S13 | local-dev/dynamic-plugin-e2e.mjs（重复删除 → 404） | PASS（2026-09-11） |
| @DYN-S14 | local-dev/dynamic-plugin-e2e.mjs（成员查询/新建/删除/运行全 403） | PASS（2026-09-11） |
| @DYN-S15 | local-dev/dynamic-plugin-e2e.mjs（注册表含 dynamic_greet 与 bocha_web_search） | PASS（2026-09-11） |
| @DYN-S16 | local-dev/bocha-search-e2e.mjs（桩服务返回网页/图片 → `S16a~f` 解析 + 参数兜底 + 鉴权头） | PASS（2026-09-11） |
| @DYN-S17 | local-dev/dynamic-plugin-e2e.mjs（空 ApiKey / 空 Query → 可读失败） | PASS（2026-09-11） |
| @DYN-S18 | local-dev/dynamic-plugin-e2e.mjs（无效 Key → `HTTP 401` + 博查响应体） | PASS（2026-09-11） |
| @DYN-S19 | local-dev/dynamic-plugin-e2e.mjs（注册表含 bocha_ai_search，示例与配置类型齐全） | PASS（2026-09-11） |
| @DYN-S20 | local-dev/dynamic-plugin-e2e.mjs（空 ApiKey / 空 Query → 可读失败） | PASS（2026-09-11） |
| @DYN-S21 | local-dev/dynamic-plugin-e2e.mjs（无效 Key → `HTTP 401` + 博查响应体；真实上游） | PASS（2026-09-11） |
| @DYN-S21b | local-dev/bocha-search-e2e.mjs（桩服务 401 → 可读失败；确定性覆盖，不依赖外网） | PASS（2026-09-11） |
| @DYN-S22 | local-dev/bocha-search-e2e.mjs（`S22a~l` 答案/追问/网页/图片/模态卡解析 + 非流式 + 参数兜底 + Answer=false） | PASS（2026-09-11） |
| @DYN-S23 | local-dev/dynamic-plugin-e2e.mjs（注册表含 feishu_webhook_text，示例与配置类型齐全） | PASS（2026-09-11） |
| @DYN-S24 | local-dev/dynamic-plugin-e2e.mjs（空 WebhookKey / 空 Text / 占位 token → 飞书 19001） | PASS（2026-09-11） |
| @DYN-S25 | local-dev/dynamic-plugin-e2e.mjs（注册表含 javascript_executor，配置示例与参数示例齐全） | PASS（2026-09-11） |
| @DYN-S26 | local-dev/dynamic-plugin-e2e.mjs（不同 JS 返回值 → 归一 ResultKind/ResultJson） | PASS（2026-09-11） |
| @DYN-S27 | local-dev/dynamic-plugin-e2e.mjs（空 JavaScriptCode / 缺 run / 语法错误 / 运行时错误 → 可读失败） | PASS（2026-09-11） |
| @DYN-S28 | local-dev/dynamic-plugin-e2e.mjs（Parameters 回显 / null+undefined 归一 / 不抛 500） | PASS（2026-09-11） |
| @DYN-S29 | local-dev/dynamic-plugin-e2e.mjs（注册表含 postgres_query / mysql_query，配置示例含 `Host`、参数示例含 `Sql`、配置类型齐全） | PASS（2026-09-26） |
| @DYN-S30 | local-dev/dynamic-plugin-e2e.mjs（`S30c` 12 类写操作/多语句被拒 + `S30d` MySQL 同样拒绝 + `S30e` 6 条合法只读放行到连接层） | PASS（2026-09-26） |
| @DYN-S31 | local-dev/dynamic-plugin-e2e.mjs（空 `Sql` / 空 `Host` → 可读失败） | PASS（2026-09-26） |
| @DYN-S32 | local-dev/dynamic-plugin-e2e.mjs（`PG_E2E_CONNECTION` 连接串解析为 Host/Port/Database/Username/Password 离散配置，指向真实 PG：MaxRows 截断 / 列名与行 / 会话只读 / 同名列去重 / bytea+jsonb+时间 / 空结果集） | PASS（2026-09-26） |
| @DYN-S33 | local-dev/dynamic-plugin-e2e.mjs（`MYSQL_E2E_CONNECTION` 指向真实 MySQL：`SELECT 1` + `SHOW VARIABLES LIKE 'transaction_read_only'`） | SKIP（2026-09-26，本机与同网段无可达 MySQL、Docker 未运行；脚本已按环境变量开关就绪） |
| @DYN-S34 | 人工：插件管理页模板下拉选 `postgres_query` / `mysql_query`，检查配置编辑器与运行抽屉的示例文案 | 待运行（前端无改动，配置示例由注册表自动下发为新离散字段文案） |
| @DYN-S36 | local-dev/paddleocr-e2e.mjs（注册表含 paddleocr_ocr / paddleocr_structure_v3 / paddleocr_vl，示例与配置类型齐全） | 待运行 |
| @DYN-S37 | local-dev/paddleocr-e2e.mjs（S37a~f 桩 /ocr → Pages/Text/OcrImage/InputImage 解析 + 鉴权头 + 参数透传） | 待运行 |
| @DYN-S38 | local-dev/paddleocr-e2e.mjs（S38a~f 桩 /layout-parsing StructureV3 → Pages/SealTexts/PrunedResultJson/OutputImages/InputImage + 鉴权头） | 待运行 |
| @DYN-S39 | local-dev/paddleocr-e2e.mjs（S39a~f 桩 /layout-parsing VL → Pages/MarkdownText/MarkdownImages/PrunedResultJson/OutputImages/InputImage + 鉴权头 + visualize=true） | 待运行 |
| @DYN-S40 | local-dev/paddleocr-e2e.mjs（空 ApiUrl → InitAsync 拒 + 恢复后仍可运行） | 待运行 |
| @DYN-S41 | local-dev/paddleocr-e2e.mjs（Token 为空 → 桩 401 → 业务异常带 HTTP 状态码 + 响应体） | 待运行 |
| @DYN-S42 | local-dev/paddleocr-e2e.mjs（桩服务被 /ocr 与 /layout-parsing 命中数） | 待运行 |
| @DYN-S43 | local-dev/moji-weather-e2e.mjs（注册表含 moji_weather，配置示例含 AppCode/Token、参数示例含 CityId/Lat/Lon） | PASS（2026-09-18） |
| @DYN-S44 | local-dev/moji-weather-e2e.mjs（`S44a~f` 表单编码 + APPCODE 头 + Token 透传 + 实况/逐日预报/定位城市容错解析 + 空条目丢弃） | PASS（2026-09-18） |
| @DYN-S45 | local-dev/moji-weather-e2e.mjs（`S45a~b` lat/lon 定位成功 + 未配置 Token 不发 token 字段） | PASS（2026-09-18） |
| @DYN-S46 | local-dev/moji-weather-e2e.mjs（`S46a~b` 无定位/半定位 → 可读失败；桩命中数证明未外呼） | PASS（2026-09-18） |
| @DYN-S47 | local-dev/moji-weather-e2e.mjs（`S47a~b` 上游 401 与信封 code!=0 归一为可读失败） | PASS（2026-09-18） |
| @DYN-S43 | local-dev/dynamic-plugin-e2e.mjs（公开图片直传完成 → 设置头像 200 → 列表回读 avatarPath 一致） | PASS 99/99（2026-09-17） |
| @DYN-S44 | local-dev/dynamic-plugin-e2e.mjs（未登记 objectKey → 404 头像文件不存在或未完成上传） | PASS 99/99（2026-09-17） |
| @DYN-S45 | local-dev/dynamic-plugin-e2e.mjs（匿名 401、普通用户 403） | PASS 99/99（2026-09-17） |
| @DYN-S50 | local-dev/ops-p1-plugins-e2e.mjs（`S50a~i` http 拨测 200/500、tcp 通/拒、dns 解析、内网防护默认拒+放行、非法方法与协议 400） | PASS 39/39（2026-09-29） |
| @DYN-S51 | local-dev/ops-p1-plugins-e2e.mjs（`S51a~k` version 免鉴权、problems 解析+主机富化+SeverityMin、body auth 命中子路径、header auth 命中根路径、hosts/triggers、登录失败归一） | PASS 39/39（2026-09-29） |
| @DYN-S52 | local-dev/ops-p1-plugins-e2e.mjs（`S52a~l` info 分节/指定节段、dbsize、slowlog、client_list、config_get、key_info 含不存在键、缺参 400、错误密码归一） | PASS 39/39（2026-09-29） |
| @DYN-S53 | local-dev/dynamic-plugin-e2e.mjs（P1 三模板注册断言：isDynamic/configType/configExample/paramsExample） | 见自检记录（2026-09-29） |
| @DYN-S54 | local-dev/ops-p2-plugins-e2e.mjs（`S54a~l` 空白名单 fail-closed/拼接替换符/未命中/灾难级黑名单压过白名单/灾难级递归删除/守卫放行进入连接；`S54m` 真机 uptime 由 SSH_E2E_CONNECTION 门控） | PASS 30/0/SKIP 2（2026-09-29） |
| @DYN-S55 | local-dev/ops-p2-plugins-e2e.mjs（`S55a~f` health 版本+Bearer 头+子路径、annotations RFC3339→毫秒+标签逐发+解析、search type=dash-db、错误令牌 401 归一） | PASS 30/0/SKIP 2（2026-09-29） |
| @DYN-S56 | local-dev/ops-p2-plugins-e2e.mjs（`S56a~f` Init 端口校验、写 SQL/多条语句先于连接被拒、连接失败归一；`S56g` 真库 @@VERSION 由 SQLSERVER_E2E_CONNECTION 门控） | PASS 30/0/SKIP 2（2026-09-29） |
| @DYN-S57 | local-dev/dynamic-plugin-e2e.mjs（P2 三模板注册断言：isDynamic/configType/configExample/paramsExample） | 见自检记录（2026-09-29） |
| @DYN-S58 | local-dev/ops-p0-plugins-e2e.mjs（`S58a~f` alerts 解析+状态过滤、silences 正则匹配器、status、401 归一） | PASS 30/0（2026-09-29） |
| @DYN-S59 | local-dev/ops-p0-plugins-e2e.mjs（`S59a~f` query_range 纳秒归一、labels/label_values 路径、series match[]、status=error 信封归一） | PASS 30/0（2026-09-29） |
| @DYN-S60 | local-dev/ops-p0-plugins-e2e.mjs（`S60a~h` pods+选择器、pod_logs 纯文本、events/deployments/nodes、403 权限提示） | PASS 30/0（2026-09-29） |
| @DYN-S61 | local-dev/ops-p0-plugins-e2e.mjs（`S61a~e` HMAC 加签被桩验证、@手机号/@所有人、errcode=310000 归一） | PASS 30/0（2026-09-29） |
| @DYN-S62 | local-dev/ops-p0-plugins-e2e.mjs（`S62a~e` key 归一、mentioned_mobile_list/@all、errcode 归一） | PASS 30/0（2026-09-29） |
| @DYN-S63 | local-dev/dynamic-plugin-e2e.mjs（P0 五模板注册断言） | PASS 156/0（含 S63 20 条）（2026-09-29） |
| @DYN-S64 | local-dev/clickstack-e2e.mjs（`S64a~g` sources 解析（log 字段/metric 表名回退/session 停用态）、Bearer 头命中、错误 Key 401 归一、非法协议 BaseUrl 运行时拒绝） | PASS 20/20（2026-09-29） |
| @DYN-S65 | local-dev/clickstack-e2e.mjs（`S65a~f` 行集混合类型解析+满额 Truncated、请求体透传（select/offset/ISO 时间窗）、缺省窗口 End-15min、MaxResults/Offset 翻页、缺 SourceId/非法 WhereLanguage 400、404/400 错误归一） | PASS 20/20（2026-09-29） |
| @DYN-S66 | local-dev/clickstack-e2e.mjs（`S66a~d` 时间线解析（ISO 桶/聚合值/分组）、epoch 毫秒透传+series 结构、缺省窗口 1h+缺省聚合、sum 缺 Field/非法 Granularity/非法 AggFn/缺 SourceId 四类 400） | PASS 20/20（2026-09-29） |

## 前端测试

| 场景 | 验证物 | 结果（日期） |
|---|---|---|
| 实例列表与模板加载 | ui/src/pages/plugins/__tests__/DynamicPluginPanel.test.tsx | PASS 3/3（2026-09-03） |
| 新建实例弹窗 | ui/src/pages/plugins/__tests__/DynamicPluginPanel.test.tsx | PASS（2026-09-03） |
| 删除实例 | ui/src/pages/plugins/__tests__/DynamicPluginPanel.test.tsx | PASS（2026-09-03） |
| @DYN-S67 | ui/src/pages/plugins/__tests__/PluginTemplates.test.tsx（系统/团队双模式卡片与实例数、空态、刷新、返回、非 admin 重定向） | PASS 10/10（2026-10-05） |
| @DYN-S68 | ui/src/pages/plugins/__tests__/DynamicPluginInstanceModal.test.tsx（预选模板/双 scope 提交分流/实例 key 查重拦截/编辑锁定）＋ PluginTemplates.test.tsx 卡片新建两例 | PASS 4/4＋（2026-10-05） |
| @DYN-S69 | ui/src/pages/plugins/__tests__/PluginTemplates.test.tsx（团队自有计数排除系统实例、canManage=false 重定向团队插件） | PASS（含上两行）（2026-10-05） |

> 博查两轮（全网搜索、AI 搜索）与墨迹天气轮**均无前端改动**：新模板由后端注册表自动进入模板下拉与运行抽屉，无需改 `DynamicPluginPanel`，也无需 `npm run syncapi`。

## 已知缺口

- 管理员侧「与其它动态实例 key 重复 → 409」分支实际不可达：`SaveDynamicPluginCommandHandler` 先把同 key 记录判为更新（@DYN-S5），`EnsureInstanceKeyUniqueAsync` 的 DB 判重谓词与 `existing` 查询完全相同，属死代码。跨团队冲突由团队插件链路负责（teamplugin TP-13）。
- `docs/aiplugin-static/` 引用的 `local-dev/static-plugin-e2e.mjs` 仍然缺失（本轮只补齐了动态插件脚本）。
- 插件运行结果的 `dataJson` 是 **PascalCase**（`PluginExecutor` 用 `JsonSerializer.Serialize(obj, type)` 的默认选项，未走 ASP.NET 的 camelCase 约定），与 HTTP 接口的响应风格不一致；消费方需按 PascalCase 读字段。属引擎既有行为，改动会影响 `dynamic_greet` 等已有模板，未在本轮调整。
- 桩服务脚本验证的是「插件 + Refit + 解析」链路对**官方样例报文**的适配；博查真实返回若与文档样例存在差异（新增/改名字段），仍需一次人工走查（见 [sop.md](./sop.md) 博查排障）。

## 构建与回归

```bash
dotnet build src/MoAI/MoAI.csproj                                   # 后端 0 error
cd ui && npm run typecheck && npm run lint && npm run test          # 前端全绿
node local-dev/dynamic-plugin-e2e.mjs                               # 实例管理 + 失败路径（需后端 5000 运行中）
node local-dev/bocha-search-e2e.mjs                                 # 博查成功路径 + 响应解析（自建桩服务，无需真实 Key）
node local-dev/paddleocr-e2e.mjs                                    # PaddleOCR 成功路径 + 响应解析（自建桩服务，无需真实 PaddleOCR）
node local-dev/moji-weather-e2e.mjs                                  # 墨迹天气成功路径 + 响应解析（自建桩服务，无需真实 AppCode）
dotnet build src/MoAI/MoAI.csproj -o .builds/ops-p1                 # P1 脚本前置：独立输出目录构建（bin/Debug 被运行中后端锁定时）
node local-dev/ops-p1-plugins-e2e.mjs                               # P1 运维三插件（自建 HTTP+RESP2 双桩）
dotnet build src/MoAI/MoAI.csproj -o .builds/ops-p2                 # P2 脚本前置：独立输出目录构建
node local-dev/ops-p2-plugins-e2e.mjs                               # P2 运维三插件（Grafana 桩 + 守卫用例；SSH/SqlServer 真链路按环境变量门控）
dotnet build src/MoAI/MoAI.csproj -o .builds/ops-p0                 # P0 脚本前置：独立输出目录构建
node local-dev/ops-p0-plugins-e2e.mjs                               # P0 运维五插件（AM/Loki/K8s/通知四桩）
dotnet build src/MoAI/MoAI.csproj -o .builds/clickstack             # ClickStack 脚本前置：独立输出目录构建
node local-dev/clickstack-e2e.mjs                                   # ClickStack 查询插件（自建 HyperDX API 三端点桩）
```

## 自检记录

- 2026-09-18（墨迹天气内置模板 moji_weather）
  - `dotnet build src/MoAI/MoAI.csproj` → **0 error**；新文件（`MojiWeather/IMojiWeatherClient.cs`、`InfraExternalHttpModule.cs` 注册段、`MojiWeatherAuthorization.cs`、`Models/MojiWeather*.cs` ×6、`Plugins/MojiWeatherPlugin.cs`）**0 告警**（首版 `MapForecast` 返回 `IReadOnlyList<>` 触发 CA1859，私有方法改返回 `List<>` 后清零）。
  - `node local-dev/moji-weather-e2e.mjs` → **PASS 17 / FAIL 0**（首跑 16/1：唯一 FAIL 为脚本自身计数错误——把两条「定位缺失」校验失败误算为上游命中，修正为断言 `mockHits===4` 并显式化「参数校验失败不外呼」）。覆盖 @DYN-S43~S47：注册表出现、CityId 定位成功路径（表单编码 + APPCODE 头 + Token 透传 + 实况/逐日预报/定位城市容错解析 + 空条目丢弃）、lat/lon 定位且未配置 Token 不发 token、定位缺失 400（不外呼）、上游 401 与信封 code!=0 归一。
  - 脚本环境适配（macOS 本机）：后端用「`local-dev/system.local.json` 拍平成 `MoAI__` 环境变量」拉起（**不用 `MAI_FILE`**——该文件是后加配置源，会反过来覆盖脚本注入的 `MoAI__Port`/`MoAI__MojiWeather__Endpoint`）；独立后端默认 :5197，与常驻 :5210 实例互不干扰；unix 下 `killBackend` 改为杀进程组（`dotnet run` 会再拉起宿主子进程），Windows 分支保留 `taskkill /T /F`。
  - 墨迹官方完整文档需注册下载；桩服务报文按阿里云云市场公开样例形态（`{code,msg,data}` 信封 + camelCase 字段）构造，真实上游字段出入待人工走查（同博查口径，见 [sdd.md](./sdd.md) 已知问题）。
- 2026-09-11（PostgreSQL / MySQL 只读查询内置模板）
  - `dotnet build src/aiplugin/MoAI.AIPlugin.Dynamic/MoAI.AIPlugin.Dynamic.csproj --no-restore -p:GenerateDependencyFile=false` → **0 error、0 告警**（PG 会话设置从字面量拼接改为 `SELECT set_config(...)` 参数化后，`CA2100`/`CA1863` 消除；常量提到类顶部消除 `SA1203`）。
  - **宿主级**：`dotnet build src/MoAI/MoAI.csproj --no-restore` → **0 error、0 告警**（连同并行会话的 PaddleOCR 三模板一起编过，`MoAI.AIPlugin.Dynamic` 不再需要临时排除 props）。
    ⚠️ 沙箱内构建前须补 Windows 系统环境变量 `ProgramData`/`ALLUSERSPROFILE`/`APPDATA`，否则 NuGet 读资产文件报 `Value cannot be null (path1)`；
    ⚠️ **不要用 `-p:GenerateDependencyFile=false` 构建宿主**——SDK 会删掉已有的 `MoAI.deps.json`，`dotnet run --no-build` 随即起不来（叶子项目无此副作用）。
  - 新文件：`SqlReadOnlyGuard.cs`（文本层守卫）、`SqlQueryResult.cs`、`SqlResultReader.cs`、`Models/{Postgres,Mysql}Query{Config,Request,Response}.cs`、`Plugins/{PostgresQueryPlugin,MysqlQueryPlugin}.cs`。
  - `MoAI.AIPlugin.Dynamic.csproj` 加 `PackageReference Include="Npgsql"`（10.0.3）与 `MySqlConnector`（2.5.0），版本中心化管理在 `Directory.Packages.props`。
  - 运行态（2026-09-11）：换入 `MoAI.AIPlugin.Dynamic.dll` 并重启后端（:5000）后，`node local-dev/dynamic-plugin-e2e.mjs`（`PG_E2E_CONNECTION` 指向开发库 `192.168.50.199:5432/moai_v2`）→ **PASS 102 / FAIL 0 / SKIP 3**，其中本次新增 @DYN-S29~S32（34 条断言）全通过：
    - 守卫拒绝 12 类写操作/多语句（含 `WITH` 内嵌 `DELETE`、`SELECT INTO`、`SET` 会话变量、行注释后跟写操作、MySQL `/*! */` 可执行注释）；
    - 守卫放行 6 条合法只读语句——用「连接失败」这一**后续**错误反证守卫未误拦（前置校验先于连接，不依赖目标库可达）；
    - PG 真实库成功路径：`RowCount=3` + `Truncated=true`（MaxRows=3 截断 5 行）、`SHOW default_transaction_read_only` 返回 `on`（服务端兜底生效）、同名列自动 `id_2`、`bytea` → Base64 `AP8=`、`jsonb`/`timestamptz` 可序列化、空结果集零行且仍带列名。
  - @DYN-S33（MySQL）**SKIP**：本机与同网段（`192.168.50.199:3306/3307`）均无可达 MySQL，Docker 守护进程未运行；断言已按环境变量开关写好，提供 `MYSQL_E2E_CONNECTION` 即可启用。
  - `node local-dev/bocha-search-e2e.mjs` → **PASS 22 / FAIL 0**（回归确认，与本次改动无耦合）。
  - ⚠️ 沙箱内 `dotnet restore` 被 NuGet `ConfigurationDefaults` 阻断（同轮次 30/31/35~40）：依赖 `obj/project.assets.json` 已还原，用 `--no-restore -p:GenerateDependencyFile=false` 编译验证。
  - ⚠️ Windows 下宿主产物目录的 dll 被运行中进程锁定：换 dll 前必须 `taskkill` 占用 :5000 的 `MoAI.exe`（PID 取 `netstat -ano | grep ":5000 .*LISTENING"`），否则替换会被占用失败/静默无效。

- 2026-09-11（JavaScript 执行器内置模板）
  - `dotnet build src/aiplugin/MoAI.AIPlugin.Dynamic/MoAI.AIPlugin.Dynamic.csproj --no-restore -p:GenerateDependencyFile=false` → **0 error、0 告警**（新文件 0 告警：`JsResultKind` 用 `[SuppressMessage("Usage", "CA1720")]` 抑制类型名警告，`JavaScriptExecutorResponse.SerializeScalar` 暂未调用，留作内部 helper）。
  - `Plugins/JavaScriptExecutorPlugin.cs` 走 Jint 4.16.2：内存 4MB / 递归 100 / 超时 4s / 最大语句 1000，每次执行独立 `new Engine`；调用 `engine.Invoke(runFunction, parameters)`，返回 `JsValue` 通过 `IsNull/IsUndefined/IsString/IsBoolean/IsNumber/IsArray/IsObject` 分派归一为 `Parameters/ResultKind/ResultJson`。
  - `MoAI.AIPlugin.Dynamic.csproj` 加 `PackageReference Include="Jint"`（中心化管理已在 `Directory.Packages.props`）。
  - Jint 4.16.2 的 `JsValue.IsNullOrUndefined` 与 `JsValue.Invoke` 已从实例方法移除——分别改用 `IsNull/IsUndefined` 拆分判断、`engine.Invoke(JsValue, object[])` 调用（记入 sdd `javascript_executor` 细节 #4）。
  - 运行态：把新 dll 换入宿主产物目录并重启后端（:5000）后，`node local-dev/dynamic-plugin-e2e.mjs` 扩展断言（DYN-S25~S28） → **PASS 102 / FAIL 0 / SKIP 3**（2026-09-11 复验；该数字含后续轮次新增的 SQL 断言）。
  - ⚠️ 复验时修正 @DYN-S27d：脚本未定义 `run` 时此前直接 `engine.Evaluate("run")` 会抛 Jint `ReferenceError`，用户只能看到不可读的 `run is not defined`，与 @DYN-S27 契约「提示必须定义 run(parameter)」不符；改为先 `engine.Evaluate("typeof run === 'function'")` 探测，缺失时返回 `JavaScript 代码必须定义 run(parameter) 函数`（记入 sdd `javascript_executor` 细节 #4）。
  - ⚠️ 复验时修正 E2E 断言自身缺陷（**非实现缺陷**）：@DYN-S26a/S26b/S28a 的请求体在单引号字符串里写成 `\"`，拼出的是**非法 JSON**（报 `'i' is invalid after a value`）；@DYN-S26b/S26c-string/S26c-array 的期望与契约（`ResultJson` 恒为 JSON 文本）相反。已改为 `JSON.stringify` 构造请求 + `safeParse` 解析后比对，避免对 `\u0022` 转义形式写正则。
  - ⚠️ Jint 的 `TimeoutInterval` 与 `MaxStatements` 在轮询点检查，无法保证毫秒级实时取消；调用方取消靠外层 `WaitAsync` 协作生效（见 sdd 细节 #3）。
  - 沙箱内 `dotnet restore` 被 NuGet `ConfigurationDefaults` 阻断（同轮次 30/31/35~40），宿主整体 `dotnet build` 待系统终端复验；本轮改动落在 `MoAI.AIPlugin.Dynamic` 单子项目，已单独编译通过并准备换入宿主运行验证。

- 2026-09-11（博查 AI 搜索内置模板）
  - `dotnet build src/aiplugin/MoAI.AIPlugin.Dynamic/MoAI.AIPlugin.Dynamic.csproj --no-restore -p:GenerateDependencyFile=false` → **0 error、新文件 0 告警**（集合 DTO 用 `IReadOnlyList<T> { get; init; }`）。
  - `dotnet build src/infra/MoAI.Infra.ExternalHttp/MoAI.Infra.ExternalHttp.csproj --no-restore -p:GenerateDependencyFile=false` → **0 error**（`InfraExternalHttpModule.cs` 0 告警）。
  - 运行态：把 `MoAI.AIPlugin.Dynamic.dll`、`MoAI.Infra.ExternalHttp.dll` 换入宿主产物目录并重启后端后：
    - `node local-dev/dynamic-plugin-e2e.mjs` → **PASS 37 / FAIL 0 / SKIP 2**（新增 @DYN-S19~S21）。
    - `node local-dev/bocha-search-e2e.mjs` → **PASS 22 / FAIL 0**（新脚本，覆盖 @DYN-S16 / @DYN-S21b / @DYN-S22，含响应解析）。
  - 真实上游实测（非桩）：无效 Key 下 AI 搜索返回 `插件执行失败: 博查 AI Search 调用失败（HTTP 401）：{"code":"401","log_id":"10bfe899d9ef3257","message":"Invalid API KEY"}`，证明 `/v1/ai-search` 路径与 `Bearer` 头对真实博查服务有效。
  - ⚠️ 环境注意：本机 shell 设有 `http_proxy/https_proxy`，`curl` 与 .NET `HttpClient` 会经代理，请求行可能变成 absolute-form（`http://host/path`）；桩服务按 `pathname` 匹配，本地探活 curl 建议加 `--noproxy '*'`。
  - ⚠️ 编译证据采集方式修正：`dotnet build ... -t:CoreCompile` **单独执行不可靠**（引用不解析，误报成片 `CS0400/CS0246`）；判 0 error/0 告警请用完整 `dotnet build ... --no-restore -p:GenerateDependencyFile=false`（必要时先 `touch` 源文件强制重编）。上一轮 tdd 中「`-t:CoreCompile` → 0 error」的说法据此更正。
  - 沙箱内 `dotnet restore` 被 NuGet `ConfigurationDefaults` 阻断（同轮次 30/31/35~40），**宿主整体 `dotnet build` 仍待系统终端复验**；本轮改动落在 `MoAI.AIPlugin.Dynamic` 与 `MoAI.Infra.ExternalHttp` 两个子项目，均已单独编译通过并换入宿主运行验证。

- 2026-09-11（博查全网搜索内置模板）
  - `dotnet build src/aiplugin/MoAI.AIPlugin.Dynamic/MoAI.AIPlugin.Dynamic.csproj --no-restore -p:GenerateDependencyFile=false` → **0 error、新文件 0 告警**（集合 DTO 用 `IReadOnlyList<T>` 规避 CA1002）。
  - 运行态：把 `MoAI.AIPlugin.Dynamic.dll` / `MoAI.Infra.ExternalHttp.dll` 换入宿主产物目录并重启后端（:5000）后，`node local-dev/dynamic-plugin-e2e.mjs` → **PASS 30 / FAIL 0 / SKIP 1**。
  - 无效 Key 实测失败信息：`插件执行失败: 博查 Web Search 调用失败（HTTP 401）：{"code":"401","log_id":"...","message":"Invalid API KEY"}`（证明 `IBoChaClient` 注入成功且对外请求真实发出）。
  - 沙箱内 `dotnet restore` 被 NuGet `ConfigurationDefaults` 阻断（同轮次 30/31/35~40），宿主整体 `dotnet build` 待系统终端复验。

- 2026-09-11（PaddleOCR 三内置模板）
  - `dotnet build src/aiplugin/MoAI.AIPlugin.Dynamic/MoAI.AIPlugin.Dynamic.csproj --no-restore -p:GenerateDependencyFile=false` → **0 error、0 告警**（共 7 个 Model + 1 个 Authorization + 3 个 Plugin）。
  - 新模型：`PaddleOcrConfig`（共享 `ApiUrl` + `Token`）、`PaddleOcrRequest/Response/Page`、`PaddleStructureV3Request/Response/Page`、`PaddleVlRequest/Response/Page`——`Page` 子类型独立成文件规避 SA1402。
  - 新插件：`PaddleOcrPlugin`（PP-OCRv5，`POST /ocr`）、`PaddleStructureV3Plugin`（PP-StructureV3，`POST /layout-parsing`，特征字段 `useSealRecognition`）、`PaddleVlPlugin`（PaddleOCR-VL，`POST /layout-parsing`，特征字段 `useLayoutDetection`，强制 `Visualize=true`）。
  - 共享辅助：`MoAI.AIPlugin.Dynamic/PaddleOcrAuthorization.cs::Build` 把 `Token` 拼成 PaddleOCR 协议要求的 `token {value}`，空 Token 短路返回空串（避免给未开启鉴权的部署发无意义的 `Authorization: token `）。
  - 命名空间冲突：`PaddleOcrResponse<T>` 在 infra 同时存在于 `MoAI.Infra.Paddleocr`（Refit 客户端用）与 `MoAI.Infra.Paddleocr.Models`（业务模型用）两个命名空间——插件用 `using InfraPaddle = MoAI.Infra.Paddleocr;` 别名指向 Refit 客户端的类型，保持与 `IPaddleocrClient` 一致。
  - 命名冲突：插件自己的 `PaddleOcrRequest` / `PaddleOcrResponse` 与 `MoAI.Infra.Paddleocr.Models.PaddleOcrRequest` 等业务模型同名——通过 `using InfraPaddleModels = MoAI.Infra.Paddleocr.Models;` 别名访问基础设施类型，避免与插件请求/响应模型混淆。
  - BaseAddress 每次执行重设：`IPaddleocrClient` 由 `AddRefitClient` 注册为 transient，每次插件执行由 DI 新建独立 `HttpClient`，并发运行互不干扰——`_client.Client.BaseAddress = new Uri(_config.ApiUrl.Trim())` 仅影响当前实例。
  - 响应契约：每插件按页聚合 `Pages[]`，文本字段按行拼接（OCR `rec_texts`）；StructureV3 的 `SealTexts` 沿用旧 `NativePlugin.PaddleocrStructureV3Plugin.InvokeAsync` 的「取每条印章 `rec_texts[0]`」语义，改为按印章拆为列表；VL 直接吐 `MarkdownText` / `MarkdownImages`；`prunedResult` 一律 `JsonElement.GetRawText()` 回写为 `PrunedResultJson`。
  - 新 E2E：`local-dev/paddleocr-e2e.mjs`（自建桩服务监听 `/ocr` 与 `/layout-parsing`，对未带 `token ` 的 Authorization 一律返 401，用请求体特征字段区分 VL/StructureV3），覆盖 @DYN-S36~S42；沙箱内 `dotnet restore` 被阻断（同上），脚本待系统终端运行。
  - 沙箱内 `dotnet restore` 被 NuGet `ConfigurationDefaults` 阻断（同轮次 30/31/35~40），宿主整体 `dotnet build` 待系统终端复验；本轮改动落在 `MoAI.AIPlugin.Dynamic` 单子项目，已单独编译通过并准备换入宿主运行验证。

### 2026-09-26 mysql_query/postgres_query 配置离散化复验

- `dotnet build src/MoAI/MoAI.csproj`（独立输出目录，绕开运行中后端的 DLL 锁）→ 0 error。
- `DYN_BASE=http://127.0.0.1:5100 PG_E2E_CONNECTION=<开发库连接串> node local-dev/dynamic-plugin-e2e.mjs` → **PASS 119 / FAIL 0 / SKIP 3**（SKIP：S16/S22 博查真实 Key、S33 无可达 MySQL）；S29c 改断言 Host 示例、S30a/b/S31b 改离散配置、S32a~g 在真实 PG 上全过（含 `SHOW default_transaction_read_only=on` 会话只读兜底）。

### 2026-09-29 P1 运维三插件（http_probe / zabbix_query / redis_query）

- `dotnet build src/MoAI/MoAI.csproj -o .builds/ops-p1`（独立输出目录，运行中后端锁 bin/Debug）→ **0 error**。
- 单测 `dotnet test tests/MoAI.AIPlugin.Dynamic.Tests` → **171/171**（新增 `HttpProbeGuardTests` 内网判定 20 例、`ZabbixResponseParserTests` 严重级/错误归一/时间时长/标签 11 例、`RedisResponseParserTests` INFO 分节/CLIENT LIST/SLOWLOG 组装/长度命令映射 13 例）。
- `node local-dev/ops-p1-plugins-e2e.mjs` → **PASS 39 / FAIL 0**，覆盖 @DYN-S50~S52（内网防护默认拒+放行、http/tcp/dns 三模式、Zabbix body auth 命中子路径与 Bearer 头命中根路径、登录失败归一、Redis 六模式+错误密码归一；桩命中数佐证真实外呼）。
- `DYN_BASE=http://127.0.0.1:5193 node local-dev/dynamic-plugin-e2e.mjs`（独立输出后端）→ **PASS 124 / FAIL 0 / SKIP 4**（SKIP：S16/S22 博查真实 Key、S32/S33 无 PG/MySQL 连接串），含新增 @DYN-S53 注册断言 12 条。
- **实踩坑**：①Refit 方法路径必须以 `/` 开头，相对路径直接抛 `URL path must start with '/'`，且 `/` 开头又与 BaseAddress 子路径互斥 → Zabbix 客户端改 `IHttpClientFactory` 具名客户端 + 手工 Uri 拼接；②StackExchange.Redis 3.x 握手对自建桩不可对齐（`HELLO 3`/`CLIENT SETINFO`/`CONFIG GET` 探针/`ECHO` 二进制 token 校验 + `ScriptResultProcessor InternalFailure`）→ redis_query 改自研 `RedisRespClient` 并移除包依赖；E2E 桩侧对照教训：RESP2 桩解析需 latin1 字节保真（ECHO 回显经 UTF-8 模板字符串会改字节），INFO 握手探针需含 `redis_version`/`role` 与 CONFIG GET `databases` 等键。
- 脚本环境适配：后端以「`.builds/ops-p1/MoAI.dll` + content root 指向 src/MoAI」拉起（不写被锁定的 bin/Debug），`dotnet run` 仅作无独立输出时的回退；Redis 桩支持 `OPP_REDIS_DEBUG=1` 打印握手命令序列（本轮定位 SE.Redis 握手失败的关键手段）。

### 2026-09-29 P2 运维三插件（ssh_executor / grafana_query / sqlserver_query）

- `dotnet build src/MoAI/MoAI.csproj -o .builds/ops-p2`（独立输出目录）→ **0 error**（新增包 SSH.NET 2026.0.0、Microsoft.Data.SqlClient 7.1.0，版本入 Directory.Packages.props）。
- 单测 `dotnet test tests/MoAI.AIPlugin.Dynamic.Tests` → **207/207**（新增 `SshCommandGuardTests` 21 例：白名单放行/空白名单 fail-closed/拼接替换符/管道分段校验/前缀词边界/灾难级黑名单/灾难级递归删除；`GrafanaResponseParserTests` 7 例）。
- `node local-dev/ops-p2-plugins-e2e.mjs` → **PASS 30 / FAIL 0 / SKIP 2**，覆盖 @DYN-S54~S56（守卫用例无条件：空白名单/拼接符/未命中/黑名单压过白名单/递归删除/守卫放行进入连接；Grafana 桩全链：health+Bearer+子路径、annotations RFC3339→毫秒+标签逐发、search、401 归一；SQL Server 守卫先于连接+连接失败归一；真机/真库由 `SSH_E2E_CONNECTION`/`SQLSERVER_E2E_CONNECTION` 门控 SKIP）。
- `DYN_BASE=http://127.0.0.1:5190 node local-dev/dynamic-plugin-e2e.mjs`（P2 独立输出后端）→ **PASS 136 / FAIL 0 / SKIP 4**，含新增 @DYN-S57 注册断言 12 条。
- **实踩坑**：①SSH.NET 2026 的 `ConnectionInfo(host, port, user, methods)` 构造签名已变（`AuthenticationMethod` 迁出 `Renci.SshNet.Common`、端口构造重载消失）→ 改用长期稳定的 `SshClient(host, port, user, password|PrivateKeyFile)` 便捷构造；②Grafana `/api/health` 在真实服务是匿名端点，401 归一用例须打 annotations/search（桩首跑 FAIL 定位）；③桩 E2E 脚本 `KEEP_BACKEND=1` 时被保留的 dotnet 子进程 stdio 管道不关闭会让 node 进程挂住不退出（链式调用被堵）→ finally 中 destroy 管道 + `unref()` + `mock.closeAllConnections()`（P1/P2 两脚本同修）。
- 环境门控：SSH 真机执行设置 `SSH_E2E_CONNECTION="host,port,user,password"`，SQL Server 真库设置 `SQLSERVER_E2E_CONNECTION="host,port,db,user,password"` 后重跑即覆盖对应 SKIP 用例。



### 2026-09-29 P0 运维五插件（alertmanager_query / loki_query / kubernetes_query / dingtalk_webhook_text / wecom_webhook_text）

- `dotnet build src/MoAI/MoAI.csproj -o .builds/ops-p0`（独立输出目录）→ **0 error**（零新 NuGet 包：AM/Loki/K8s 走 factory+手工 Uri，钉钉/企微走 Refit 固定域名）。
- 单测 `dotnet test tests/MoAI.AIPlugin.Dynamic.Tests` → **239/239**（新增 `LokiTimeHelperTests` 8 例：RFC3339/秒/毫秒→纳秒三态归一；`DingTalkSignHelperTests` 3 例：HMAC-SHA256 数据串复算与 URL 编码断言）。
- `node local-dev/ops-p0-plugins-e2e.mjs` → **PASS 30 / FAIL 0 / SKIP 0**，覆盖 @DYN-S58~S62（AM alerts 状态过滤+子路径+Bearer、silences 正则匹配器 ~ 前缀、status；Loki query_range 纳秒归一+Basic、labels/label_values 路径、series、status=error 信封；K8s pods 命名空间路径+选择器、pod_logs 纯文本、events/deployments/nodes、403 权限提示；钉钉桩复算 HMAC 验签、@手机号/@所有人、errcode=310000 归一；企微 key 归一、mentioned_list）。首跑 26/4，4 个 FAIL 全部是桩/接口层问题而非插件逻辑：①桩未实现 AM state 过滤（断言期望过滤后 1 条）；②桩漏了 Loki series 路由；③④**Refit `[Query("name")]` 构造参数是分隔符不是别名**，查询参数名被 camelCase 成 `accessToken`——与既有 `IClickHouseClient` 的 `[Query, AliasAs("...")]` 写法对齐后修复。
- `DYN_BASE=http://127.0.0.1:5188 node local-dev/dynamic-plugin-e2e.mjs`（P0 独立输出后端）→ **PASS 156 / FAIL 0 / SKIP 4**，含新增 @DYN-S63 注册断言 20 条。
- 并行会话协作：本轮构建一度被另一会话 ClickStack 插件 WIP 的编译中间态阻断，按 30s 轮询至恢复后再跑验证（未触碰对方文件）。

### 2026-09-29 ClickStack 查询插件（clickstack_query，HyperDX 对外 API）

- `dotnet build src/MoAI/MoAI.csproj -o .builds/clickstack`（独立输出目录）→ **0 error**（零新 NuGet 包：`IClickStackClient` 走 factory+手工 Uri，同 Zabbix/Grafana 模式）。
- 单测 `dotnet test tests/MoAI.AIPlugin.Dynamic.Tests` → **239/239**（新增 `ClickStackResponseParserTests` 19 例：sources 解析含 metric 表名回退/停用态、search 行集 ToClr 归一与 rows 回退、chart 毫秒桶转 ISO/series 值提取、`{message}`/`{error}` 双错误形态、时间解析无时区按 UTC）。
- `node local-dev/clickstack-e2e.mjs` → **PASS 20 / FAIL 0**，覆盖 @DYN-S64~S66（sources 解析+Bearer 头+错误 Key 401 归一（提示 Personal API Access Key 非 Ingestion Key）+非法协议 BaseUrl 运行时拒绝；search 请求体透传（select/offset/ISO 时间窗）+行集混合类型解析+满额 Truncated+缺省窗口 End-15min+MaxResults/Offset 翻页+404/400 归一；chart epoch 毫秒透传+series 结构+groupBy 数组+缺省窗口 1h/aggFn=count/granularity=1h+sum 缺 Field 等四类 400）。首跑 4/15：15 个 FAIL 全部源于 Infra 注册两处遗漏（`using MoAI.Infra.ClickStack;` 与 `AddTransient<IClickStackClient>`）导致 DI 解析失败——新 infra 客户端「接口/实现/注册+using」四件套缺一不可。
- **实踩坑**：①保存实例不触发 `InitAsync`（凭证/BaseUrl 校验都在运行态，同 Zabbix 口径），非法协议用例从「保存被拒」改为「保存成功+运行被拒」；②场景号 @DYN-S58~S63 被并行 P0 轮占号，改文档前重读最新发现后改用 @DYN-S64~S66（铁律再次生效）；③并行 P0 轮 `LokiTimeHelper`（乘法优先级把 `.ToString()` 绑到字面量）与 `AlertmanagerQueryPlugin`（status 对象误当字符串取）两处编译错误由本轮顺手修复以恢复共享工作区可构建。
- 语义备忘：chart 契约要求 epoch 毫秒且时间必填（与 search 的 ISO 可省不同），插件侧统一补缺省窗（chart=1h、search=15min）；服务端 rows 只给本次行数，Truncated 语义为「达到上限即可能还有更多」。
- 真实实例联调（2026-09-29）：用户接入 192.168.50.199:28000 实测——28000 确认为对外 API server（`GET /api/v2/sources`、`POST /api/v2/charts/series` 无 Key 时均 401「Unauthorized」，即路由存在的正常形态；28080 为 UI，404 会剥掉 `/api` 前缀），但 `POST /api/v2/search` 返回 Express「Cannot POST」404：该 2026-07 前后构建的 `hyperdx:2` 镜像尚无 Search 端点（/api/v1/search 等变体亦 404，无 openapi.json 可查）。处置：升级 `clickstack-app` 镜像获得 Search，过渡期 sources/chart 两模式可用；插件 404 诊断据此增强（Express「Cannot …」形态→「端点不存在/版本过旧/BaseUrl 指向 UI」专属提示），增强后回归 239/239 + E2E 20/20 保持全绿。

### 2026-10-05 模板列表页（实例数与卡片新建）

- `cd ui && npm run typecheck` → 0 error；`npm run lint` → 0 error（9 警告均为既有文件）；`npm run test` → **455/455**（新增 `PluginTemplates.test.tsx` 10 例 + `DynamicPluginInstanceModal.test.tsx` 4 例；`DynamicPluginPanel.test.tsx`/`TeamPlugins.test.tsx` 适配共享模态）；`npm run build` → ✓。
- 实现要点：**零后端改动、零 syncapi**——实例数由前端聚合（系统侧 `GET /ai/plugin/manage/list?kind=dynamic` 全站平铺按 `templeteKey` 计数；团队侧 `GET /team/{id}/plugin/list` 过滤 `kind=dynamic && isTeamOwned`）；三处入口（动态 Tab 原有 + 静态/系统插件 Tab + 团队动态面板）指向 `/plugin/templates`，团队模式带 `?teamId=`；系统/团队两份内联创建表单合并为共享 `DynamicPluginInstanceModal`（`kg_cypher_query` 绑定预填与头像上传随 scope 保留）。
- **实踩坑**：①`feedback` 在 vitest 未注册 antd App 实例时是 no-op（仅 console.warn）→ 查重拦截用例断言「不调保存」的行为而非消息文案；②`vi.mock` 工厂必须覆盖被测模块图引用的**全部**具名导出（共享模态引入 `classifyLabel` 后，旧 classify mock 缺该导出直接报错）；③antd Modal `onOk` 内 `validateFields()` 拒绝需自捕获；创建分支 config 补默认 `'{}'`，顺手修掉「Monaco 显示 `{}` 但表单值实为空导致校验拦截」的隐性怪癖。
