# 变量页（/variable）操作手册（SOP）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 上游：[../../../docs/variable/sop.md](../../../docs/variable/sop.md)（后端/角色/排障总册）

本页为团队变量的前端入口；变量概念、角色定义、`${key}` 引用语法与后端验收脚本见 [../../../docs/variable/sop.md](../../../docs/variable/sop.md)。本页补充页面专属操作：

1. 先在左上角「团队」选择器切换目标团队；未选团队时页面给出引导。
2. Member 打开本页为只读（无新建/操作列），只能查看普通变量的值。
3. 编辑私密变量时值框为空——这是刻意设计（不回填防泄露），直接保存即"保持不变"。
