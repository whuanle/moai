# 变量管理模块设计规格（SDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 上游：[../team/sdd.md](../team/sdd.md) ｜ 证据：[local-dev/variable-e2e.mjs](../../local-dev/variable-e2e.mjs)

- 日期：2026-09-03
- 状态：数据库 + API + 前端已实现；插件参数定义处的 ${key} 引用随插件模块落地
- 领域：`src/variable`（Shared/Core/Api），前端 `ui/src/pages/variables`

## 1. 目标

参考 GitHub Actions 的 vars/secrets 模型，为团队提供变量管理：插件配置等场景以 `${key}` 引用变量，运行时由服务端替换。私密变量（如飞书应用密钥）的值仅 Owner/Admin 可见，成员不可见。

## 2. 数据模型

- `team_variable`：`id / team_id / group_name(50) / key(100) / value(text) / is_secret / description(255)` + 审计（bool 软删除）
- partial 唯一 `(team_id, key) WHERE is_deleted = false`：同团队未删除范围变量名唯一，删除后同名可重建
- **私密变量值 AES 加密落库**（`IAESProvider`，密钥来自 SystemOptions.AES）；普通变量明文
- DDL：`asserts/variable.sql`

## 3. 权限矩阵（Handler 层判定，复用团队角色）

| 操作 | Owner/Admin | Member | 非成员 |
|---|---|---|---|
| 创建/更新/删除 | ✅ | 403 | 404 |
| 列表 | ✅（含私密值） | ✅（私密值恒掩码 null，序列化时整字段省略） | 404 |
| 详情 | ✅（私密值解密返回） | 私密 403 / 普通可见 | 404 |
| 替换 | ✅（结果含私密值） | 403 | 404 |

## 4. API

| 方法 | 路由 | 说明 |
|---|---|---|
| POST | `/api/variable` | 创建 `{teamId, key, groupName?, isSecret, value, description?}` |
| PUT | `/api/variable/{id}` | 更新 `{groupName?, value?, description?}`；value=null 保持不变 |
| DELETE | `/api/variable/{id}` | 软删除 |
| GET | `/api/variable/list?teamId=&groupName=&keyword=` | 列表（含 myRole，私密值掩码） |
| GET | `/api/variable/{id}` | 详情（私密值仅管理员） |
| POST | `/api/variable/substitute` | `${key}` 替换（仅管理员） |

## 5. 替换语义（决策 D3）

- 语法：`${key}`，正则 `\$\{([A-Za-z][A-Za-z0-9_]*)\}`（与变量名规则一致）
- 未匹配到变量的占位符**保留原文**（便于排查配置缺漏）；普通与私密变量均替换（私密解密）
- 服务：`IVariableService.SubstituteAsync(teamId, content)` 供插件运行时在服务端内部调用；面向成员的替换接口不开放（防私密值经响应泄露）

## 5b. 前端设计（/variable 页）

- 页面依赖「当前团队」上下文（`store.currentTeamId` + 侧边栏切换器，未选团队显示引导空态）
- 组件结构与私密编辑三原则（不回填/留空=不变/新值才轮换）、权限渲染矩阵详见 [ui/docs/variable/sdd.md](../../ui/docs/variable/sdd.md)（FE-VR）
- 列表值列：普通明文 `copyable`，私密恒掩码 `••••••••`（数据层已掩码，前端仅兜底）

## 6. 关键决策

- **D1** key 团队内唯一、分组仅组织用途：保证 `${key}` 在团队运行时寻址无歧义
- **D2** 私密值 AES 加密落库 + 接口永不向成员回显（列表/详情均拦截）；编辑时留空表示保持不变
- **D3** 替换服务端内部化（见上）；**D4** key 与类型不可变更（GitHub 风格，避免引用失效）

## 7. 已知问题 / 下阶段

- 插件参数定义处引用 `${key}` 的校验（引用不存在的变量给出提示）随插件模块落地
- 变量分组目前为自由文本，可升级为独立分组表；审计日志（谁在何时读取过私密值）下阶段考虑
