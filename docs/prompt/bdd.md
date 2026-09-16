# 提示词（Prompt）行为规格（BDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 上游：[../publication/bdd.md](../publication/bdd.md) ｜ [../classify/bdd.md](../classify/bdd.md) ｜ 证据：[local-dev/prompt-e2e.mjs](../../local-dev/prompt-e2e.mjs)
> 规范：[../DOC-STANDARD.md](../DOC-STANDARD.md)。设计论证见 SDD，本文只写场景。

## Feature: 个人提示词

Background:

- Given 用户 Alice 已注册并登录

@PT-S1 @auto:e2e

### Scenario: 创建个人提示词

- When Alice 提交创建提示词（team_id=0、名称「个人提示词」、内容、prompt 分类）
- Then 创建成功并返回提示词 id，我的列表可见该提示词且分类正确

@PT-S2 @auto:e2e

### Scenario: 创建入参校验

- When 提交空名称、超长名称（>20）、空内容或不存在分类的创建请求
- Then 分别被 400/404 拒绝，不产生数据

@PT-S3 @auto:e2e

### Scenario: 个人提示词仅创建人可见可用

- Given Alice 的个人提示词未上架
- When Bob 查询自己的列表或该提示词详情
- Then Bob 的列表不含该提示词，详情返回 404
- When Alice 查询详情
- Then 可见完整内容

@PT-S4 @auto:e2e

### Scenario: 个人提示词仅创建人可修改

- When Bob 提交修改该提示词
- Then 返回 403
- When Alice 提交修改名称/描述/内容/分类
- Then 修改成功且详情生效

@PT-S5 @auto:e2e

### Scenario: 删除个人提示词

- When Alice 删除自己的提示词
- Then 删除成功，详情变为 404
- When Bob 尝试删除 Alice 的提示词
- Then 返回 403

## Feature: 团队提示词

Background:

- Given 团队 T 拥有 Owner「Tom」与 Member「Mary」

@PT-S6 @auto:e2e

### Scenario: 仅团队管理员可创建团队提示词

- When Mary 提交创建团队提示词
- Then 返回 403
- When 团队外用户 Bob 提交创建
- Then 返回 404
- When Tom 提交创建团队提示词
- Then 创建成功并返回 id

@PT-S7 @auto:e2e

### Scenario: 团队提示词默认只在团队内可见

- When Mary 查询团队提示词列表
- Then 列表包含该团队提示词，且 Mary 可查看详情
- When Bob（非成员）查询该团队列表或详情
- Then 均返回 404

@PT-S8 @auto:e2e

### Scenario: 团队提示词仅管理员可管理

- When Mary 提交修改或删除团队提示词
- Then 均返回 403

## Feature: 申请上架与提示词市场

@PT-S9 @auto:e2e

### Scenario: 个人提示词创建人申请上架

- When Bob 申请上架 Alice 的个人提示词
- Then 返回 403
- When Alice 申请上架
- Then 成功返回审核记录 id，重复申请返回 409
- And 我的列表该项出现待审核申请 id

@PT-S10 @auto:e2e

### Scenario: 撤回上架申请

- When Alice 撤回待审核申请
- Then 撤回成功，列表的待审核申请 id 清空
- When Bob 尝试撤回该申请
- Then 返回 403

@PT-S11 @auto:e2e

### Scenario: 审批通过进入提示词市场

- Given Alice 的个人提示词有待审核上架申请
- When 系统管理员审批通过
- Then 提示词 is_public 置为 true，市场列表对所有用户可见，任意用户可查看详情内容

@PT-S12 @auto:e2e

### Scenario: 团队提示词上架

- When Mary 申请上架团队提示词
- Then 返回 403
- When Tom 申请上架且管理员审批通过
- Then 该提示词进入提示词市场

@PT-S13 @auto:e2e

### Scenario: 市场浏览计数与关键字过滤

- When 非创建人/非团队成员查看一次市场提示词详情
- Then 计数器加一
- When 按名称关键字过滤市场列表
- Then 仅返回命中的提示词

@PT-S14 @auto:e2e

### Scenario: 删除联动清理上架申请

- Given 提示词有待审核上架申请
- When 创建人删除该提示词
- Then 系统同步移除其待审核申请，管理员审核列表不再出现该记录

## Feature: 前端页面

> 菜单单一入口为「提示词市场」（/prompt-market），页头 Tab 切换「提示词市场 / 我的提示词」（/prompts 与 /prompt-market 为同一页面的两个 Tab 路由）；两个 Tab 均以分类列表 + 关键字筛选，卡片列表 + 分页展示，不使用表格。

@PT-S15 @auto:vitest

### Scenario: 提示词中心页渲染（市场 Tab 默认与我的提示词 Tab）

- When 打开「提示词市场」菜单进入页面
- Then 默认显示市场 Tab，加载已上架提示词并渲染分类列表（全部/各分类）、卡片与分页
- When 切换到「我的提示词」Tab
- Then 加载个人提示词，卡片显示「已上架」「待审核」等状态标签

@PT-S16 @auto:vitest

### Scenario: 分类筛选、新建入口、申请上架与删除交互

- When 点击分类列表中的某个分类
- Then 以该分类 id 重新加载当前 Tab 列表
- When 在「我的提示词」Tab 点击「新建提示词」
- Then 跳转到独立编辑器页（/prompts/new）
- When 点击未上架提示词的「申请上架」并提交
- Then 以 resourceType=prompt 调用上架申请接口
- When 点击删除并确认
- Then 调用删除接口

@PT-S17 @manual

### Scenario: 浏览器走查提示词中心、编辑器与团队分区

- When 在浏览器中访问「提示词市场」菜单、页头两个 Tab、独立编辑器与团队详情「提示词」分区
- Then 市场卡片可查看详情（Markdown 渲染）并复制内容；我的提示词卡片可编辑/申请上架/撤回/删除；编辑器左侧 Markdown 编辑、右侧实时预览；团队分区成员只读、管理员可创建/编辑/删除/申请上架

## Feature: 提示词编辑器与头像

@PT-S18 @auto:vitest

### Scenario: 编辑器实时预览与保存

- Given 打开独立编辑器页（新建或编辑）
- When 在左侧输入 Markdown 内容（如一级标题）
- Then 右侧实时渲染出对应 Markdown 元素
- When 保存
- Then 新建以路由团队 id（个人为 0）调用创建接口，编辑调用更新接口且回填详情

@PT-S20 @auto:vitest

### Scenario: 编辑器 Markdown 工具栏

- Given 打开提示词编辑器并输入文本
- When 选中文字点击工具栏「加粗」
- Then 文本被加粗语法包裹且光标落在语法内，右侧实时预览渲染对应样式
- When 点击标题/列表/引用/任务等行级工具
- Then 编辑内核对选区内每行插入或取消对应 Markdown 前缀
- When 点击代码块/表格/分隔线
- Then 在光标处整块插入对应 Markdown 结构

@PT-S19 @auto:e2e

### Scenario: 上传提示词头像

- Given 提示词已存在，且图片已通过存储三段直传完成登记
- When 他人设置该个人提示词头像
- Then 返回 403
- When 创建人（团队提示词为 Admin+）设置头像
- Then 成功且详情返回该头像 objectKey
- When 使用未登记的 objectKey 设置头像
- Then 返回 404
