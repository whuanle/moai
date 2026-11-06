# 团队智能体模块行为场景（BDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md)

## Feature: 访问与权限

```gherkin
@AG-S1 @auto:e2e
Scenario: 门禁
  When 未携带令牌查询列表
  Then 返回未认证
  When Member 创建/更新/删除/设置头像
  Then 返回禁止
  When 非成员执行创建/列表/详情/更新/删除
  Then 按目标不存在处理
```

## Feature: 创建与校验

```gherkin
@AG-S2 @auto:e2e
Scenario: 参数校验
  When 以空名称/非法或空 key（大写、空串）/空主提示词/非法子智能体标识（数字开头）/空子智能体名称/空子智能体描述/超过 20 个子智能体/组内重复标识创建
  Then 返回参数错误

@AG-S3 @auto:e2e
Scenario: 依赖校验
  When 声明团队无权使用的插件（主或子智能体）
  Then 返回参数错误
  When 声明不属于本团队的知识库
  Then 返回参数错误
  When 声明不属于本团队的知识图谱
  Then 返回参数错误
  When 携带未上传登记的头像 objectKey 创建
  Then 返回不存在

@AG-S4 @auto:e2e
Scenario: 创建成功与回读
  When Admin 创建智能体（含 key、2 个子智能体与本团队知识库依赖）
  Then 返回智能体 id
  When Member 查询列表
  Then 响应含 myRole、key/名称/子智能体数/审计字段正确
  When 查询详情
  Then key/主提示词/描述/启用态/子智能体定义（key+中文名）/依赖均正确回读
```

## Feature: 名称与标识唯一

```gherkin
@AG-S5 @auto:e2e
Scenario: 团队内 key/name 双唯一
  When 同团队创建同名智能体
  Then 返回冲突
  When 同团队创建同 key 智能体
  Then 返回冲突
  When 更新为另一已存在智能体的名称
  Then 返回冲突（key 创建后不可变更，不在更新字段内）
```

## Feature: 列表筛选

```gherkin
@AG-S6 @auto:e2e
Scenario: 关键字筛选
  When 按名称/描述关键字查询列表
  Then 仅返回命中的智能体，未命中返回空
```

## Feature: 更新（整文档替换）

```gherkin
@AG-S7 @auto:e2e
Scenario: 更新生效
  When Admin 更新名称/启停为停用/子智能体列表替换为仅 1 个
  Then 返回成功且详情回读生效
  When Member 查询列表
  Then 列表回显停用状态
  When Member 调用启停端点（PUT {id}/enabled）
  Then 返回禁止
  When Admin 调用启停端点恢复启用
  Then 返回成功且列表回显启用（卡片直切语义，不走整文档替换）
```

## Feature: 头像

```gherkin
@AG-S8 @auto:e2e
Scenario: 头像防护
  When Member 设置头像
  Then 返回禁止
  When 以伪造 objectKey 设置头像
  Then 返回不存在
```

## Feature: 删除与软删复用

```gherkin
@AG-S9 @auto:e2e
Scenario: 删除
  When Member 删除
  Then 返回禁止
  When 非成员删除
  Then 按目标不存在处理
  When Admin 删除
  Then 返回成功且详情 404、列表不再包含
  When 以同名重建
  Then 创建成功（软删释放唯一约束）
```

## Feature: 系统内置智能体（规划师/构建师）

```gherkin
@AG-S10 @auto:e2e
Scenario: 内置种入与保护
  When 创建新团队
  Then 团队自动种入内置「规划师 Plan」(key=plan)「构建师 Build」(key=build)（isSystem=true，含子智能体）
  When 新建第二个团队
  Then 同样自动种入
  When Member 查看列表
  Then 内置智能体同样可见
  When Admin 删除内置智能体
  Then 返回参数错误（不可删除）
  When Admin 编辑内置智能体（整文档替换）
  Then 返回成功且子智能体定义保留
  When Member 调用补种端点
  Then 返回禁止
  When 平台管理员调用补种端点
  Then 返回成功且幂等（已种团队内置数量保持 2；旧版本仅名字的种子行被领养补 key，不重复插入）
```
