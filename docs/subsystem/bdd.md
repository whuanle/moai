# 子系统接入（Subsystem，SUB）行为规格（BDD，Gherkin）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md)（场景→验证映射） ｜ [SOP](./sop.md)（对接指引与排障）
> 编号规则与标签语义见 [../DOC-STANDARD.md](../DOC-STANDARD.md) 第 3 节。

```gherkin
Feature: 管理面门禁
  @SUB-S1 @auto:e2e
  Scenario: 非 root 无权管理子系统
    Given 普通用户已登录
    When 请求子系统的列表或创建
    Then 返回没有权限（403）

  @SUB-S2 @manual
  Scenario: 未登录访问管理面
    When 不带 token 请求子系统列表
    Then 返回未认证（401）

Feature: 子系统注册与一次性密钥
  Background:
    Given root 已登录

  @SUB-S3 @auto:e2e
  Scenario: 创建子系统并生成密钥（掩码回显）
    When root 以合法 appid 创建子系统（全员可用）
    Then 创建成功并返回子系统 id 与密钥展示掩码（不回原文）
    And 库内以 AES 可逆密文存储，原文可随时经信封解密查看

  @SUB-S4 @auto:e2e
  Scenario: 非法 appid 被拒
    When root 以非法格式 appid（数字开头等）创建子系统
    Then 返回参数错误（400）

  @SUB-S5 @auto:e2e
  Scenario: 重复 appid 被拒
    Given 已存在 appid 为 report 的子系统
    When root 再以 report 创建子系统
    Then 返回冲突（409）

Feature: 接入 token 换取（机器面）
  @SUB-S6 @auto:e2e
  Scenario: 错误密钥换取 token 被拒
    Given 子系统 report 存在
    When 以错误密钥请求换取 token
    Then 返回未认证（401）

  @SUB-S7 @auto:e2e
  Scenario: 不存在的 appid 换取 token 被拒
    When 以不存在的 appid 请求换取 token
    Then 返回未认证（401）

  @SUB-S8 @auto:e2e
  Scenario: 正确密钥换取 token
    When 以 appid 与正确密钥请求换取 token
    Then 返回接入 token（Bearer）与有效期

Feature: 心跳与在线状态
  @SUB-S9 @auto:e2e
  Scenario: 心跳需要子系统 token
    When 无 token 或伪造 token 请求心跳
    Then 返回未认证（401）

  @SUB-S10 @auto:e2e
  Scenario: 心跳保活在线状态
    Given 接入已换取接入 token
    When 接入携带 token 与实例标识心跳
    Then 心跳成功且 root 列表展示该接入在线（在线实例数 1）

  @SUB-S61 @auto:e2e
  Scenario: 心跳必须携带实例标识
    Given 接入已换取接入 token
    When 心跳请求缺少实例标识
    Then 返回参数错误（400）

  @SUB-S62 @auto:e2e
  Scenario: 多实例分别心跳回显在线实例数
    Given 同一接入的两个实例以各自实例标识先后心跳
    When root 查询列表与接入自省
    Then 均展示在线且在线实例数为 2（实例超窗未续期后自动掉出集合）

  @SUB-S11 @auto:e2e
  Scenario: 未心跳时为离线
    Given 接入从未心跳
    When root 查询接入列表
    Then 该接入展示为离线（在线实例数 0）

  @SUB-S12 @auto:e2e
  Scenario: 接入自省
    Given 接入已换取接入 token
    When 请求接入自省接口
    Then 返回自身 appid/名称/类型/状态/团队授权范围/在线状态与在线实例数

  @SUB-S13 @auto:e2e
  Scenario: 无 token 自省被拒
    When 无 token 请求子系统自省接口
    Then 返回未认证（401）

Feature: 停用即时生效
  Background:
    Given 子系统已存在且持有有效接入 token

  @SUB-S14 @auto:e2e
  Scenario: root 停用子系统
    When root 将子系统状态改为停用
    Then 更新成功且在线状态被清除

  @SUB-S15 @auto:e2e
  Scenario: 停用后拒绝换取 token
    When 以正确密钥请求换取 token
    Then 返回禁止（403）

  @SUB-S16 @auto:e2e
  Scenario: 停用后已有 token 立即失效
    When 子系统以停用前换取的 token 心跳
    Then 返回禁止（403）

Feature: 密钥重置
  Background:
    Given 子系统已存在

  @SUB-S17 @auto:e2e
  Scenario: 重置密钥返回新密钥
    When root 重置子系统接入密钥
    Then 返回新的 48 位密钥原文（仅此一次）

  @SUB-S18 @auto:e2e
  Scenario: 旧密钥立即失效
    When 以旧密钥换取 token
    Then 返回未认证（401）
    When 以新密钥换取 token
    Then 换取成功

Feature: 团队授权
  Background:
    Given root 已登录且存在团队 A 与团队 B

  @SUB-S19 @auto:e2e
  Scenario: 全员可用对所有团队可见
    Given 子系统为全员可用且启用
    When 团队 A 成员查询本团队可用子系统
    Then 结果包含该子系统

  @SUB-S20 @auto:e2e
  Scenario: 指定团队仅授权团队可见
    Given 子系统切换为指定团队并仅授权团队 A
    When 团队 A 与团队 B 成员各自查询可用子系统
    Then 团队 A 可见该子系统
    And 团队 B 不可见

  @SUB-S21 @auto:e2e
  Scenario: 授权团队清单回显
    Given 子系统为指定团队且已授权团队 A
    When root 查询该子系统的授权团队
    Then 返回团队 A 的 id

  @SUB-S22 @auto:e2e
  Scenario: 授权不存在的团队被拒
    When root 以不存在的团队 id 更新授权
    Then 返回参数错误（400）

  @SUB-S23 @auto:e2e
  Scenario: 非团队成员查询可用列表被拒
    When 非团队成员查询某团队的可用子系统
    Then 返回不存在（404）

Feature: 删除与 appid 复用
  Background:
    Given 子系统 report 已存在

  @SUB-S24 @auto:e2e
  Scenario: root 删除子系统
    When root 删除该子系统
    Then 删除成功（软删除）

  @SUB-S25 @auto:e2e
  Scenario: 删除后密钥失效
    When 以删除前的密钥换取 token
    Then 返回未认证（401）

  @SUB-S26 @auto:e2e
  Scenario: 删除后 appid 释放可复用
    When root 再以 report 创建子系统
    Then 创建成功

Feature: 管理页（前端）
  @SUB-S27 @auto:vitest
  Scenario: 非 root 访问管理页被重定向
    Given 普通用户登录
    When 访问子系统管理页
    Then 重定向到应用广场且不渲染表格

  @SUB-S28 @auto:vitest
  Scenario: 列表展示子系统概况
    Given root 登录且存在子系统若干
    When 打开子系统管理页
    Then 展示 appid/名称/团队授权范围/状态/在线状态/创建时间
    And 指定团队范围时完整展示全部授权团队名称（团队已删除回退 #id，名称缺失回退计数）

  @SUB-S29 @auto:vitest
  Scenario: 创建后弹出一次性密钥
    When root 填写表单创建子系统
    Then 弹窗展示一次性密钥与保存警告
    And 密钥可复制

  @SUB-S30 @auto:vitest
  Scenario: 编辑回显授权团队并整体覆盖
    When root 编辑指定团队范围的子系统并保存
    Then 已授权团队被回显且保存按整体覆盖提交

  @SUB-S31 @auto:vitest
  Scenario: 危险操作需二次确认
    When root 点击停用/重置密钥/删除
    Then 均需 Popconfirm 确认后执行

Feature: 委托 SSO 登录
  子系统无自建用户表，用户经 MoAI 登录后跳转子系统；回跳地址仅取子系统注册的回调地址（防开放重定向）。

  @SUB-S32 @auto:e2e
  Scenario: 未登录用户请求委托码被拒
    When 未登录用户请求签发委托码
    Then 返回未认证（401）

  @SUB-S33 @auto:e2e
  Scenario: 未配置回调地址的子系统不开放委托登录
    Given 子系统未配置登录回调地址
    When 登录用户请求签发委托码
    Then 返回禁止（403）

  @SUB-S34 @auto:e2e
  Scenario: root 配置登录回调地址列表
    When root 更新子系统并设置回调地址列表（可多个入口）
    Then 更新成功

  @SUB-S64 @auto:e2e
  Scenario: 多回调地址按 redirect_uri 指定入口
    Given 子系统注册了生产与测试两个回调地址
    When 用户签发委托码时携带 redirect_uri 指向测试入口
    Then 返回的回跳地址为该测试入口

  @SUB-S65 @auto:e2e
  Scenario: 未注册的 redirect_uri 被拒
    When 签发委托码携带不在注册列表内的 redirect_uri
    Then 返回禁止（403）

  @SUB-S66 @auto:e2e
  Scenario: 缺省回跳注册列表第一个
    When 签发委托码未携带 redirect_uri
    Then 返回的回跳地址为注册列表第一个

  @SUB-S35 @auto:e2e
  Scenario: 未获授权的用户不能签发委托码
    Given 子系统为全员可用且用户未加入任何团队
    When 用户请求签发委托码
    Then 返回禁止（403）
    Given 子系统切换为指定团队且用户团队不在授权清单
    When 用户请求签发委托码
    Then 返回禁止（403）

  @SUB-S36 @auto:e2e
  Scenario: 授权用户签发一次性委托码
    Given 子系统开放登录且用户团队已获授权
    When 用户请求签发委托码（可携带透传参数）
    Then 返回委托码与注册的回调地址和子系统名称

  @SUB-S37 @auto:e2e
  Scenario: 委托码兑换需子系统机器凭证
    When 不带子系统 token 请求兑换
    Then 返回未认证（401）

  @SUB-S38 @auto:e2e
  Scenario: 子系统兑换委托码获得用户委托 token
    Given 子系统持有机器 token 且委托码有效
    When 子系统请求兑换
    Then 返回用户 access/refresh token 与用户基本资料（id/用户名/昵称）

  @SUB-S39 @auto:e2e
  Scenario: 委托码一次性不可重放
    When 以已兑换过的委托码再次兑换
    Then 返回未认证（401）

  @SUB-S40 @auto:e2e
  Scenario: 委托码绑定子系统不可跨子系统兑换
    Given 委托码为子系统 A 签发
    When 子系统 B 以自己的机器 token 兑换
    Then 返回未认证（401）

  @SUB-S41 @auto:e2e
  Scenario: 用户委托 token 刷新
    When 子系统以有效 refresh token 请求刷新
    Then 返回新的 access/refresh token 与用户资料
    When 以无效 refresh token 请求刷新
    Then 返回未认证（401）

  @SUB-S42 @auto:e2e
  Scenario: 停用后委托登录一并失效
    Given 子系统已停用且存在未过期 refresh token
    When 用户请求签发委托码
    Then 返回禁止（403）
    When 子系统请求刷新用户委托 token
    Then 返回禁止（403）

  @SUB-S43 @auto:vitest
  Scenario: MoAI 委托 SSO 跳转页
    Given 子系统以 /sso/authorize?client=<appid> 引导用户到 MoAI
    When 用户已登录
    Then 自动签发委托码并回跳注册回调（携带 code 与可选 state）
    When 用户未登录
    Then 先跳转 MoAI 登录页，登录成功后回到本页继续

  @SUB-S44 @auto:vitest
  Scenario: 跳转页错误处理
    When 缺少 client 参数或签发失败（未开放/未授权）
    Then 展示错误原因与返回应用广场入口，不做任何跳转

  @SUB-S45 @auto:e2e
  Scenario: 机器 token 刷新轮换
    When 子系统以 appid+密钥换取 token
    Then 返回 access token 与 refresh token
    When 以 refresh token 请求刷新
    Then 返回全新 access/refresh token 且新 access 可用
    When 以伪造 refresh token 请求刷新
    Then 返回未认证（401）

  @SUB-S46 @auto:e2e
  Scenario: 组织授权写入与校验（统一组织授权表 org_grant，资源类型 subsystem）
    Given 已创建父子两级组织角色并绑定成员
    When root 对子系统整体覆盖组织授权
    Then 保存成功；同一组织角色重复授权 400、不存在的组织角色 400
    And 指定团队空清单（仅组织授权）为合法配置

  @SUB-S47 @auto:e2e
  Scenario: 组织授权回显
    When root 查询子系统组织授权配置与子系统列表
    Then 回显授权角色（id/名称/方向）与列表 authorizedOrgs

  @SUB-S48 @auto:e2e
  Scenario: down 方向按子树命中
    When 父角色以 down 授权后，其子角色成员请求签发委托码
    Then 放行
    When 无团队且无组织角色的用户请求签发
    Then 403

  @SUB-S49 @auto:e2e
  Scenario: self 方向仅该角色命中
    When 授权方向改为 self
    Then 子角色成员 403，父角色成员放行

  @SUB-S50 @auto:e2e
  Scenario: up 方向含授权角色及以上
    When 授权子角色并选 up
    Then 父角色成员放行

  @SUB-S51 @auto:e2e
  Scenario: 团队与组织授权取并集
    When 指定团队 A 且组织授权存在
    Then 团队 A 成员（无组织角色）与组织成员任一命中即放行
    When 清空组织授权
    Then 组织成员 403，团队授权成员仍放行

  @SUB-S52 @auto:e2e
  Scenario: 组织授权管理门禁
    When 非 root 读写子系统组织授权
    Then 返回 403
```

```gherkin
Feature: 合并模型：子系统与应用接入同一接入（同表/同列表/同接口）
  @SUB-S53 @auto:e2e
  Scenario: 创建应用接入自动生成 appid
    When root 以 appType=app 创建应用接入
    Then 返回 moai-sa- 前缀 appid 与 48 位密钥原文（均仅此一次）
    And 应用接入无回调与授权配置，新建即启用

  @SUB-S54 @auto:e2e
  Scenario: 子系统 appid 禁用保留前缀
    When root 以 moai- 保留前缀自填 appid 创建子系统
    Then 返回参数错误（400）

  @SUB-S55 @auto:e2e
  Scenario: 列表同表回显两类接入
    When root 查询接入列表
    Then 子系统与应用接入同列表回显（类型/标识前缀/在线实例数/审计列）
    And 应用接入仅回显 key 前缀不回显原文，且无授权范围字段
    And 支持按类型过滤

  @SUB-S56 @auto:e2e
  Scenario: 直连轨仅限应用接入
    When 应用接入以 moai-sa- appid 直连协作面自省
    Then 返回 200
    When 子系统以自填 appid 直连
    Then 返回 401（自填 appid 可读性高，必须以 appid+密钥换取 token）

  @SUB-S57 @auto:e2e
  Scenario: 统一 token 换取与旧端点下线
    When 子系统或应用接入以 appid+密钥请求 /api/subsys/token
    Then 均换取成功（同一套接口）
    When 请求旧 /api/subsys/system/token 或 /api/subsys/system/info
    Then 端点已下线（404/405）

  @SUB-S58 @auto:e2e
  Scenario: 任一接入全量访问系统资源面
    When 任一系统级接入读写 /api/subsys/system/* 资源接口
    Then 均返回 200（资源范围分档已下线，无只读限制）

  @SUB-S59 @auto:e2e
  Scenario: 应用接入行无子系统专属端点
    When 对应用接入行查询授权团队、组织授权或写组织授权
    Then 返回 404
    When 以应用接入 appid 请求签发委托码
    Then 返回 404（委托登录为子系统专属）

  @SUB-S60 @auto:e2e
  Scenario: 应用接入同样可心跳
    When 应用接入携带实例标识心跳
    Then 心跳成功且列表/自省展示在线（与应用接入共用同一心跳端点）

  @SUB-S63 @auto:e2e
  Scenario: 查看密钥（信封加密回显，随时可查）
    When root 携带 RSA 加密的信封 key 请求回显密钥
    Then 以该 key 的 AES-GCM 密文返回原文与掩码，本地解密即可查看/复制（列表与传输全程无明文）
    When 对存量哈希行请求回显
    Then 返回 400 提示重置
```

```gherkin
Feature: 接入头像（子系统与应用接入通用，同表同字段）
  @SUB-S67 @auto:e2e
  Scenario: 创建接入携带头像
    Given root 已直传一张头像图片并完成登记（objectKey）
    When 创建子系统/应用接入时携带 avatarPath
    Then 创建成功且列表回显该 objectKey（前端拼装 /static 地址展示）

  @SUB-S68 @auto:e2e
  Scenario: 头像 objectKey 防伪校验
    When 创建或更新携带未登记/未完成上传的 avatarPath
    Then 返回 404（头像文件不存在或未完成上传，防止任意伪造 objectKey）

  @SUB-S69 @auto:e2e
  Scenario: 更新替换与保持
    Given 接入已设置头像
    When 更新携带新的合法 avatarPath
    Then 更新成功且列表回显新 objectKey
    When 更新不携带 avatarPath（如停用切换等调用）
    Then 头像保持不变（null=不修改）

  @SUB-S70 @auto:e2e
  Scenario: 不带头像默认为空
    When 创建接入不携带 avatarPath
    Then 列表回显头像为空串（前端显示名称首字符兜底）；子系统与应用接入同表同结构均回显

Feature: 系统应用接入（系统级接入 key）
  @SAA-S1 @auto:e2e
  Scenario: 管理面仅 root 可用
    Given 已注册普通用户
    When 普通用户查询或创建系统应用接入
    Then 返回 403

  @SAA-S2 @auto:e2e
  Scenario: 创建与一次性 key
    When root 创建应用接入（appid 自动生成，无资源范围选择）
    Then 返回 moai-sa- 前缀的 appid 与密钥原文（均仅此一次）
    And 列表仅回显 key 前缀（appid 为直连凭证不回显原文）

  @SAA-S4 @auto:e2e
  Scenario: key 直连自省
    When 携带正确 key 请求协作面自省（统一入口 /api/subsys/info）
    Then 返回接入身份（类型/状态），不含资源范围
    When 携带伪造 key 或其它前缀 key 请求
    Then 返回 401

  @SAA-S5 @auto:e2e
  Scenario: 更新即时生效
    When root 修改接入名称
    Then 自省即时回显新名称
    When 更新不存在的接入
    Then 返回 404

  @SAA-S6 @auto:e2e
  Scenario: 停用与恢复
    Given 一个启用的系统应用接入
    When root 停用后自省
    Then 返回 403
    When root 重新启用后自省
    Then 返回 200

  @SAA-S7 @auto:e2e
  Scenario: 删除即失效
    When root 删除系统应用接入
    Then 该 key 自省返回 401 且列表不再可见

  @SAA-S8 @auto:e2e
  Scenario: 搜索
    When root 按 key 前缀搜索
    Then 仅命中匹配的接入

  @SAA-S9 @auto:vitest
  Scenario: 管理页单列表与类型列
    Given root 打开应用接入中心
    Then 子系统与应用接入在同一列表展示（类型列区分，不再分 Tab）
    And 列表展示接入名称、类型、appid（前缀）、在线、状态与审计列
    And 非 root 无法进入页面

  @SAA-S10 @auto:vitest
  Scenario: 创建弹窗与一次性 key 展示
    When root 提交新建表单
    Then 弹窗展示 key 原文与仅显示一次提示

  @SAA-S11 @auto:e2e
  Scenario: 更新不存在的接入
    When root 更新不存在的接入 id
    Then 返回 404

  @SAA-S12 @auto:e2e
  Scenario: 创建返回 appid 原文与密钥掩码
    When root 创建系统应用接入
    Then 返回 moai-sa- 前缀 appid 原文与密钥展示掩码（不回原文，原文可随时信封查看）
    And appid 直连协作面自省返回 200

  @SAA-S13 @auto:e2e
  Scenario: 错误密钥换取 token 被拒
    When 以 appid 与错误密钥请求换取 token
    Then 返回 401

  @SAA-S14 @auto:e2e
  Scenario: appid+密钥换取 access/refresh token
    When 以正确 appid+密钥请求换取
    Then 返回 access token、refresh token 与有效期
    And 以 access token 调用协作面自省与系统资源接口均返回 200（token 轨）

  @SAA-S15 @auto:e2e
  Scenario: refresh token 轮换
    When 以 refresh token 请求刷新
    Then 返回全新 access/refresh 且新 access 可用
    When 以伪造 refresh token 请求刷新
    Then 返回 401

  @SAA-S16 @auto:e2e
  Scenario: 停用后双轨凭据即时拒绝
    Given 系统应用接入已停用
    When 以 appid 直连或以 access token 调用协作面
    Then 均返回 403
    When root 重新启用
    Then token 轨恢复 200

  @SAA-S17 @auto:e2e
  Scenario: 重置密钥旧密钥立即失效
    When root 重置密钥
    Then 返回 48 位新密钥原文（仅此一次）
    And 旧密钥换取 token 返回 401，新密钥换取返回 200

  @SAA-S18 @manual
  Scenario: （已退役）范围分档——只读接入可读不可写
    Given 资源范围分档已随合并模型下线（见 @SUB-S58）
    Then 本场景退役，编号不复用

  @SAA-S60 @auto:e2e
  Scenario: 资源面全量访问
    When 任一系统级接入直连写组织角色或写用户
    Then 均返回 200（原 scope 分档 403 语义随合并模型移除）

  @SAA-S61 @auto:e2e
  Scenario: 子系统与应用接入同一套接口
    Given 子系统与应用接入各自换取 token
    When 分别访问 /api/subsys/system/* 资源面
    Then 均返回 200（同一套接口调用 MoAI 平台资源）

  @SAA-S62 @auto:e2e
  Scenario: 应用接入心跳在线
    When 应用接入携带实例标识心跳
    Then 自省回显在线与在线实例数

  @SAA-S19 @auto:e2e
  Scenario: 用户 upsert 创建返回初始密码
    When 按 userName 推送新用户（未提供密码）
    Then 创建成功并返回一次性初始密码
    And 该初始密码可用于登录

  @SAA-S20 @auto:e2e
  Scenario: 用户 upsert 重复推送幂等更新
    When 以相同 userName 再次推送（更新昵称）
    Then 返回 created=false 与同一用户 id，且不返回初始密码

  @SAA-S21 @auto:e2e
  Scenario: 用户推送参数校验
    When 推送非法邮箱、与他用户冲突的邮箱或弱密码
    Then 分别返回 400、409、400

  @SAA-S22 @auto:e2e
  Scenario: 超级管理员账号受保护
    When 推送 userName=admin 且要求禁用
    Then 返回 400 拒绝禁用

  @SAA-S23 @auto:e2e
  Scenario: 禁用与启用用户
    When 禁用用户后其登录被拒
    And 重新启用返回 200

  @SAA-S63 @auto:e2e
  Scenario: 用户 upsert 携带第三方身份绑定
    When 推送用户并携带连接器 id、第三方标识与 union_id
    Then 返回 oauthBound=true（新建时附一次性初始密码）
    When 以相同用户与身份重复推送
    Then 幂等复用绑定（created=false、oauthBound=true、userId 不变）
    When 同一身份绑给另一用户，或同一用户在同一渠道更换标识
    Then 返回 409；未知渠道 400
    And 绑定冲突在写用户前被拒绝，不留下半成品用户

  @SAA-S64 @auto:e2e
  Scenario: 为第三方身份颁发内部应用访问用户 token
  When 以系统级凭据提交应用 id、连接器 id 与第三方标识
  Then 返回 accessToken/refreshToken/expiresIn、站内 userId 与 userName
  When 第三方身份未绑定站内用户
  Then 在本次请求内自动创建用户并绑定第三方身份，返回 userCreated=true
  When 使用相同第三方身份再次请求
  Then 幂等复用同一用户并返回 userCreated=false
  When 命中多个用户、用户被禁用、目标为外部应用或未发布/访问点关闭
  Then 分别返回 409、403、400、403
    When 目标为已发布且访问点开启的内部应用
    Then 颁发的 token 仅能访问该应用，刷新按用户/应用/访问点状态重验

  @SAA-S66 @auto:e2e
  Scenario: 系统级子系统与系统级应用接入共享用户身份能力
    When 以系统级子系统接入凭据调用内部应用访问用户 token 颁发接口
    Then 返回访问用户 token
    When 以团队级应用接入凭据调用该接口
    Then 返回未认证

  @SAA-S65 @auto:e2e
  Scenario: 访问用户 token 自动建户不产生孤儿用户
    When 对未发布或访问点关闭的内部应用以全新第三方身份申请 token
    Then 请求被拒且不会创建用户或 OAuth 绑定
    When 目标应用恢复可用后以相同第三方身份再次申请
    Then 本次才自动创建用户并完成绑定

  @SAA-S24 @auto:e2e
  Scenario: 组织角色创建与层级
    When 创建根角色与子角色
    Then 列表按 path 物化路径回显父子层级
    When 同父创建重名角色
    Then 返回 409

  @SAA-S25 @auto:e2e
  Scenario: 用户角色分配与查询
    When 整体覆盖用户角色集合
    Then 角色下用户清单与用户持有角色均可回显

  @SAA-S26 @auto:e2e
  Scenario: 组织角色删除保护
    When 删除仍有子角色的角色
    Then 返回 400
    When 清空用户角色后删除子角色
    Then 返回 200

  @SAA-S27 @auto:e2e
  Scenario: 系统创建团队
    When 创建团队
    Then 返回团队 id 且不添加成员
    When 创建同名团队
    Then 返回 409

  @SAA-S28 @auto:e2e
  Scenario: 团队更新与团队列表
    When 更新团队简介与查询全部团队
    Then 更新生效且列表含新建团队

  @SAA-S29 @auto:e2e
  Scenario: 团队成员幂等 upsert
    When 添加成员后以不同角色重复推送
    Then 两次均成功且角色更新为最新

  @SAA-S30 @auto:e2e
  Scenario: 移除团队成员
    When 移除成员后再移除一次
    Then 第一次 200、第二次 404

  @SAA-S31 @auto:e2e
  Scenario: 删除接入后全链凭据失效
    When root 删除系统应用接入
    Then token 轨调用、密钥换取与 refresh 刷新均返回 401

  @SAA-S58 @manual
  Scenario: （已退役）管理面板资源分组
    Given 资源范围编辑器已随范围分档下线移除（新建应用接入即开即用）
    Then 本场景退役，编号不复用；重置密钥弹窗行为由 @SAA-S10 承接

  @SAA-S59 @auto:vitest
  Scenario: 团队接入面板密钥展示与重置
    When 管理员创建团队接入
    Then 弹窗同时展示 appid 与一次性密钥
    When 点击重置密钥并确认
    Then 弹窗一次性展示新密钥
