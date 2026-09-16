# 提示词（Prompt）操作手册（SOP）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 证据：[local-dev/prompt-e2e.mjs](../../local-dev/prompt-e2e.mjs)
> 规范：[../DOC-STANDARD.md](../DOC-STANDARD.md)。设计论证见 SDD，场景见 BDD 编号，本文只写操作。

## 1. 日常操作

| 操作 | 入口 | 说明 |
|---|---|---|
| 创建个人提示词 | 一级菜单「提示词市场」→ 页头「我的提示词」Tab → 新建提示词 | 进入独立编辑器页，左侧 Markdown 编辑、右侧实时预览 |
| 上传提示词头像 | 编辑器页头像位上传图片 | JPG/PNG ≤5MB，走存储直传，详情/列表/市场展示 |
| 创建团队提示词 | 团队详情 → 提示词 → 新建提示词 | 仅团队 Owner/Admin，成员只读可用 |
| 申请上架 | 「我的提示词」Tab 卡片「申请上架」→ 填申请说明 | 个人提示词创建人本人；团队提示词 Admin+ |
| 撤回上架申请 | 「我的提示词」Tab 卡片「撤回申请」（待审核状态） | 仅待审核可撤回，权限同申请 |
| 审批上架 | 系统管理员「审批上架」菜单（/publications） | 通过后 `is_public=true`，进入提示词市场 |
| 使用市场提示词 | 一级菜单「提示词市场」→ 市场卡片「使用」 | 详情弹窗内一键复制内容；他人每次查看计数 +1 |
| 提示词分类 | 管理员「分类管理」prompt 页签 | `prompt_class_id` 必须指向 prompt 类型分类 |

## 2. 排障表

| 现象 | 原因 | 处置 |
|---|---|---|
| 创建提示词 404「分类不存在」 | `prompt_class_id` 不是 prompt 类型分类 | 管理员在分类管理建 prompt 分类后重试 |
| PUT 提示词 400 | 请求体缺 `name`/`content` 或超长（名称 20/内容 10000） | 按 400 errors 提示修正 |
| 团队分区看不到「新建提示词」 | 当前账号是 Member | 属预期（@PT-S8），找团队管理员 |
| 个人提示词他人详情 404 | 未上架个人提示词对他人不可见（防探测） | 属预期（@PT-S3）；上架后可见 |
| 审批通过 404「提示词不存在或已删除」 | 审批前资源被删除 | 只能驳回该申请（publication 状态机） |
| 存量库查询 prompt 报表不存在 | 库早于 prompt 表创建 | 执行 `asserts/prompt.sql`（见下） |

存量库建表：

```bash
docker exec -i moai-postgres psql -U postgres -d moai < asserts/prompt.sql
```

## 3. 验收流程（对应 [@PT-S17](./bdd.md#pt-s17) 走查）

1. 后端 `cd src/MoAI && dotnet run`，前端 `cd ui && npm run dev`。
2. 普通账号登录 → 「提示词市场」菜单 → 页头切到「我的提示词」Tab → 新建 → 进入编辑器页，用工具栏「H2/加粗」插入语法右侧实时渲染（也可手输 Markdown），上传头像后保存 → 卡片出现「未上架」且带头像。
3. 点卡片「申请上架」→ admin 在「审批上架」通过 → 刷新出现「已上架」，页头「提示词市场」Tab 可见。
4. 另一普通账号打开市场 → 使用（复制内容，详情 Markdown 渲染）→ 计数 +1。
5. 团队 Owner 在团队详情「提示词」分区新建（同一编辑器页）→ Member 只读可见；Owner 申请上架走同一审批链。

## 4. 回归命令

```bash
dotnet build src/MoAI/MoAI.csproj
cd ui && npm run typecheck && npm run lint && npm run test
node local-dev/prompt-e2e.mjs          # PT 46/46
node local-dev/publication-e2e.mjs     # PB 34/34（上架链路回归）
```
