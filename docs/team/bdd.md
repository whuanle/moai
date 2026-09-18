# 团队模块行为场景（BDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md)

术语：Owner=团队所有者（角色 0）；Admin=管理员（1）；Member=成员（2）。

## Feature: 创建团队

```gherkin
@TM-S1 @auto:e2e
Scenario: 未登录不能访问团队接口
  When 未携带令牌查询我的团队
  Then 返回未认证

@TM-S2 @auto:e2e
Scenario: 创建团队并自动成为所有者
  When 用户创建团队 "A"
  Then 返回团队 id
  And 我的团队列表包含 "A"，角色为 Owner，成员数为 1

@TM-S3 @auto:e2e
Scenario: 团队名未删除范围内唯一
  Given 已存在未删除团队 "A"
  When 任意用户创建同名团队 "A"
  Then 返回冲突错误

@TM-S4 @auto:e2e
Scenario: 创建参数校验
  When 以空名称创建团队
  Then 返回参数错误
  When 以超过 50 字符的名称创建团队
  Then 返回参数错误

@TM-S4b @auto:e2e
Scenario: 添加成员角色只允许 Admin 或 Member
  When 添加成员并指定角色为 Owner
  Then 返回参数错误
```

## Feature: 团队可见性

```gherkin
@TM-S5 @auto:e2e
Scenario: 所有者查看详情
  Given 我是团队 "A" 的 Owner
  When 查看团队详情
  Then 返回详情且我的角色为 Owner

@TM-S5b @auto:e2e
Scenario: 非成员不可见
  When 非成员用户查看团队详情或成员列表
  Then 返回不存在（不泄露团队存在性）

@TM-S9 @auto:e2e
Scenario: 成员查看成员列表
  Given 团队含 Owner/Admin/Admin/Member 四名成员
  When 成员查看成员列表
  Then 返回四人且角色正确
```

## Feature: 成员管理

```gherkin
@TM-S7 @auto:e2e
Scenario: 添加成员
  Given 我是 Owner
  When 添加用户为 Member
  Then 返回成功且成员列表包含该用户
  When 再次添加同一用户
  Then 返回冲突错误
  When 添加不存在的用户
  Then 返回不存在

@TM-S7b @auto:e2e
Scenario: 授予 Admin 角色仅限所有者
  Given 我是 Admin
  When 添加用户并授予 Admin 角色
  Then 返回禁止
  When 我是 Owner
  When 添加用户并授予 Admin 角色
  Then 返回成功

@TM-S8 @auto:e2e
Scenario: 调整角色仅限所有者
  Given 我是 Admin
  When 调整他人角色
  Then 返回禁止
  When 我是 Owner
  When 将成员提升为 Admin
  Then 返回成功
  When 调整自己的角色
  Then 返回参数错误
```

## Feature: 移除与退出

```gherkin
@TM-S10 @auto:e2e
Scenario: 移除保护矩阵
  When 对 Owner 执行移除（无论操作者是谁）
  Then 返回参数错误
  When Owner 尝试移除自己
  Then 返回参数错误
  When Admin 移除另一 Admin
  Then 返回禁止
  When Member 尝试移除他人
  Then 返回禁止

@TM-S10d @auto:e2e
Scenario: Owner 移除 Admin
  Given 我是 Owner
  When 移除一名 Admin
  Then 返回成功且成员列表不再包含该用户

@TM-S10f @auto:e2e
Scenario: 成员自行退出
  Given 我是 Member
  When 移除自己
  Then 返回成功且不再在成员列表
```

## Feature: 团队维护

```gherkin
@TM-S11 @auto:e2e
Scenario: 修改团队信息
  When 以空名称提交修改
  Then 返回参数错误
  When Owner 修改名称与简介
  Then 返回成功且详情回显新值

@TM-S12 @auto:e2e
Scenario: 团队不可解散
  When Owner 尝试解散团队
  Then 返回不支持且团队继续存在（解散能力已移除，仅平台管理员可在「团队」界面禁用团队）
```

## Feature: 所有权转让与团队头像（二期）

```gherkin
@TM-S13 @auto:e2e
Scenario: 转让所有权
  Given 我是 Owner，团队中有 Admin 和 Member
  When Admin 尝试转让所有权
  Then 返回禁止
  When 转让给非成员或自己
  Then 返回不存在或参数错误
  When Owner 转让所有权给 Admin
  Then 返回成功
  And 原所有者角色变为 Admin，新所有者角色为 Owner
  And 团队仍不可被解散

@TM-S14 @auto:e2e
Scenario: 设置团队头像
  Given 我是 Owner，已通过存储管线完成一次图片上传
  When 以伪造的 objectKey 设置头像
  Then 返回不存在
  When 以已完成的 objectKey 设置头像
  Then 返回成功
  And 团队详情回显可公开访问的头像地址
  When Member 尝试设置头像
  Then 返回禁止
```

## Feature: 管理员团队治理（后台）

```gherkin
@TM-S15 @auto:e2e
Scenario: 管理员查看全部团队
  Given 我是系统管理员
  When 查看全部团队列表
  Then 返回成功且包含非本人创建的团队
  And 列表项含负责人与成员数
  When 非管理员或未登录访问该列表
  Then 返回禁止或未授权

@TM-S15b @auto:e2e
Scenario: 禁用与启用团队
  Given 我是系统管理员
  When 禁用不存在的团队
  Then 返回不存在
  When 普通用户禁用某个团队
  Then 返回禁止
  When 禁用某个团队
  Then 返回成功，列表中该团队状态为已禁用，且按已禁用筛选仅返回该团队
  When 启用该团队
  Then 返回成功且状态恢复为正常

@TM-S16 @auto:e2e
Scenario: 管理员转让团队负责人
  Given 我是系统管理员，目标用户不是该团队成员
  When 非管理员执行转让，或目标用户/团队不存在
  Then 返回禁止或不存在
  When 转让给非团队成员
  Then 返回成功，该用户自动加入团队并成为负责人，原负责人降为管理员
  When 再次转让给当前负责人
  Then 返回参数错误

@TM-S16b @manual
Scenario: 团队列表对多负责人脏数据容错
  Given 某团队因历史脏数据存在多条 Owner 记录
  When 查看管理员团队列表或我的团队列表
  Then 返回成功，该团队负责人按 UserId 最小的一条展示，列表不报错
```

## Feature: 团队详情页导航

```gherkin
@TM-S17 @auto:vitest
Scenario: 进入团队默认落在内部应用分区
  Given 我已进入某团队详情页
  Then 面包屑只展示「主页」与「团队头像 + 团队名称」，不再出现「团队」层级
  And 左侧菜单不包含「团队信息」入口
  When 直接打开 /team/{id}，或访问不可见/非法的分区子路由
  Then 页面回落到「内部应用」分区并加载团队应用列表，不渲染被越权访问的分区
```
