# 系统设置（Settings）设计规格（SDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 上游：[../account/sdd.md](../account/sdd.md)（用户态与门禁依赖） ｜ 下游：[../auth/sdd.md](../auth/sdd.md)（oauth_auto_register 读取方） ｜ 规范：[../settings.md](../settings.md)（设置项编写规范） ｜ 证据：[local-dev/audit-345.mjs](../../local-dev/audit-345.mjs)
> 规范：[../DOC-STANDARD.md](../DOC-STANDARD.md)。行为场景见 BDD（@SET-Sxx），本文不重复。

## 目标

一组**固定**的系统配置项（当前内置：`oauth_auto_register`「允许第三方账号登录直接创建账号」，默认 `"false"`）。定义集中在 `SettingDefinitions` 注册表（单一事实来源），可调整的只有**值**。提供查询全部设置项与保存单个设置项两个接口，供前端 `/settings` 使用。

角色门禁（只在 Controller 层判断，禁止下沉 Handler）：**查询**要求 admin+；**保存**仅 root。root 判定为 `setting` 表 `key="root"` 的 value = 用户 id（种子 id=1 即 admin）。

## 组件

```
src/settings/
├── MoAI.Settings.Shared/  Commands/SaveSettingCommand({key,value})
│                          Queries/QuerySettingsCommand + Responses（SettingItemResponse：Key/Name/Description/Value）
│                          Services/ISettingsService
├── MoAI.Settings.Core/    Handlers/{Query,Save}SettingHandler（薄委托）+ Services/SettingsService（校验与读写）
└── MoAI.Settings.Api/     SettingsController（[Route("/settings")]，门禁在此）
src/database/…/Seed/       SettingDefinition(s)/SettingSeed（注册表 + 种子：{Id=1, key="root", value="1"}）
                           SettingConfiguration（key≤50 哈希索引、name 20、description 255、value 2000）
ui/src/                    api/settings.ts（SettingKeys 常量 + get/save）、pages/settings/Settings.tsx（/settings）
```

## API 契约

| 方法 | 路由 | 门禁 | 说明 |
|---|---|---|---|
| GET | `/api/settings` | `IsAdmin`（root 隐含满足），否则 403「只有管理员可以访问设置项」 | 返回全部内置项 `{key,name,description,value}` |
| PUT | `/api/settings` | 仅 `IsRoot`，否则 403「只有超级管理员可以修改设置项」 | `{key, value}` 均 string |

认证由 `ApiApplicationModelConvention` 自动追加 `[Authorize]`。

## 关键决策

1. **查询合并默认值**：以 `SettingDefinitions.All` 为基准遍历，库有记录用记录值、无记录返回 `DefaultValue`——GET 永远返回全部内置项，不因种子缺失漏项。
2. **保存校验**：`SettingDefinitions.Find(key)`（忽略大小写）未命中 400「无效的配置项.」；库中无记录时自动以内置 Key/Name/Description 插入新行（首次写入自动建行，[@SET-S8](./bdd.md#set-s8)），有记录仅更新 value。
3. **`key="root"` 系统级保护**：不在白名单内，不能经设置接口读写（保存得 400），只能改库维护（[@SET-S6](./bdd.md#set-s6)）。
4. **value 约定**：字符串存储（可承载 JSON），布尔统一 `"true"`/`"false"`；`setting` 表无应用层缓存，**写入即刻生效于下一个读请求**。
5. 门禁依赖 `IUserAccountService.GetUserStateAsync`（Redis `userstate:{userId}` 1h，[../account/sdd.md](../account/sdd.md)）；root/admin 变更后需失效缓存才即时生效（user-management 写操作已负责）。
6. 前端**固定字段渲染**（非动态表单）：新增设置项须同步改 `Settings.tsx` 与 i18n（步骤见 [../settings.md](../settings.md)）；非 admin 整页重定向 /dashboard（后端为最终防线）；脏检查控制保存按钮，保存失败重新加载回滚为库值。

## 已知问题

- 无未修复缺陷。
- 遗留观察（全局，非本模块缺陷）：GET /api/settings 为 admin 专属，member 得 403（有意设计，见 [@SET-S2](./bdd.md#set-s2)）；root 变更后权限最长滞后 1 小时（用户态缓存）。
