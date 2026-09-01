# MoAI 文档体系标准（DOC-STANDARD）

> 本文件是全部领域文档的**唯一写作规范**。任何 `docs/<模块>/` 与 `ui/docs/<模块>/` 下的四件套必须遵守。修改本文 = 修改所有文档的契约。

## 1. 文档分层（L0-L3）

```
L0 导航层    docs/README.md          ← 全仓库文档地图 + 回归入口（唯一入口，按需跳转）
L1 规范层    docs/cqrs-conventions.md、docs/DOC-STANDARD.md、
             ui/docs/frontend-conventions.md、ui/docs/design-system/*   ← 跨模块不变量
L2 领域层    docs/<模块>/{sdd,bdd,tdd,sop}.md  ← 每模块四件套（本文主体）
L3 证据层    local-dev/*.mjs（可执行验收脚本）、测试文件、命令输出   ← 被引用，不复制内容
```

分层规则：**上层索引不承载内容，下层内容不跨层复制**。需要别层信息时一律链接。

## 2. 四件套职责契约（严格分离，违例即冗余）

| 文件 | 唯一职责 | 禁止出现 |
|---|---|---|
| `sdd.md` | 设计规格：目标、组件、数据、关键决策、已知问题 | 行为场景（→bdd）、测试步骤（→tdd）、操作手册（→sop） |
| `bdd.md` | 行为规格：**纯 Gherkin**（小黄瓜）场景，含场景编号 | 实现细节、curl 命令、文件路径解释（→sdd/tdd/sop） |
| `tdd.md` | 验证映射：场景编号 → 验证物（脚本/单测/走查）→ 结果证据 | 重复场景描述（引用编号即可）、运维操作（→sop） |
| `sop.md` | 运维手册：日常操作、排障表、验收流程（引用 bdd 编号） | 设计论证（→sdd）、完整场景复述（→bdd 编号） |

## 3. Gherkin（小黄瓜）规范

- 每个场景至少 2 个步骤且以 `Then` 收尾；标准形态为 `Given/When/Then`（可加 `And/But`），无前置可省 `Given`，纯断言场景可 `Given+Then`（应尽量补 `When`）；复用前置用 `Background:`；参数化用 `Scenario Outline:` + `Examples:`。
- **场景必须带编号标签**：`@<模块缩写>-S<序号>`（如 `@UM-S1`、`@AUTH-S3`）。编号是四件套相互引用与回归映射的主键，**永久不复用**。
- 关联自动化时加第二标签：`@auto:e2e` / `@auto:vitest` / `@manual`（未自动化）。
- 语言用中文；步骤写业务行为，不写 HTTP/代码细节（那些放 tdd 的映射里）。
- Feature 按业务能力划分（一个 bdd.md 可有多个 Feature）。

## 4. 相互引用契约（每件头部必带）

每个文件顶部固定一个「关联」块，链接另外三件与上下游模块：

```markdown
> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 上游：[../auth/sdd.md](../auth/sdd.md) ｜ 证据：[local-dev/user-management-e2e.mjs](../../local-dev/user-management-e2e.mjs)
```

正文引用场景一律用编号：如「验收执行 [@UM-S12](./bdd.md#um-s12)」。文件内标题锚点格式：场景标签小写（GitHub 风格 `#um-s12`）。

## 5. 篇幅约束

- sdd ≤ 120 行；bdd 场景数即模块行为面（不设上限但每场景 ≤ 8 步）；tdd 映射表为主 ≤ 100 行；sop ≤ 100 行。
- 重复出现在两件中的内容 = 缺陷：删除一处改为链接。

## 6. tdd.md 映射表格式（强制）

```markdown
| 场景 | 验证物 | 结果（日期） |
|---|---|---|
| @UM-S12 | local-dev/user-management-e2e.mjs#grant | PASS 34/34（2026-09-02） |
| @UM-S20 | ui/src/pages/users/__tests__/Users.test.tsx | PASS 3/3（2026-09-02） |
| @UM-S30 | @manual（浏览器走查，见 sop.md 第 N 节） | PASS（2026-09-02） |
```

## 7. 变更流程

改代码 → 更新对应 bdd 场景（增删改编号）→ tdd 补映射并执行 → sdd 已知问题/决策同步 → sop 排障表同步。四件不同步视为文档缺陷。
