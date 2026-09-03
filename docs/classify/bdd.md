# 分类管理（Classify）行为规格（BDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 上游：[../user-management/bdd.md](../user-management/bdd.md) ｜ 证据：[local-dev/classify-e2e.mjs](../../local-dev/classify-e2e.mjs)
> 规范：[../DOC-STANDARD.md](../DOC-STANDARD.md)。设计论证见 SDD，本文只写场景。

## Feature: 分类列表查询（管理员）

### Scenario: 查询 plugin 类型分类列表

- Given 我是管理员，且存在 plugin 类型分类
- When 我请求查询 plugin 类型分类列表
- Then 返回 plugin 类型全部分类，且每项含 name 与 description

@CLS-S1 @auto:e2e

### Scenario: 查询 app 类型分类列表

- Given 我是管理员，且存在 app 类型分类
- When 我请求查询 app 类型分类列表
- Then 返回 app 类型全部分类，且每项含 name 与 description

@CLS-S2 @auto:e2e

### Scenario: 查询 kb 类型分类列表

- Given 我是管理员，且存在 kb 类型分类
- When 我请求查询 kb 类型分类列表
- Then 返回 kb 类型全部分类，且每项含 name 与 description

@CLS-S3 @auto:e2e

## Feature: 分类维护（管理员）

### Scenario: 新增分类

- Given 我是管理员
- When 我提交新增 plugin 类型分类名称「测试插件分类」
- Then 返回该分类 id，且刷新列表可见该分类出现在 plugin 分类中

@CLS-S4 @auto:e2e

### Scenario: 新增同类型同名分类被拒

- Given 已存在 plugin 类型分类「测试插件分类」
- When 我再次提交新增 plugin 类型同名分类
- Then 返回 409，提示「分类名称已存在，请更换后重试」

@CLS-S5 @auto:e2e

### Scenario: 删除仍被引用的插件分类被拒

- Given plugin 类型分类「测试插件分类」下仍存在插件
- When 我删除该插件分类
- Then 返回 409，提示「该分类下仍存在插件，无法删除」

@CLS-S6 @auto:e2e

### Scenario: 修改分类

- Given 已存在 plugin 类型分类「测试插件分类」
- When 我将该类名称改为「新插件分类」
- Then 返回成功，且刷新列表分类名称为「新插件分类」

@CLS-S7 @auto:e2e

### Scenario: 修改为同类型已占用名称被拒

- Given 已存在 plugin 类型分类「分类A」「分类B」
- When 我将「分类B」改名为「分类A」
- Then 返回 409，提示「分类名称已存在，请更换后重试」

@CLS-S8 @auto:e2e

### Scenario: 删除未被引用的分类

- Given 已存在 plugin 类型分类「待删除分类」，且其下无插件
- When 我删除该分类
- Then 返回成功，且刷新列表不再出现该分类

@CLS-S9 @auto:e2e

## Feature: 权限与校验

### Scenario: 非管理员访问被拒

- Given 我是普通成员
- When 我请求查询分类列表
- Then 返回 403，提示「只有管理员可以管理分类」

@CLS-S10 @auto:e2e

### Scenario: 名称与描述长度校验

- Given 我是管理员
- When 我提交名称超过 20 字符或描述超过 255 字符的分类
- Then 返回 400 参数校验错误

@CLS-S11 @auto:e2e
