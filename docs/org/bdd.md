# 组织（Org，ORG）行为规格（BDD，Gherkin）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md)（场景→验证映射） ｜ [SOP](./sop.md)（管理页操作与排障）
> 编号规则与标签语义见 [../DOC-STANDARD.md](../DOC-STANDARD.md) 第 3 节。

```gherkin
Feature: 管理员门禁
  @ORG-S1 @auto:e2e
  Scenario: 非管理员无权管理组织
    Given 普通用户已登录
    When 请求组织列表、创建组织或分配用户组织
    Then 返回没有权限（403）

  @ORG-S2 @manual
  Scenario: 未登录访问管理面
    When 不带 token 请求组织列表
    Then 返回未认证（401）

Feature: 组织树创建与物化路径
  Background:
    Given 管理员已登录

  @ORG-S3 @auto:e2e
  Scenario: 空名称被拒
    When 创建名称为空的组织
    Then 返回参数错误（400）

  @ORG-S4 @auto:e2e
  Scenario: 上级组织不存在
    When 以上级 id 999999 创建组织
    Then 返回不存在（404）

  @ORG-S5 @auto:e2e
  Scenario: 创建根组织
    When 创建不指定上级的组织
    Then 创建成功且物化路径为 /<id>/

  @ORG-S6 @auto:e2e
  Scenario: 子孙组织路径逐级追加
    When 依次在根组织下创建子组织、在子组织下创建孙组织
    Then 子组织路径为 /<根id>/<子id>/，孙组织路径为 /<根id>/<子id>/<孙id>/

  @ORG-S7 @auto:e2e
  Scenario: 多个根组织并存
    When 再创建另一个根组织
    Then 两个根组织路径各自独立且互不为前缀

  @ORG-S8 @auto:e2e
  Scenario: 同一上级下重名被拒
    Given 根组织下已存在名为"销售部"的组织
    When 在同一上级下再创建"销售部"
    Then 返回冲突（409）

  @ORG-S9 @auto:e2e
  Scenario: 不同上级下允许同名
    When 在另一根组织下创建同名"销售部"
    Then 创建成功

Feature: 组织查询
  @ORG-S10 @auto:e2e
  Scenario: 列表按物化路径数值层级序返回
    Given 已存在多级组织树与另一根组织
    When 查询组织列表
    Then 父组织先于其全部后代、同级按 id 数值序（id 10 不排在 id 7 前）

  @ORG-S11 @auto:e2e
  Scenario: 列表回传层级字段
    When 查询组织列表
    Then 每项含上级 id、物化路径、是否有子组织与直属成员数

  @ORG-S12 @auto:e2e
  Scenario: 按名称搜索
    When 以组织名关键字查询列表
    Then 仅命中名称匹配的组织

Feature: 组织更新与子树迁移
  @ORG-S13 @auto:e2e
  Scenario: 改名不影响路径
    When 修改组织名称且上级不变
    Then 名称更新且物化路径与后代路径不变

  @ORG-S14 @auto:e2e
  Scenario: 调整上级整体重写子树路径
    Given 组织B（带后代C）在根A下
    When 把 B 调整到根 D 下
    Then B 路径变为 /<Did>/<Bid>/，C 路径变为 /<Did>/<Bid>/<Cid>/，父子关系同步更新

  @ORG-S15 @auto:e2e
  Scenario: 调整为根组织
    When 把子组织的上级清空
    Then 其路径变为顶级 /<id>/，后代路径同步前移

  @ORG-S16 @auto:e2e
  Scenario: 移动到自身后代之下被拒（防环）
    Given 组织 B 带后代 C
    When 把 B 的上级改为 C
    Then 返回参数错误（400）

  @ORG-S17 @auto:e2e
  Scenario: 自身设为上级被拒
    When 把组织的上级改为自身
    Then 返回参数错误（400）

  @ORG-S18 @auto:e2e
  Scenario: 更新不存在的组织
    When 修改 id 不存在的组织
    Then 返回不存在（404）

Feature: 用户组织分配
  @ORG-S19 @auto:e2e
  Scenario: 给不存在的用户分配被拒
    When 以用户 id 999999 保存组织集合
    Then 返回不存在（404）

  @ORG-S20 @auto:e2e
  Scenario: 含不存在组织的分配被拒
    When 保存包含不存在组织 id 的集合
    Then 返回参数错误（400）

  @ORG-S21 @auto:e2e
  Scenario: 分配多个组织并按路径序回显
    When 给用户分配两个有层级关系的组织
    Then 保存成功，回显按物化路径排序且带用户资料

  @ORG-S22 @auto:e2e
  Scenario: 分配列表自动去重
    When 保存含重复组织 id 的集合
    Then 仅落库去重后的绑定

  @ORG-S23 @auto:e2e
  Scenario: 组织成员清单与搜索
    Given 两个用户绑定了同一组织
    When 查询该组织成员并按用户名搜索
    Then 清单含两个用户，搜索仅命中匹配者

  @ORG-S24 @auto:e2e
  Scenario: 整体覆盖用户组织集合
    Given 用户已持有两个组织
    When 保存仅含其中一个组织的新集合
    Then 用户只保留该组织（空集合即清空）

Feature: 删除
  @ORG-S25 @auto:e2e
  Scenario: 存在子组织时删除被拒
    Given 组织仍有子组织
    When 删除该组织
    Then 返回参数错误（400）

  @ORG-S26 @auto:e2e
  Scenario: 删除叶子组织并级联解绑
    Given 叶子组织下绑定了一个用户
    When 删除该组织
    Then 删除成功且该用户的绑定一并解除

  @ORG-S27 @auto:e2e
  Scenario: 删除后列表消失且不可重复删除
    When 删除成功后再次查询列表并重复删除
    Then 列表不再出现该组织，重复删除返回 404

Feature: 组织管理页（左侧组织树 + 右侧成员列表）
  @ORG-S28 @auto:vitest
  Scenario: 非管理员进入页面被重定向
    Given 普通用户已登录
    When 访问组织页
    Then 页面重定向离开且不渲染管理界面

  @ORG-S29 @auto:vitest
  Scenario: 左侧组织树固定默认根并按层级展示
    Given 已存在多级组织树且某组织为默认组织
    When 打开组织管理 Tab
    Then 树顶固定展示「默认根」（不可选中，空树时也在），组织挂其下展示名称与直属成员数，默认组织带徽标

  @ORG-S30 @auto:vitest
  Scenario: 新建顶级组织与下级组织（仅输入名称）
    When 点击默认根或某组织节点右侧的新建按钮并填写名称保存
    Then 分别以无上级与该组织为上级提交创建，表单仅含名称一项

  @ORG-S31 @auto:vitest
  Scenario: 编辑组织仅改名
    When 编辑选中组织并保存
    Then 只提交名称与原上级（层级调整走拖拽）

  @ORG-S32 @auto:vitest
  Scenario: 删除组织需二次确认
    When 在选中组织上点击删除并确认
    Then 才会提交删除请求

  @ORG-S33 @auto:vitest
  Scenario: 选中组织右栏按用户管理样式展示成员并可移除
    When 在左侧树选中带成员的组织并确认移除某用户
    Then 右栏按用户管理页样式展示成员资料（头像+昵称主行+@用户名/邮箱/手机号/组织标签/状态标签/加入时间）
    And 移除按整体覆盖该用户组织集合提交

  @ORG-S34 @manual
  Scenario: 组织页提供团队与组织双 Tab
    Given 管理员已登录
    When 打开菜单「组织」（原团队管理入口）
    Then 默认展示团队管理 Tab，可切换到组织管理 Tab（左侧树 + 右侧成员列表）

Feature: 默认组织（注册自动赋予）
  Background:
    Given 管理员已登录

  @ORG-S35 @auto:e2e
  Scenario: 非管理员设置默认组织被拒
    Given 普通用户已登录
    When 请求设置默认组织
    Then 返回没有权限（403）

  @ORG-S36 @auto:e2e
  Scenario: 设置不存在的默认组织
    When 以不存在的组织 id 设置默认组织
    Then 返回不存在（404）

  @ORG-S37 @auto:e2e
  Scenario: 设置默认组织并在列表标识
    When 管理员把某组织设为默认组织
    Then 保存成功且组织列表中仅该组织带默认标记

  @ORG-S38 @auto:e2e
  Scenario: 注册新用户自动获得默认组织
    Given 已设置默认组织
    When 新用户完成注册（含 OAuth 自动建号）
    Then 该用户自动持有默认组织

  @ORG-S39 @auto:e2e
  Scenario: 清空默认后新注册用户无组织
    Given 已设置默认组织
    When 管理员清空默认组织后新用户完成注册
    Then 该用户不持有任何组织

  @ORG-S40 @auto:e2e
  Scenario: 删除默认组织自动清空设置
    Given 默认组织已无子组织
    When 管理员删除该默认组织
    Then 默认组织设置自动清空，其后新注册用户无组织

  @ORG-S41 @auto:vitest
  Scenario: 设为默认与取消默认
    When 对选中非默认组织一键设为默认，或对默认组织二次确认取消默认
    Then 分别提交设置该组织为默认与清空默认

  @ORG-S42 @manual
  Scenario: 拖拽组织重新挂层级
    Given 已存在多级组织树
    When 将某组织拖入另一组织节点内、拖到节点之间或拖到默认根上
    Then 分别成为该组织下级、同级换挂（挂到目标组织的上级）、升为顶级组织
    And 拖到自身或其下级组织时被拦截提示

Feature: 统一组织授权（资源按组织线定向放通）
  Background:
    Given 管理员已登录
    And 已存在多级组织树且部分用户绑定组织

  @ORG-S43 @auto:e2e
  Scenario: 非管理员无权配置组织授权
    Given 普通用户已登录
    When 配置、查询或判定某资源的组织授权
    Then 返回没有权限（403）

  @ORG-S44 @auto:e2e
  Scenario: 授权参数校验
    When 以不存在的资源类型或不存在组织配置授权
    Then 返回参数错误（400）

  @ORG-S45 @auto:e2e
  Scenario: 整体覆盖配置授权并回显
    When 为某资源配置组织授权（一个或多个组织各带方向规则）
    Then 保存成功并回显生效数与每条记录的组织名称、物化路径、方向
    And 再次配置时旧记录整体被新集合替换，空集合即清空

  @ORG-S46 @auto:e2e
  Scenario: 仅该组织方向（self）判定
    Given 某资源授权给组织 R 方向为仅该组织
    When 分别以绑定 R 的用户、绑定其他组织的用户、无组织用户判定
    Then 仅绑定 R 的用户放通，其余不放行

  @ORG-S47 @auto:e2e
  Scenario: 向上方向（up）判定
    Given 某资源授权给下级组织 R 方向为向上
    When 以绑定 R 自身、R 的祖先组织、无组织用户分别判定
    Then R 自身与全部祖先组织绑定的用户放通（如报表应用「经理及以上」可见），无组织用户不放行

  @ORG-S48 @auto:e2e
  Scenario: 向下方向（down）判定
    Given 某资源授权给上级组织 R 方向为向下
    When 以绑定 R 自身、R 的子孙组织、无组织用户分别判定
    Then R 及其整棵子树组织绑定的用户均放通（如某部门全员可见），无组织用户不放行

  @ORG-S49 @auto:e2e
  Scenario: 有效组织与有效用户展开
    Given 某资源存在授权记录
    When 查询该资源的有效组织与有效用户
    Then 返回按方向规则展开后的组织集合（层级序）与展开组织绑定的用户 id 并集

  @ORG-S50 @auto:e2e
  Scenario: 多记录并集与无授权不放行
    Given 某资源配置了多条授权记录、另一资源未配置任何授权
    When 分别判定绑定各组织的用户
    Then 多条记录按并集放通；无授权记录的资源对任何用户均不放行（组织维度未配置不限制，由消费方与团队逻辑组合）

  @ORG-S51 @auto:e2e
  Scenario: 组织树迁移后判定随新路径
    Given 某资源授权给组织 R 方向为向上且 R 的旧祖先用户已放通
    When 把 R 整体迁移到另一分支下
    Then 旧祖先组织绑定的用户不再放通，新祖先组织绑定的用户放通，有效组织展开随新树

  @ORG-S52 @auto:e2e
  Scenario: 删除组织级联清理授权
    Given 某资源授权给组织 R 且 R 上存在用户绑定
    When 管理员删除叶子组织 R
    Then 引用 R 的授权记录一并清理，该资源授权回显为空且判定不再放通
```
