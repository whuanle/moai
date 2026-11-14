# 通知中心（Notification，NT）行为规格（BDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 证据：[local-dev/notification-e2e.mjs](../../local-dev/notification-e2e.mjs)

Feature: 通知渠道管理（仅管理员）

@NT-S1 渠道接口门禁
@auto:e2e
  Scenario: 未登录与非管理员访问被拒
    When 未登录用户请求渠道列表
    Then 返回 401
    When 非管理员请求渠道列表、创建渠道或发送广播
    Then 均返回 403

@NT-S2 创建渠道与配置校验
@auto:e2e
  Scenario: 按类型创建渠道
    When 管理员按飞书/钉钉/企微/Telegram/Slack/Discord/Teams/通用 webhook/SMTP/Gotify/ntfy/Bark/Server酱/PushPlus/站内推送等类型提交合法配置
    Then 创建成功并返回渠道 id
  Scenario: 非法请求被拒
    When 名称与既有渠道重复
    Then 返回 400
    When webhook 地址不是合法 http(s) 地址、配置不是有效 JSON、Telegram 缺少 chatId、
      And 站内推送未配置任何收件人
    Then 均返回 400
  Scenario: SMTP 渠道创建不再要求收件人
    When 管理员创建不含收件人邮箱的 SMTP 渠道
    Then 创建成功（收件人与邮件主题前缀已移出渠道配置，改为发送时动态指定；旧收件人校验用例已退役）

@NT-S3 渠道列表与搜索
@auto:e2e
  Scenario: 列表回显与过滤
    When 管理员查询渠道列表并按本次运行标识搜索
    Then 仅返回本批渠道，配置原样回显，创建/更新人名回填

@NT-S4 更新渠道约束
@auto:e2e
  Scenario: 更新渠道
    When 管理员改名并更新配置
    Then 列表回显新名称
    When 把渠道更新为另一种类型
    Then 返回 400（类型不可变更）
    When 更新为既有渠道名称
    Then 返回 400
    When 更新或删除不存在的渠道
    Then 返回 404

Feature: 渠道测试（同步试发，经领域服务留痕）

@NT-S5 七类渠道测试成功
@auto:e2e
  Scenario: 各渠道收到测试通知
    When 管理员对飞书渠道（按 id）执行测试
    Then 返回成功，桩收到 msg_type=text、timestamp 与验签通过的 sign
    When 用钉钉配置（未保存）试发
    Then 返回成功，请求 URL 附 timestamp/sign 且验签通过
    When 测试企业微信、Telegram、通用 webhook（默认结构与自定义头）、模板渲染
    Then 均返回成功且桩按各渠道协议收到正确载荷
    When 测试 Slack/Discord（204）/Teams/Gotify（token 查询参数）/ntfy（根路径 JSON）/Bark（设备键）/Server酱（form）/PushPlus
    Then 均返回成功且载荷符合各渠道协议

@NT-S6 渠道测试失败回显
@auto:e2e
  Scenario: 外部接口异常原样回显
    When 飞书机器人返回 code!=0、webhook 返回 HTTP 500、Telegram 返回 ok=false
    Then 测试返回成功=false 且失败原因包含对方错误信息
    When 对不存在的渠道 id 执行测试
    Then 返回 404

@NT-S21 SMTP 渠道自带连接配置（旧 @NT-S7「联动系统设置」随 SMTP 连接收敛到渠道作废不复用）
@auto:e2e
  Scenario: 渠道自包含连接发信且独立于系统设置
    When 管理员创建含主机/端口/加密方式/发件人的 SMTP 渠道，系统「邮箱验证」保持关闭
    Then 携带试发收件人邮箱测试发送成功，本地桩捕获到试发指定收件人与主题（收件人随请求动态提供）
    When 连接配置非法（host 为空/端口越界/加密方式非法/发件人非法）时创建
    Then 返回请求错误（400）（校验语义见 [@SET-S61](../settings/bdd.md#set-s61)）
    When 系统设置「邮箱验证」绑定该渠道并启用
    Then 平台系统邮件（验证码等）按同一渠道配置发送（[@SET-S64](../settings/bdd.md#set-s64)）

@NT-S8 站内推送收件人解析
@auto:e2e
  Scenario: 推送全部管理员
    When 渠道配置开启「推送全部管理员」
    Then 测试返回成功，admin 用户可在站内信中看到测试标题

Feature: 广播投递与发送记录

@NT-S9 广播投递全部启用渠道
@auto:e2e
  Scenario: 异步落发送记录
    When 管理员发送广播（所选渠道含 SMTP 时附收件人邮箱、含短信时附收件人手机号）
    Then 返回本次投递的启用渠道数量
    And 每个启用渠道各产生一条发送记录，最终均为成功（站内/SMTP/webhook/机器人）

@NT-S10 失败渠道终态与原因
@auto:e2e
  Scenario: 失败渠道记录原因
    When 广播包含返回 500 的 webhook 与签名失败的飞书渠道
    Then 对应记录终态为失败且失败原因非空
    And 记录快照包含渠道类型/级别/耗时

@NT-S11 发送记录过滤
@auto:e2e
  Scenario: 按渠道与状态过滤
    When 按渠道 id 过滤发送记录
    Then 仅返回该渠道的记录
    When 按失败状态过滤
    Then 仅返回失败记录

@NT-S22 发送记录关联用户快照
@auto:e2e
  Scenario: 广播记录无关联用户
    When 广播落发送记录
    Then 每条记录的关联用户为空（广播面向全员，不定向）
    And 列表接口回显关联用户 id 快照与用户名字段（均为空串）
  Scenario: 定向个人推送记录收件人
    When 业务方经通知服务以定向站内用户集合投递（应用机器人个人推送）
    Then 该渠道发送记录快照收件人 id（逗号分隔，超长截断）
    And 列表接口按快照 id 回显用户名（用户已删除的跳过）

@NT-S12 停用渠道不投递
@auto:e2e
  Scenario: 停用后广播
    When 停用一个渠道后再次广播
    Then 返回的投递渠道数减一，该渠道无新发送记录

@NT-S13 站内推送渠道送达站内信
@auto:e2e
  Scenario: 广播经站内渠道触达管理员
    When 广播包含站内推送渠道
    Then admin 用户站内信中收到该广播标题

@NT-S14 删除渠道保留历史
@auto:e2e
  Scenario: 删除后历史可查
    When 删除渠道
    Then 渠道列表不再包含它，历史发送记录仍保留（渠道名快照可读）

@NT-S15 广播参数校验
@auto:e2e
  Scenario: 空标题被拒
    When 广播标题为空
    Then 返回 400
  Scenario: 动态收件人按所选渠道类型必填
    When 广播所选渠道含 SMTP 但未提供收件人邮箱
    Then 返回 400
    When 广播所选渠道含短信但未提供收件人手机号
    Then 返回 400
    When 广播提供的收件人手机号格式非法（非纯数字或长度越界）
    Then 返回 400

@NT-S16 广播目标组合（渠道多选与站内信）
@auto:e2e
  Scenario: 指定渠道广播
    When 广播仅指定一个启用渠道（channelIds）
    Then 返回投递渠道数 1，且该渠道产生一条成功发送记录
  Scenario: 仅发送站内信
    When 广播不选渠道、仅勾选发送站内信
    Then 返回投递渠道数 0，全部启用用户的站内信中收到该广播
  Scenario: 渠道与站内信均未选择
    When 广播既不选渠道也不勾选站内信
    Then 返回 400

@NT-S32 发送记录实际收件人快照
@auto:e2e
  Scenario: SMTP 渠道记录收件人邮箱
    When 广播含 SMTP 渠道并附收件人邮箱
    Then 该渠道发送记录快照实际收件人邮箱（逗号分隔，超长截断）
    And 其余固定地址渠道（webhook 等）发送记录收件人为空

Feature: 站内信托底（领域服务定向通知语义 + 渠道试发留痕）

@NT-S33 定向通知站内信必达
@auto:e2e
  Scenario: 渠道投递失败不影响站内信送达
    When 业务方定向站内用户发送通知且所选外部渠道投递失败（如用户未绑定应用机器人平台，整批零绑定）
    Then 这些用户的站内信已先行送达（HTML 内容降级纯文本）
    And 该渠道发送记录终态失败并留痕原因
  Scenario: 无定向目标的全员型通知不自动发站内信
    When 业务方未指定定向用户集合（如登录安全高风险渠道告警）
    Then 不自动补发站内信（站内触达由调用方自行决定，如广播的发送站内信选项）

@NT-S34 渠道试发走领域服务（留痕 + 站内信托底）
@auto:e2e
  Scenario: 已保存渠道试发落发送记录
    When 管理员对已保存渠道执行测试
    Then 产生一条发送记录（关联用户快照=试发人），终态与渠道实际投递结果一致
  Scenario: 试发人站内信副本必达
    When 管理员对渠道执行测试
    Then 无论渠道投递成败，试发人站内信均收到测试通知副本
  Scenario: 保存前试发不落记录
    When 管理员用未保存配置试发（临时渠道不入库）
    Then 不产生发送记录，试发人站内信副本同样送达

Feature: 发送失败数量角标（管理员侧边栏「通知中心」）

@NT-S35 发送失败记录数量角标
@auto:e2e
  Scenario: 查询发送失败记录数量
    When 管理员查询发送失败的记录数量
    Then 仅管理员可访问（未登录 401、普通用户 403）
    And 数量与发送记录列表按 status=failed 过滤的总数一致
    And 前端管理员侧边栏「通知中心」菜单按该数量显示角标（60s 轮询）

Feature: 应用机器人渠道（飞书/钉钉/企业微信 App 凭证，按第三方账号绑定反查个人推送）

@NT-S17 应用机器人渠道配置校验
@auto:e2e
  Scenario: 缺失关键凭证被拒
    When 管理员创建飞书应用机器人渠道但缺少 App ID
    Then 返回 400
    When 创建钉钉应用机器人渠道但缺少 AgentId
    Then 返回 400
    When 创建企业微信应用机器人渠道但缺少应用 Secret
    Then 返回 400
  Scenario: 凭证齐全创建成功
    When 管理员按三类应用机器人类型提交完整凭证（服务地址可指向自建/桩）
    Then 创建成功且列表回显对应类型

@NT-S18 应用机器人绑定链路
@auto:e2e
  Scenario: 用户绑定第三方账号后具备可投递标识
    When 管理员创建三个 OAuth 连接（飞书/钉钉/企业微信，凭证独立于通知渠道应用）
    And 用户经第三方授权绑定三个平台账号
    Then 绑定记录持有平台侧用户标识（飞书 open_id 与 union_id、钉钉 unionId、企业微信 userid）

@NT-S19 应用机器人试发定向当前管理员
@auto:e2e
  Scenario: 未绑定时试发失败
    When 管理员尚未绑定企业微信账号时测试企业微信应用机器人渠道
    Then 测试返回失败，原因提示没有站内用户绑定该平台
  Scenario: 绑定后试发按本人绑定投递
    When 管理员绑定三平台后分别测试三类应用机器人渠道
    Then 飞书以 union_id 收件（缺失时退回 open_id）且文本含通知标题
    And 钉钉将绑定 unionId 换算为企业内 userid 并按 AgentId 投递工作通知
    And 企业微信按绑定 userid 直投应用消息并携带 AgentId

@NT-S20 应用机器人广播按绑定反查投递
@auto:e2e
  Scenario: 广播触达全部已绑定用户
    When 两个用户均绑定三平台后广播指定三类应用机器人渠道
    Then 飞书逐用户单聊推送（union_id 收件）
    And 钉钉工作通知 userid_list 同时包含两用户换算后的 userid
    And 企业微信应用消息 touser 同时包含两用户 userid
  Scenario: 通知应用与登录应用跨企业时渠道失败可观测
    When 平台侧无法把绑定 unionId 换算到通知应用所属企业
    Then 广播该渠道的发送记录终态为失败，原因说明 unionId 均未换算

Feature: 通知模板（文本 + 变量定义，发送时插值渲染）

@NT-S23 模板接口门禁与 CRUD 校验
@auto:e2e
  Scenario: 未登录与非管理员访问被拒
    When 未登录用户请求模板列表
    Then 返回 401
    When 非管理员创建模板
    Then 返回 403
  Scenario: 按规则创建模板
    When 管理员提交名称、主题、正文、内容类型（纯文本/HTML）、变量定义与可选的平台模板映射
    Then 创建成功并返回模板 id，列表回显含审计人名
  Scenario: 非法请求被拒
    When 名称与既有模板重复、正文为空、变量定义不是合法 JSON 数组、变量名不合法或重复
    Then 均返回 400
  Scenario: 更新模板
    When 管理员整体覆盖更新模板字段
    Then 列表回显新内容
    When 更新为既有模板名称或更新不存在的模板
    Then 分别返回 400 与 404

@NT-S24 模板渲染预览与变量校验
@auto:e2e
  Scenario: 渲染插值正确且花括号字面保留
    When 管理员按变量定义提供全部变量值请求渲染预览
    Then 标题与正文中的 {{变量}} 双花括号占位符被替换为变量值，未定义的占位符（如 JSON 片段、{0}、单花括号 {x}）按字面保留
  Scenario: 变量值不完整或模板不存在
    When 提供的变量值未覆盖模板全部变量定义
    Then 返回 400 且错误信息列出缺失变量名
    When 请求渲染不存在的模板
    Then 返回 404

@NT-S25 模板删除保留历史
@auto:e2e
  Scenario: 删除模板
    When 管理员删除模板
    Then 列表移除该模板，历史发送记录与站内信中已发送的渲染文本不受影响

Feature: 内置通知模板（种子提供，验证码邮件等，不可删除可编辑）

@NT-S31 内置模板回显与保护
@auto:e2e
  Scenario: 种子内置模板存在且回显内置标识
    Given 新部署平台种子内置注册/登录/找回密码三条验证码邮件模板（HTML，变量 {{code}}/{{siteName}}/{{expireMinutes}} 双花括号形态）
    When 管理员查询模板列表
    Then 三条模板均带内置标识（isBuiltIn）且内容类型为 HTML
  Scenario: 内置模板不可删除可编辑
    When 管理员删除内置模板
    Then 返回请求错误（400）提示内置通知模板不可删除
    When 管理员编辑内置模板的标题/正文
    Then 编辑成功且列表回显新内容（可改回）

Feature: 短信渠道（阿里云/腾讯云，平台侧已审核模板发送）

@NT-S26 短信渠道配置校验
@auto:e2e
  Scenario: 缺失关键配置被拒
    When 管理员创建阿里云短信渠道但缺少签名名称
    Then 返回 400
    When 创建腾讯云短信渠道但缺少短信应用 SdkAppId
    Then 返回 400
  Scenario: 凭证齐全创建成功
    When 管理员按阿里云/腾讯云类型提交完整配置（服务地址可指向自建/桩）
    Then 创建成功且列表回显对应类型（收件人手机号已移出渠道配置，改为发送时动态指定；旧手机号配置校验用例已退役）

@NT-S27 短信渠道必须经模板发送
@auto:e2e
  Scenario: 无模板直发失败
    When 直接测试短信渠道（不使用通知模板）
    Then 渠道发送失败，原因提示需使用通知模板
  Scenario: 模板试发缺收件人手机号失败
    When 用已映射平台模板的模板向短信渠道试发但未提供收件人手机号
    Then 试发失败，原因提示收件人手机号由发送时动态提供
  Scenario: 模板未映射对应平台
    When 用未配置阿里云模板 Code 的模板向阿里云短信渠道试发
    Then 试发失败，原因提示需配置阿里云模板 Code
    When 用未配置腾讯云模板 Id 的模板向腾讯云短信渠道试发
    Then 试发失败，原因提示需配置腾讯云模板 Id

@NT-S28 短信试发请求参数与平台错误归一
@auto:e2e
  Scenario: 阿里云按 POP 协议发送
    When 管理员用已映射阿里云模板 Code 的模板向阿里云短信渠道试发（附收件人手机号）
    Then 请求查询串携带签名、时间戳、试发指定收件手机号、签名名与模板 Code，模板参数为变量值 JSON
    When 平台返回业务错误码
    Then 试发失败且错误归一为平台错误码与描述
  Scenario: 腾讯云按 TC3 协议发送
    When 管理员用已映射腾讯云模板 Id 的模板向腾讯云短信渠道试发（附收件人手机号）
    Then 请求携带 TC3 签名头，JSON 请求体携带 SdkAppId、签名名、模板 Id、号码集，参数数组按模板正文占位符出现顺序排列
    When 平台返回单号码失败状态或顶层 Error 对象
    Then 试发失败且错误归一为平台错误码与描述

Feature: HTML 模板与广播带模板

@NT-S29 HTML 模板分渠道投递
@auto:e2e
  Scenario: 邮件渠道按 HTML 发送
    When 用 HTML 模板渲染结果经 SMTP 渠道发送
    Then 邮件正文为 text/html 且保留模板原始结构与样式
  Scenario: 非邮件渠道降级纯文本
    When 用 HTML 模板渲染结果经通用 webhook 渠道发送
    Then 载荷正文为去标签纯文本

@NT-S30 广播带模板
@auto:e2e
  Scenario: 标题正文可空由模板生成
    When 广播选择通知模板并提供全部变量值，标题与正文留空
    Then 入队成功，发送记录与站内信收到渲染后的标题与正文
  Scenario: 变量或模板无效被拒
    When 广播提供的变量值不完整
    Then 返回 400
    When 广播指定的模板不存在
    Then 返回 404
