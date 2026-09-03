# 变量页（/variable）设计规格（SDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 上游：[../../../docs/variable/sdd.md](../../../docs/variable/sdd.md)（变量契约）、[../page-admin/sdd.md](../page-admin/sdd.md)（管理页基准） ｜ 证据：`ui/src/pages/variables/__tests__/Variables.test.tsx`
> 规范：[../../../docs/DOC-STANDARD.md](../../../docs/DOC-STANDARD.md)。行为场景见 BDD（@FE-VR-Sxx），本文不重复。

## 目标

团队变量的管理页：随「当前团队」上下文切换（AppSider 选择器），Owner/Admin 管理、Member 只读普通变量。依赖 `store.currentTeamId`；未选团队显示引导空态（同 [[../page-account/sdd.md|账号页]] 的上下文依赖模式）。

## 组件（Variables.tsx）

- 结构：`Page`（title；成员附只读副标题，Admin+ 无副标题——2026-09-02 管理页约定）+ `extra` 新建按钮（Admin+）+ `QueryBar`（分组 Select 选项由当页数据去重派生 + 关键字 Input）+ `DataTable`。
- 列：变量名（`Text code` 纯名称，不带 `${}` 装饰——引用语法只在插件配置中出现）/ 分组 / 类型 Tag（私密红色）/ 值（私密恒 `••••••••` 掩码；普通 `copyable`）/ 描述 / 更新时间（`formatDateTime`）/ 操作（编辑/删除，Admin+）。
- 未选团队：居中空态「请先在左上角选择一个团队，或 前往创建团队」（Link → /team）。

## 新建/编辑 Modal

- 字段：变量名（仅创建可填，正则 `^[A-Za-z][A-Za-z0-9_]{0,99}$`）/ 分组（≤50）/ 类型 Switch（仅创建可切）/ 值 TextArea（monospace）/ 描述（≤255）。
- **私密编辑三原则**：打开不回填值（防 DOM 泄露）；留空提交 = 保持不变（`value: undefined`）；提供新值才轮换。普通变量编辑回填当前值。
- 提交后仅刷新本页数据；类型与变量名不可变更（后端契约）。

## 权限渲染（镜像后端，后端仍是最终防线）

| 元素 | Owner/Admin | Member |
|---|---|---|
| 新建按钮 / 操作列 / 删除 | ✅ | 不渲染 |
| 值列 | 普通明文 / 私密掩码 | 同左（数据层已掩码） |
| 只读副标题 | 无 | 显示 |

## 状态与请求

- 数据：`api/variable.ts` → Kiota 客户端；`getVariables(teamId, {groupName, keyword})`。
- 筛选：QueryBar 外部 `filterForm` 实例，搜索/重置共用 `applyFilters()`（重置后读清空值再拉取）。
- 团队上下文缺失（未选团队）时不发请求，直接渲染空态。
