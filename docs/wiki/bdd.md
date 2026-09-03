# 知识库模块行为场景（BDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md)

## Feature: 访问控制

```gherkin
@WK-S1 @auto:e2e
Scenario: 未登录不能访问
  When 未携带令牌查询知识库列表
  Then 返回未认证
```

## Feature: 创建知识库

```gherkin
@WK-S2 @auto:e2e
Scenario: 参数校验
  When 以空名称/超长名称/非法 teamId 创建
  Then 返回参数错误

@WK-S3 @auto:e2e
Scenario: 角色权限
  When Member 创建知识库
  Then 返回禁止
  When 非成员创建知识库
  Then 返回不存在

@WK-S4 @auto:e2e
Scenario: Admin 创建并列出
  When Admin 创建知识库
  Then 返回知识库 id
  And Member 查询列表可见该知识库且响应含 myRole
  And 非成员查询列表返回不存在
```

## Feature: 名称唯一

```gherkin
@WK-S5 @auto:e2e
Scenario: 团队作用域唯一
  When 同团队创建同名知识库
  Then 返回冲突
  When 不同团队创建同名知识库
  Then 返回成功
```

## Feature: 详情与更新

```gherkin
@WK-S6 @auto:e2e
Scenario: 详情可见性
  When 成员查询详情
  Then 返回名称与 myRole
  When 非成员或已删除 id 查询
  Then 返回不存在

@WK-S7 @auto:e2e
Scenario: 更新
  When Member 更新
  Then 返回禁止
  When Admin 更新名称与简介
  Then 返回成功且详情回显
  When 更新为同团队已有名称
  Then 返回冲突
```

## Feature: 删除

```gherkin
@WK-S8 @auto:e2e
Scenario: 软删除
  When Member 删除
  Then 返回禁止
  When Admin 删除
  Then 返回成功
  And 详情返回不存在、列表不含、同团队同名可重建
```

## Feature: 知识库文档（二期）

```gherkin
@WD-S1 @auto:e2e
Scenario: 访问控制
  When 未携带令牌查询文档列表
  Then 返回未认证

@WD-S2 @auto:e2e
Scenario: 标题校验
  When 以空标题/超过 100 字标题创建文档
  Then 返回参数错误

@WD-S3 @auto:e2e
Scenario: 非成员不可见
  When 非成员创建文档或查询列表
  Then 返回不存在

@WD-S4 @auto:e2e
Scenario: Member 可创建（内容协作）
  When Member 创建带 Markdown 正文的文档
  Then 返回文档 id
  And 列表可见且响应含 myRole、列表项不含正文字段
  And 查询详情返回 Markdown 正文

@WD-S6 @auto:e2e
Scenario: Member 可编辑
  When Member 修改标题与正文
  Then 返回成功且详情回显

@WD-S7 @auto:e2e
Scenario: 删除需管理员
  When Member 删除文档
  Then 返回禁止
  When Admin 删除文档
  Then 返回成功且详情返回不存在

@WD-S8 @auto:e2e
Scenario: 知识库删除后文档不可访问
  Given 知识库已被删除
  When 查询其文档详情
  Then 返回不存在
```
