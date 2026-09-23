# 技能（Skill）模块行为规格（BDD）

> 关联：[SDD](./sdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 证据：[local-dev/skill-userconfig-e2e.mjs](../../local-dev/skill-userconfig-e2e.mjs) ｜ [local-dev/skill-market-e2e.mjs](../../local-dev/skill-market-e2e.mjs)

## 管理端（IsAdmin）

```gherkin
Feature: 技能管理
  Background:
    Given 平台管理员已登录

  Scenario: 分页搜索
    When 请求 GET /api/skill/list?pageNo=1&pageSize=20&searchText=docx
    Then 200，返回 totalCount 与 items（key/name/description/isSystem/isDisable/fileCount/createTime/updateTime）
    And fileCount：内置技能取内嵌清单数量，自定义技能取 files JSON 数量

  Scenario: 创建技能校验
    When POST /api/skill { key: "Bad Key", ... }
    Then 400 技能标识仅允许小写字母开头（^[a-z][a-z0-9_]{0,29}$）
    When POST /api/skill { key: "docx_writer", ... }（已存在）
    Then 409 技能标识已存在
    When files 含路径带 ".." 或以 "/" 开头
    Then 400 文件路径不合法
    When files 含未上传完成的 fileId
    Then 400 存在未上传完成的技能包文件

  Scenario: 内置技能保护
    When DELETE /api/skill/{docx_writer id}
    Then 400 系统内置技能不可删除，可使用禁用
    When PUT /api/skill/{id}/disable { isDisable: true }
    Then 200，技能从 options 与运行时加载中消失

  Scenario: 包文件上传
    When POST /api/skill/file/preupload { fileName: "x.exe", ... }
    Then 400 不支持该技能包文件格式（白名单：py/md/json/txt/csv/yaml/yml/j2/html/css/js）
    When 上传 .py 文件 → complete → 创建技能引用该 fileId
    Then 201/200 全链路成功，文件落 MinIO 前缀 skill/

  Scenario: 非管理员
    Given 普通用户登录
    When GET /api/skill/list
    Then 403 只有管理员可以管理技能
    When GET /api/skill/options
    Then 200（挂载选项对登录用户开放）

## 应用挂载（团队 Admin+）

  Scenario: 挂载校验
    When 保存应用配置 skills 含已禁用/不存在/无权使用（非系统内置、非市场公开、非本团队）的技能 id
    Then 400 包含不存在、已禁用或无权使用的技能，请重新选择
    When 保存时不携带 skills 字段（null）
    Then 已保存技能保持不变（兼容旧前端整体替换式保存）
    When 查询应用配置
    Then 响应含 skills（uuid 数组回显）

## Agent 运行时

  Scenario: 技能加载（沙箱开启）
    Given 应用挂载 docx_writer 且 execution_settings.sandbox.enabled=true
    And Agent 发起 call_tool skill_docx_writer
    Then 技能文件写入 /workspace/skills/docx_writer/
    And tool result 含 instructions 与文件清单
    When Agent 调 sandbox_run_code 执行 generate_docx.py
    Then 脚本内 _ensure 自动 pip 安装 python-docx（--break-system-packages）
    And build_docx(outline) 生成 /workspace/output.docx

  Scenario: 产物交付
    When Agent 调 sandbox_save_artifact { path: "/workspace/output.docx" }
    Then 字节读取→SHA256→上传 appagent/{sessionId:N}/ 前缀→file 记录
    And 返回 { fileId, fileName, downloadUrl(1h), expiresInMinutes }
    And Agent 将 downloadUrl 呈现给用户

  Scenario: 沙箱未启用降级
    Given 应用挂载技能但未开启沙箱
    When Agent 调 skill_{key}
    Then 200 返回 instructions，message 提示"未启用沙箱，脚本无法写入执行"
    And 不发生任何沙箱写入

## 归属与三级权限（2026-09-16 增量）

```gherkin
Feature: 技能归属与三级权限
  Background:
    Given 用户已登录（平台管理员 / 团队管理员 / 普通用户）

  @SKL-S1 @auto:e2e
  Scenario: 个人技能归属创建人，仅本人可见可用
    When Alice 创建个人技能（teamId=0）
    Then 创建成功，Alice 的技能选项（includePersonal）包含该技能
    And Bob 的技能选项不包含 Alice 的个人技能

  @SKL-S2 @auto:e2e
  Scenario: 团队技能仅团队管理员可创建
    When 团队 Member 创建团队技能（teamId>0）
    Then 403 只有团队管理员可以创建团队技能
    When 团队 Owner 创建团队技能
    Then 创建成功，团队成员的技能选项（teamId）包含该技能且不含他人个人技能

  @SKL-S3 @auto:e2e
  Scenario: 非归属人不可管理他人技能
    When Bob 更新或删除 Alice 的个人技能
    Then 403 只有技能创建人可以管理该技能
    When 团队 Member 禁用团队的技能
    Then 403 只有团队管理员可以管理该技能

  @SKL-S4 @auto:e2e
  Scenario: 归属人可更新并删除个人技能
    When Alice 更新自己的个人技能
    Then 200
    When Alice 删除自己的个人技能
    Then 200（软删除），选项与运行时加载中消失
```

## 用户级应用配置（2026-09-16 增量；2026-09-19 改为「应用默认技能」模型）

```gherkin
Feature: 用户级应用配置（跨会话复用，技能仅可在默认范围内勾选）
  Background:
    Given 团队拥有应用 App，管理员已配置默认技能，成员 Charlie 已登录

  @SKL-S5 @auto:e2e
  Scenario: 管理员配置默认技能
    When 管理员保存应用配置（skills=技能列表）
    Then 保存成功，应用配置回显 skills
    And 技能候选=系统内置∪市场公开∪本团队（不含个人技能）

  @SKL-S6 @auto:e2e
  Scenario: 用户默认全选与目录回显
    When Charlie 首次查询应用用户配置（未保存过）
    Then skills=应用默认技能全集（默认全部启用），promptId=0
    And 响应携带默认技能目录（defaultSkills）

  @SKL-S7 @auto:e2e
  Scenario: 技能仅可在默认范围内勾选
    When Charlie 保存勾选默认技能子集并选择可用专家
    Then 保存成功，回显 skills 与 promptId
    When Charlie 保存应用未开放的技能（如他人个人技能）
    Then 400 存在应用未开放的技能
    When Charlie 保存不可用的专家提示词（如他人个人提示词）
    Then 404 提示词不存在或不可用
    When 非团队成员查询该应用用户配置
    Then 404

  @SKL-S8 @manual
  Scenario: 运行时技能按勾选交集生效
    Given 应用默认技能 A、B，Charlie 已在应用设置中取消勾选 B
    When Charlie 发起对话并触发 list_tools
    Then 工具列表含 skill_A 且不含 skill_B
    And Charlie 未保存过用户配置时默认技能全部生效
    And 默认技能被删除/禁用后，后续对话静默剔除该技能

  @SKL-S9 @auto:vitest
  Scenario: 应用设置面板仅展示默认技能目录
    When Charlie 打开对话页「应用设置」面板
    Then 仅展示应用默认技能目录（无锁定项），技能可勾选/取消
    And 专家列表为本人个人提示词与本团队提示词，位于技能目录之上；保存提交勾选技能与专家（新会话默认，已有会话且变化时即时切换）

  @SKL-S10 @auto:vitest
  Scenario: 应用配置页配置默认技能
    When 团队管理员在应用配置页选择技能并保存
    Then 写入 app_agent_config.skills
    And 技能选项不含个人技能
```

## 技能市场与个人维护（2026-09-17 增量）

```gherkin
Feature: 技能市场与个人/团队自助维护
  Background:
    Given 用户已登录（Alice 建个人技能，Bob 旁观，团队 Owner/Member 各一，平台管理员审批）

  @SKM-S1 @auto:e2e
  Scenario: 我的技能列表
    When Alice 创建个人技能并上传包文件
    Then Alice 的 my_list 含该技能（未公开、无待审、文件数正确）
    And Bob 的 my_list 不含 Alice 的个人技能

  @SKM-S2 @auto:e2e
  Scenario: 团队技能列表
    When 团队成员请求 team_list
    Then 200 且含本团队技能
    When 非团队成员请求该团队 team_list
    Then 404 团队不存在或你不是团队成员

  @SKM-S3 @auto:e2e
  Scenario: 详情可见性
    When Alice 查看自己的技能详情
    Then 200
    When Bob 查看 Alice 的未公开个人技能详情
    Then 403 无权查看该技能
    When 该技能上架后 Bob 再查看详情
    Then 200

  @SKM-S4 @auto:e2e
  Scenario: 技能包下载
    When Alice 请求自己的技能下载地址
    Then 200，返回每个文件的预签名下载地址（1 小时有效，带 response-content-disposition 强制浏览器附件下载）
    When Bob 请求该未公开技能的下载地址
    Then 403
    When 该技能上架后 Bob 再请求下载地址
    Then 200

  @SKM-S5 @auto:e2e
  Scenario: 个人技能上架审批
    When Bob 申请上架 Alice 的个人技能
    Then 403 只有创建人可以申请上架个人资源
    When Alice 申请上架
    Then 200，my_list 回显待审核申请 id
    And 重复申请 409
    When Bob 撤回该申请
    Then 403
    When Alice 撤回后再重新申请，管理员审批通过
    Then 技能 is_public=true，重复审批 409

  @SKM-S6 @auto:e2e
  Scenario: 技能市场列表
    When 任意登录用户请求 market_list
    Then 200，含全部已上架技能（个人与团队），并带创建人姓名

  @SKM-S7 @auto:e2e
  Scenario: 团队技能上架
    When 团队 Member 申请上架团队技能
    Then 403 只有团队管理员可以申请上架
    When 团队 Owner 申请且管理员审批通过
    Then 市场列表含该团队技能

  @SKM-S8 @auto:e2e
  Scenario: 内置技能市场保护
    Given 库中存在系统内置技能（SkillSeed 种子）
    When 申请上架内置技能
    Then 400 系统内置技能无需申请上架
    When 请求内置技能下载地址
    Then 400 系统内置技能不支持下载
    And 市场列表不含内置技能

  @SKM-S9 @auto:e2e
  Scenario: 删除联动清理待审核申请
    Given 技能已有待审核的上架申请
    When 归属人删除该技能
    Then 200，且管理员审批该申请时 404 申请不存在（无僵尸记录）

  @SKM-S10 @manual
  Scenario: 技能分类接入
    Given 管理员在分类管理「技能」页签维护分类（可配 emoji）
    When 创建/更新技能选择该分类
    Then 保存成功，列表与详情返回 classifyId
    When 提交不存在的 skill 分类 id
    Then 返回 404「技能分类不存在」
    When 技能中心（市场/我的）点击分类 chip（emoji + 名称）
    Then 列表按 classifyId 过滤

  @SKM-S11 @auto:e2e
  Scenario: 技能压缩包上传自动解压
    Given 登录用户上传 .zip 技能包（三段直传完成）
    When 调用 /skill/file/extract
    Then 服务端解压：每个条目按 sha256 登记为 skill/ 前缀资源文件并返回清单
    And 压缩包唯一顶层目录被剥离（SKILL.md 落根路径）
    And 解析 SKILL.md frontmatter 的 name/description 与正文 instructions 随响应返回
    When 前端在技能编辑弹窗选择 zip
    Then 自动触发解压并回填名称/描述/使用说明，文件清单并入列表
    When 压缩包含 ../ 路径穿越条目
    Then 400 路径不合法
    When 压缩包内嵌套 .zip
    Then 400 不允许嵌套 zip
    When 对非 zip 文件调用解压
    Then 400 仅支持 zip
    When 对不存在的文件 id 调用解压
    Then 404
    When 条目数超过 100 或解压总大小超过 50MB
    Then 400 超出上限

  @SKM-S12 @auto:e2e
  Scenario: 技能头像
    Given 登录用户经存储直传管线上传图片完成登记
    When 创建技能携带 avatar objectKey
    Then 200，my_list 列表项与详情返回 avatarPath
    When 调用 /skill/{id}/avatar 更换头像
    Then 200，详情反映新头像
    When 提交未登记的伪造 objectKey
    Then 404 头像文件不存在或未完成上传
```
