# 角色组织（Org，ORG）行为规格（BDD，Gherkin）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md)（场景→验证映射） ｜ [SOP](./sop.md)（管理页操作与排障）
> 编号规则与标签语义见 [../DOC-STANDARD.md](../DOC-STANDARD.md) 第 3 节。

```gherkin
Feature: 管理员门禁
  @ORG-S1 @auto:e2e
  Scenario: 非管理员无权管理组织角色
    Given 普通用户已登录
    When 请求角色列表、创建角色或分配用户角色
    Then 返回没有权限（403）

  @ORG-S2 @manual
  Scenario: 未登录访问管理面
    When 不带 token 请求角色列表
    Then 返回未认证（401）

Feature: 角色树创建与物化路径
  Background:
    Given 管理员已登录

  @ORG-S3 @auto:e2e
  Scenario: 空名称被拒
    When 创建名称为空的角色
    Then 返回参数错误（400）

  @ORG-S4 @auto:e2e
  Scenario: 上级角色不存在
    When 以上级 id 999999 创建角色
    Then 返回不存在（404）

  @ORG-S5 @auto:e2e
  Scenario: 创建根角色
    When 创建不指定上级的角色
    Then 创建成功且物化路径为 /<id>/

  @ORG-S6 @auto:e2e
  Scenario: 子孙角色路径逐级追加
    When 依次在根角色下创建子角色、在子角色下创建孙角色
    Then 子角色路径为 /<根id>/<子id>/，孙角色路径为 /<根id>/<子id>/<孙id>/

  @ORG-S7 @auto:e2e
  Scenario: 多个根角色并存
    When 再创建另一个根角色
    Then 两个根角色路径各自独立且互不为前缀

  @ORG-S8 @auto:e2e
  Scenario: 同一上级下重名被拒
    Given 根角色下已存在名为"销售部"的角色
    When 在同一上级下再创建"销售部"
    Then 返回冲突（409）

  @ORG-S9 @auto:e2e
  Scenario: 不同上级下允许同名
    When 在另一根角色下创建同名"销售部"
    Then 创建成功

Feature: 角色查询
  @ORG-S10 @auto:e2e
  Scenario: 列表按物化路径数值层级序返回
    Given 已存在多级角色树与另一根角色
    When 查询角色列表
    Then 父角色先于其全部后代、同级按 id 数值序（id 10 不排在 id 7 前）

  @ORG-S11 @auto:e2e
  Scenario: 列表回传层级字段
    When 查询角色列表
    Then 每项含上级 id、物化路径、是否有子角色与直属成员数

  @ORG-S12 @auto:e2e
  Scenario: 按名称搜索
    When 以角色名关键字查询列表
    Then 仅命中名称匹配的角色

Feature: 角色更新与子树迁移
  @ORG-S13 @auto:e2e
  Scenario: 改名不影响路径
    When 修改角色名称且上级不变
    Then 名称更新且物化路径与后代路径不变

  @ORG-S14 @auto:e2e
  Scenario: 调整上级整体重写子树路径
    Given 角色B（带后代C）在根A下
    When 把 B 调整到根 D 下
    Then B 路径变为 /<Did>/<Bid>/，C 路径变为 /<Did>/<Bid>/<Cid>/，父子关系同步更新

  @ORG-S15 @auto:e2e
  Scenario: 调整为根角色
    When 把子角色的上级清空
    Then 其路径变为顶级 /<id>/，后代路径同步前移

  @ORG-S16 @auto:e2e
  Scenario: 移动到自身后代之下被拒（防环）
    Given 角色 B 带后代 C
    When 把 B 的上级改为 C
    Then 返回参数错误（400）

  @ORG-S17 @auto:e2e
  Scenario: 自身设为上级被拒
    When 把角色的上级改为自身
    Then 返回参数错误（400）

  @ORG-S18 @auto:e2e
  Scenario: 更新不存在的角色
    When 修改 id 不存在的角色
    Then 返回不存在（404）

Feature: 用户角色分配
  @ORG-S19 @auto:e2e
  Scenario: 给不存在的用户分配被拒
    When 以用户 id 999999 保存角色集合
    Then 返回不存在（404）

  @ORG-S20 @auto:e2e
  Scenario: 含不存在角色的分配被拒
    When 保存包含不存在角色 id 的集合
    Then 返回参数错误（400）

  @ORG-S21 @auto:e2e
  Scenario: 分配多个角色并按路径序回显
    When 给用户分配两个有层级关系的角色
    Then 保存成功，回显按物化路径排序且带用户资料

  @ORG-S22 @auto:e2e
  Scenario: 分配列表自动去重
    When 保存含重复角色 id 的集合
    Then 仅落库去重后的绑定

  @ORG-S23 @auto:e2e
  Scenario: 角色成员清单与搜索
    Given 两个用户绑定了同一角色
    When 查询该角色成员并按用户名搜索
    Then 清单含两个用户，搜索仅命中匹配者

  @ORG-S24 @auto:e2e
  Scenario: 整体覆盖用户角色集合
    Given 用户已持有两个角色
    When 保存仅含其中一个角色的新集合
    Then 用户只保留该角色（空集合即清空）

Feature: 删除
  @ORG-S25 @auto:e2e
  Scenario: 存在子角色时删除被拒
    Given 角色仍有子角色
    When 删除该角色
    Then 返回参数错误（400）

  @ORG-S26 @auto:e2e
  Scenario: 删除叶子角色并级联解绑
    Given 叶子角色下绑定了一个用户
    When 删除该角色
    Then 删除成功且该用户的绑定一并解除

  @ORG-S27 @auto:e2e
  Scenario: 删除后列表消失且不可重复删除
    When 删除成功后再次查询列表并重复删除
    Then 列表不再出现该角色，重复删除返回 404

Feature: 组织角色管理页（左侧角色树 + 右侧成员列表）
  @ORG-S28 @auto:vitest
  Scenario: 非管理员进入页面被重定向
    Given 普通用户已登录
    When 访问组织角色页
    Then 页面重定向离开且不渲染管理界面

  @ORG-S29 @auto:vitest
  Scenario: 左侧组织树固定默认根并按层级展示
    Given 已存在多级组织树且某组织为默认角色
    When 打开角色管理 Tab
    Then 树顶固定展示「默认根」（不可选中，空树时也在），组织挂其下展示名称与直属成员数，默认角色带徽标

  @ORG-S30 @auto:vitest
  Scenario: 新建顶级组织与下级组织（仅输入名称）
    When 点击默认根或某组织节点右侧的新建按钮并填写名称保存
    Then 分别以无上级与该组织为上级提交创建，表单仅含名称一项

  @ORG-S31 @auto:vitest
  Scenario: 编辑组织仅改名
    When 编辑选中组织并保存
    Then 只提交名称与原上级（层级调整走拖拽）

  @ORG-S32 @auto:vitest
  Scenario: 删除角色需二次确认
    When 在选中角色上点击删除并确认
    Then 才会提交删除请求

  @ORG-S33 @auto:vitest
  Scenario: 选中组织右栏按用户管理样式展示成员并可移除
    When 在左侧树选中带成员的组织并确认移除某用户
    Then 右栏按用户管理页样式展示成员资料（头像+昵称主行+@用户名/邮箱/手机号/角色标签/状态标签/加入时间）
    And 移除按整体覆盖该用户组织集合提交

  @ORG-S34 @manual
  Scenario: 组织角色页提供团队与角色双 Tab
    Given 管理员已登录
    When 打开菜单「组织角色」（原团队管理入口）
    Then 默认展示团队管理 Tab，可切换到角色管理 Tab（左侧树 + 右侧成员列表）

Feature: 默认角色（注册自动赋予）
  Background:
    Given 管理员已登录

  @ORG-S35 @auto:e2e
  Scenario: 非管理员设置默认角色被拒
    Given 普通用户已登录
    When 请求设置默认角色
    Then 返回没有权限（403）

  @ORG-S36 @auto:e2e
  Scenario: 设置不存在的默认角色
    When 以不存在的角色 id 设置默认角色
    Then 返回不存在（404）

  @ORG-S37 @auto:e2e
  Scenario: 设置默认角色并在列表标识
    When 管理员把某角色设为默认角色
    Then 保存成功且角色列表中仅该角色带默认标记

  @ORG-S38 @auto:e2e
  Scenario: 注册新用户自动获得默认角色
    Given 已设置默认角色
    When 新用户完成注册（含 OAuth 自动建号）
    Then 该用户自动持有默认角色

  @ORG-S39 @auto:e2e
  Scenario: 清空默认后新注册用户无角色
    Given 已设置默认角色
    When 管理员清空默认角色后新用户完成注册
    Then 该用户不持有任何组织角色

  @ORG-S40 @auto:e2e
  Scenario: 删除默认角色自动清空设置
    Given 默认角色已无子角色
    When 管理员删除该默认角色
    Then 默认角色设置自动清空，其后新注册用户无角色

  @ORG-S41 @auto:vitest
  Scenario: 设为默认与取消默认
    When 对选中非默认角色一键设为默认，或对默认角色二次确认取消默认
    Then 分别提交设置该角色为默认与清空默认

  @ORG-S42 @manual
  Scenario: 拖拽组织重新挂层级
    Given 已存在多级组织树
    When 将某组织拖入另一组织节点内、拖到节点之间或拖到默认根上
    Then 分别成为该组织下级、同级换挂（挂到目标组织的上级）、升为顶级组织
    And 拖到自身或其下级组织时被拦截提示
```
