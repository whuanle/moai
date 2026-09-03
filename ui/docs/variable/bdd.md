# 变量页（/variable）行为场景（BDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md)
> 完整 Vitest 断言见 `ui/src/pages/variables/__tests__/Variables.test.tsx`（@FE-VR-Sxx）。

```gherkin
@FE-VR-S1 @auto:vitest
Scenario: 列表渲染与私密掩码
  Given 当前团队有两个变量（普通 WIKI_NAME、私密 FEISHU_SECRET）
  When 打开变量页
  Then 普通变量显示值，私密变量显示掩码且类型标签为私密

@FE-VR-S2 @auto:vitest
Scenario: Admin 可见管理入口
  When Owner/Admin 打开变量页
  Then 显示「新建变量」按钮与操作列

@FE-VR-S3 @auto:vitest
Scenario: Member 只读
  When Member 打开变量页
  Then 不渲染新建按钮与操作列，并显示只读副标题

@FE-VR-S4 @auto:vitest
Scenario: 未选团队引导
  When 未选择团队打开变量页
  Then 显示选择团队引导且不发起请求

@FE-VR-S5 @manual
Scenario: 编辑私密留空保持不变
  When Admin 编辑私密变量且值留空保存
  Then 值不变（服务端替换接口验证）、描述已更新
```
