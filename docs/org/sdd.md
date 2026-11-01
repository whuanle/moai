# 组织（Org，ORG）设计规格（SDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md)（@ORG-Sxx） ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 上游：[../account/sdd.md](../account/sdd.md)（用户与 IsAdmin 门禁）、[../team/sdd.md](../team/sdd.md)（团队概念边界） ｜ 证据：[local-dev/org-e2e.mjs](../../local-dev/org-e2e.mjs)
> 规范：[../DOC-STANDARD.md](../DOC-STANDARD.md)。行为场景见 BDD，本文不重复。

## 目标

提供**全局层级「组织」树**——即**通讯录层级**（集团组织架构：董事长/CEO→部门→…），组织下可再挂组织；每组织维护**物化路径 `path`**，子树判定只需前缀匹配，无需按 parentId 递归展开。用户与组织多对多绑定，管理员统一维护。支持**默认组织**：设置后新注册用户（含 OAuth 自动建号）自动获得，可清空。

**概念边界**：组织与团队是两个正交维度——团队是**协作与资源隔离单元**（资源按 team_id 隔离、团队内成员协作维护，见 [../team/sdd.md](../team/sdd.md)）；组织**不隔离资源、不承载协作**，只做「用户 ↔ 组织架构」绑定，定位是**授权维度**：把资源定向放通给某条组织线（如生产团队的应用开放给「管理层」，使用者无需加入生产团队）时，按组织成员圈定范围，设计见下文「统一组织授权」。

**轻量定位**：MoAI 不是 OA/ERP——组织只做通讯录层级树 + 用户绑定 + 授权维度，**不搞复杂组织管理与权限设计**（岗位/职级/汇报线/审批流/多维组织模型等一律不引入）。代码符号沿用历史命名（`OrgRole`/`org_role`/`/org/role/*` 路由、`OrgRoleId` 字段），概念名统一为「组织」。

BI 等子系统的组织快照接口为后续轮次，本期只做管理与数据面。

## 角色与门禁

| 角色 | 判定 | 能力 |
|---|---|---|
| 管理员 | `UserStateInfo.IsAdmin`（root 天然蕴含） | 组织树增删改、父子调整、用户组织分配 |
| 普通用户 | 其余 | 管理面 403，前端重定向 /apps |

门禁在 Controller 层 `EnsureAdminAsync`（IsAdmin 口径，与 ClassifyController 同款）；树合法性校验（防环/重名/深度）在 Handler。

## 数据模型

- `org_role`（`OrgRoleEntity`，IFullAudited 软删除）：Id(int 自增)/Name(≤50，同父唯一)/ParentId(int?，null=根)/Path(≤255)。
  - **path 语义**：根为 `/{id}/`，子级逐段追加（如 `/1/3/8/`）；**带尾斜杠保证前缀匹配无歧义**（`LIKE '/1/3/%'` 不会误中 `/1/30/`）；未删除记录内唯一索引兜底。
  - **不设固定层级上限**，仅受 path 列长 255 约束（int id 最长约 10 位，可承载 20+ 层）。
- `org_user_role`（`OrgUserRoleEntity`，IFullAudited）：UserId+RoleId 复合唯一索引（filter is_deleted=0），同 TeamUser 范式，不建物理外键。
- **默认组织**：设置表键 `ORG_DEFAULT_ROLE`（`SettingDefinitions` 注册，Value=组织 id 字符串、空=无默认，与 `root` 键存用户 id 同构）；`PUT /org/role/default`（管理员）设置/清空，全局唯一后设覆盖，**只影响新注册用户不追溯存量**；删除默认组织时同事务清空该设置。
- 存量库升级：启动时幂等 DDL 自动补表（[DatabasPostgresModule](../../src/database/MoAI.Database.Postgres/DatabasPostgresModule.cs)，与 asserts/org.sql 等价），或手工执行 [asserts/org.sql](../../asserts/org.sql)；设置键由 [asserts/setting.sql](../../asserts/setting.sql) 幂等补种。

## 组件

```
src/org/
├── MoAI.Org.Shared/   Commands/{Create,Update,Delete}OrgRoleCommand、UpdateOrgUserRolesCommand、UpdateOrgRoleDefaultCommand、SetOrgGrantsCommand
│                      Queries/{QueryOrgRoles,QueryOrgRoleUsers,QueryUserOrgRoles}Command(+Responses)、QueryOrgGrant*Command、CheckOrgGrantCommand(+Responses)
│                      Services/{IOrgGrantService,IOrgGrantCache,OrgGrantModels}（统一组织授权契约，供消费方模块引用）
├── MoAI.Org.Api/      OrgController（/org/role/*、/org/role/default、/org/user/{userId}/roles）、OrgGrantController（/org/grant/*，全部 EnsureAdminAsync）
└── MoAI.Org.Core/     Handlers×14、OrgRoleTreeGuard（父校验/同父重名/path 拼接与数值层级序比较）、OrgGrantGuard（资源类型解析）
│                      Services/{UserRegistrationOrgRoleHook（注册默认组织钩子）,OrgGrantService（授权判定，[InjectOnScoped]）,OrgGrantRedisCache（三 key 缓存）}
│                      注：组织成员查询返回用户资料字段（Email/Phone/Avatar/IsAdmin/IsRoot/IsDisable，与用户管理列表对齐）
src/account/MoAI.Account.Shared/  Services/IUserRegistrationHook（注册增强钩子契约：Auth 与 Org 的最低公共层）
src/auth/MoAI.Auth.Core/          RegisterUserCommandHandler（唯一运行时建号点，建号后调钩子）
src/database/          OrgGrantEntity + OrgGrantEnums + OrgGrantConfiguration；DatabasPostgresModule 幂等 DDL；asserts/org.sql
ui/src/                api/org.ts、pages/admin/{TeamsPanel,OrgRolesPanel}.tsx
                      （AdminTeams 为「组织」双 Tab 容器；组织 Tab 左侧组织树 + 右侧选中组织成员列表）
tests/MoAI.Org.Tests/  sqlite 内存库单测 55 例（含 OrgGrantServiceTests 14 例，InMemoryOrgGrantCache 假缓存）
```

## 关键决策

1. **path 由自增 id 拼出**：创建走事务内两段保存（先拿 id 再写 path）；调整上级时**一次 SaveChanges 整体重写子树 path**（新 path = 新父 path + 本组织段 + 原相对后缀），避免逐层递归。
2. **防环用 path 前缀**：新父 path 以本组织 path 为前缀（含自身）即 400，等价于"不能移到自身或其下级之下"。
3. **排序用 path 分段数值比较**（`OrgRoleTreeGuard.ComparePath`）：纯字符串序在 id 位数不同时乱序（`"/10/" < "/7/"`），数值比较保证父先于子且同级按 id 数值序；排序在内存侧做（sqlite 单测兼容惯例）。
4. **路由回填 id 不进请求体校验**：`Update*.Command` 的路由 id（orgRoleId/userId）不在 `IModelValidator` 声明规则——自动校验发生在模型绑定阶段、先于 Controller 回填，校验它会把所有 PUT 先拦成 400（实踩，见 rounds-log）；存在性在 Handler 判 404。
5. **同父重名 409，不同父允许同名**；删除有子组织 400，删除叶子级联解绑用户绑定（一次 SaveChanges）。
6. **用户组织整体覆盖语义**：`PUT /org/user/{userId}/roles` 全量覆盖该用户组织集合（空=清空），差集增删；成员数即时回显。
7. **前端并入「组织用户」菜单**：不新增路由/菜单项，`/admin/teams` 菜单文案为「组织用户」，页内 Tab 切换团队管理/组织管理/用户管理（用户管理 2026-11-01 自独立 `/users` 页并入第三 Tab，旧地址重定向 `?tab=users` 平滑落位）；组织 Tab 为**左侧组织树 + 右侧选中组织成员列表**（头部为组织操作区：编辑/设默认·取消默认/删除）。树顶固定**虚拟「默认根」节点**（非库表实体、不可选中，空树时也展示，顶级组织 parentId=0 挂其下）；**无全局新建按钮**，每节点（含默认根）右侧 + 按钮就地新建，**表单仅名称一项**（层级由入口上下文决定）；**调整层级用拖拽**——拖入节点内=成为下级、拖到节点间=同级换挂（挂到目标的上级）、拖到默认根=升顶级，前端按 path 前缀拦截自身子树（后端防环兜底）；同级无独立排序，按 id 数值序展示。
8. **默认组织经注册钩子赋予**：仓库无 MediatR 事件先例，沿用「接口定义在 Shared、实现注册在 Core、宿主组合」惯例——契约 `IUserRegistrationHook` 放 Account.Shared（Auth 与 Org 均已引用的最低公共层）；`RegisterUserCommandHandler` 是唯一运行时建号点（OAuth 自动建号内部复用它），钩子在**第一次 SaveChanges 之后**调用（自增 id 才可用，org_user_role 无物理外键不会自动修正），追加变更随第二次 SaveChanges 提交（OAuth 外层 TransactionScope 内则整体原子）；钩子**尽力而为**：组织缺失/异常静默跳过，绝不影响注册主流程。

## 统一组织授权（org_grant，2026-11-01 落地）

跨资源复用的组织授权表（定位见上方「概念边界」）：一条记录 = 资源 (type, id) × 单个组织 × 方向规则。授权面向**全站用户按组织归属**生效，与团队隔离正交——补齐「团队内使用 ↔ `is_public` 全员公开（publication）」之间的中间粒度；应用发布授权是首个规划消费方（后续轮次接入）；**子系统使用授权已接入**（2026-11-01，资源类型 subsystem：`/subsystem/{id}/org-grants` 管理 + 委托 SSO 签发门禁与团队授权取并集，见 [../subsystem/sdd.md](../subsystem/sdd.md) 关键决策 5/8）。

- **表 `org_grant`**（`OrgGrantEntity`，IFullAudited 软删除，无物理外键）：resource_type（`OrgGrantResourceType` 枚举存 int：app/wiki/knowledgeGraph/prompt/skill，新资源扩枚举接入）/ resource_id（**varchar(64) 字符串**——应用 Guid 与知识库 long 统一转字符串）/ org_role_id / scope（`OrgGrantScope`：self 仅该组织 / up 含祖先「R 及以上」/ down 含子树「R 及以下」）。唯一 partial 索引 `(resource_type, resource_id, org_role_id) WHERE is_deleted=0`（重复授权=改写方向）。DDL 三处与 org_role 同款（EF 配置 / DatabasPostgresModule 幂等 DDL / asserts/org.sql）。
- **判定语义**（path 前缀匹配，零递归）：记录 (R, scope) 对用户组织 X——self `X.path == R.path`；up `R.path` 以 `X.path` 为前缀（R 及以上：报表应用选各部门「经理」+ up，经理及以上可见、组员不可见）；down `X.path` 以 `R.path` 为前缀（R 及以下）。多记录、用户多组织均取**并集**；**无授权记录不放行**（纯组织维度判定，消费方与团队逻辑 OR 组合）。
- **领域服务**：契约 `IOrgGrantService` 在 Org.Shared/Services（消费方只引用 Shared，namespace MoAI.Org.Services），实现 `[InjectOnScoped]` `OrgGrantService`。方法：ReplaceGrantsAsync（整体覆盖，同组织重复 400）/ GetGrantsAsync（回显组织名与 path）/ IsAllowedAsync / ListAllowedRolesAsync（按方向展开，`OrgRoleTreeGuard.ComparePath` 排序）/ ListAllowedUserIdsAsync（展开组织绑定用户并集）/ ListAllowedResourceIdsAsync（反查用户可见资源供列表页）；resourceId 提供 long/Guid/string 三形态（内部统一字符串）。
- **Redis 缓存**（read-through + 1h TTL + 写后显式 KeyDelete；契约 `IOrgGrantCache` 供单测换假实现）：`orggrant:{type}:{id}`（授权 id+scope，**不存 path**）/ `org:userroles:{userId}`（组织 id）/ `org:rolepaths`（全量 id→path，整树一把 key）。失效映射：ReplaceGrants→资源 key；UpdateOrgUserRoles→用户 key；Create/Update/DeleteOrgRole→rolepaths；DeleteOrgRole 同事务级联软删引用授权并逐资源删 key、解绑用户删 key。读路径全部 try/catch 降级直查 DB，缓存故障不影响鉴权；TTL 兜底一致性。
- **管理面**（`OrgGrantController`，/org/grant/*，全部 EnsureAdminAsync）：`PUT {resourceType}/{resourceId}`（SetOrgGrantsCommand，resourceType 收枚举名/json 名字符串，`OrgGrantResourceTypes.TryParse` 解析）/ GET 回显 / roles（展开有效组织）/ users（有效用户）/ check?userId=（判定）——供 e2e 验证与未来管理页；消费方仍走进程内服务。

## 后续规划（本期不做）

- BI 子系统组织快照只读接口（挂子系统机器面 + org:read 开关）与 `org.changed` MQ 事件——BI 对接轮次落地，path 届时直接进快照，BI 用前缀匹配做可见范围（与统一组织授权同源 path）。

## 已知问题

- 无。E2E/单测全绿（见 [TDD](./tdd.md)）。
