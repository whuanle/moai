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
├── MoAI.Settings.Shared/  Commands/SaveSettingCommand({key,value})
│                          Queries/QuerySettingsCommand + Responses（SettingItemResponse：Key/Name/Description/Value）
│                          Models/Neo4jKnowledgeGraphSettings + Services/ISettingsService、IKnowledgeGraphSettingsService
├── MoAI.Settings.Core/    Handlers/{Query,Save}SettingHandler（薄委托）+ Services/SettingsService（校验与读写）
│                          + Services/KnowledgeGraphSettingsService（读取知识图谱四项，未开启时返回 Enabled=false）
└── MoAI.Settings.Api/     SettingsController（[Route("/settings")]，门禁在此）
src/database/…/Seed/       SettingDefinition(s)/SettingSeed（注册表 + 种子：{Id=1, key="root", value="1"}）
                           SettingConfiguration（key≤50 哈希索引、name 20、description 255、value 2000）
ui/src/                    api/settings.ts（SettingKeys 常量 + get/save）、pages/settings/Settings.tsx（/settings，知识图谱卡片仅 root 可见）
```

## API 契约

| 方法 | 路由 | 门禁 | 说明 |
|---|---|---|---|
| GET | `/api/settings` | `IsAdmin`（root 隐含满足），否则 403「只有管理员可以访问设置项」 | 返回全部内置项 `{key,name,description,value}` |
| PUT | `/api/settings` | 仅 `IsRoot`，否则 403「只有超级管理员可以修改设置项」 | `{key, value}` 均 string |

认证由 `ApiApplicationModelConvention` 自动追加 `[Authorize]`。

知识图谱组为逐 key 读写（无批量接口）：前端保存时先写 `OPEN_NEO4J`，开启状态下再依次写 `NEO4J_URI`/`NEO4J_USERNAME`/`NEO4J_PASSWORD`；关闭时不写连接项（[@SET-S17](./bdd.md#set-s17)）。

## 关键决策

1. **查询合并默认值**：以 `SettingDefinitions.All` 为基准遍历，库有记录用记录值、无记录返回 `DefaultValue`——GET 永远返回全部内置项，不因种子缺失漏项。
2. **保存校验**：`SettingDefinitions.Find(key)`（忽略大小写）未命中 400「无效的配置项.」；库中无记录时自动以内置 Key/Name/Description 插入新行（首次写入自动建行，[@SET-S8](./bdd.md#set-s8)），有记录仅更新 value。
3. **`key="root"` 系统级保护**：不在白名单内，不能经设置接口读写（保存得 400），只能改库维护（[@SET-S6](./bdd.md#set-s6)）。
4. **value 约定**：字符串存储（可承载 JSON），布尔统一 `"true"`/`"false"`；`setting` 表无应用层缓存，**写入即刻生效于下一个读请求**。
5. 门禁依赖 `IUserAccountService.GetUserStateAsync`（Redis `userstate:{userId}` 1h，[../account/sdd.md](../account/sdd.md)）；root/admin 变更后需失效缓存才即时生效（user-management 写操作已负责）。
6. 前端**固定字段渲染**（非动态表单）：新增设置项须同步改 `Settings.tsx` 与 i18n（步骤见 [../settings.md](../settings.md)）；当前页面唯一内容为 root 专属知识图谱卡片，非 root（含普通 admin）整页重定向 /dashboard（后端 PUT 亦仅 root，为最终防线）；脏检查控制保存按钮，保存失败重新加载回滚为库值。
7. **知识图谱组语义**：`OPEN_NEO4J="true"` 才认为能力开启；读取经 `KnowledgeGraphSettingsService` 归一（未开启直接返回 `Enabled=false`，不回传连接信息），连接项值经 `SettingDefinitions.Find` 兜底默认空串。密码明文存 `setting.value`（≤2000），与现有设置项一致，不做加解密。
8. **能力暴露而非实现**：本模块只提供开关与连接配置的读写及 `IKnowledgeGraphSettingsService` 读取出口；Neo4j 驱动、图谱抽取与检索属知识库后续迭代，不在本模块内。

## 已知问题

- 无未修复缺陷。
- 遗留观察（全局，非本模块缺陷）：GET /api/settings 为 admin 专属，member 得 403（有意设计，见 [@SET-S2](./bdd.md#set-s2)）；root 变更后权限最长滞后 1 小时（用户态缓存）。
- 遗留观察：`NEO4J_PASSWORD` 明文存储且 `GET /api/settings` 面向 admin 返回全部值，因此非 root 的 admin 亦可通过接口读到密码；前端知识图谱卡片仅 root 渲染（[@SET-S18](./bdd.md#set-s18)），后续如需严格隔离应在查询层按角色裁剪敏感值。
