# 变量管理模块行为场景（BDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md)

## Feature: 访问与权限

```gherkin
@VR-S1 @auto:e2e
Scenario: 未登录不能访问
  When 未携带令牌查询变量列表
  Then 返回未认证

@VR-S3 @auto:e2e
Scenario: 管理权限
  When Member 创建/更新/删除变量
  Then 返回禁止
  When 非成员执行任意操作
  Then 返回不存在
```

## Feature: 创建与校验

```gherkin
@VR-S2 @auto:e2e
Scenario: 校验
  When 以非法变量名（数字开头/含连字符）/空值/非法 teamId 创建
  Then 返回参数错误

@VR-S4 @auto:e2e
Scenario: 创建普通与私密变量（含分组）
  When Admin 创建普通变量与私密变量（不同分组）
  Then 返回变量 id

@VR-S5 @auto:e2e
Scenario: 团队内变量名唯一
  When 同团队创建同名变量
  Then 返回冲突
```

## Feature: 可见性（私密值保护）

```gherkin
@VR-S6 @auto:e2e
Scenario: 列表掩码
  When Member 查询列表
  Then 普通变量可见值，私密变量不回传值字段，且响应含 myRole

@VR-S7 @auto:e2e
Scenario: 详情
  When Member 查看私密变量详情
  Then 返回禁止
  When Admin 查看私密变量详情
  Then 返回解密后的原值
  When 非成员查看任意详情
  Then 返回不存在
```

## Feature: 更新

```gherkin
@VR-S9 @auto:e2e
Scenario: 更新
  When Member 更新
  Then 返回禁止
  When Admin 更新分组/描述/普通值
  Then 返回成功且生效
  When Admin 编辑私密变量但不填值
  Then 值保持不变
  When Admin 为私密变量填入新值
  Then 值更新为新值
```

## Feature: 替换

```gherkin
@VR-S10 @auto:e2e
Scenario: 运行时替换
  When Admin 对文本执行替换
  Then 普通与私密变量均被替换为对应值
  And 未定义的 ${占位符} 保留原文
  When Member 执行替换
  Then 返回禁止
```

## Feature: 分组与删除

```gherkin
@VR-S11 @auto:e2e
Scenario: 分组筛选
  When 按分组名筛选列表
  Then 仅返回该分组的变量

@VR-S12 @auto:e2e
Scenario: 删除
  When Member 删除
  Then 返回禁止
  When Admin 删除
  Then 返回成功且同名可重建
```
