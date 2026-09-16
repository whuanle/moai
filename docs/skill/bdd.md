# 技能（Skill）模块行为规格（BDD）

> 关联：[SDD](./sdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md)

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
    When 保存应用配置 skills 含已禁用/不存在的技能 id
    Then 400 包含不存在或已禁用的技能，请重新选择
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

## 用户级应用配置（2026-09-16 增量）

```gherkin
Feature: 用户级应用配置（跨会话复用）
  Background:
    Given 团队拥有应用 App，成员 Charlie 已登录

  @SKL-S5 @auto:e2e
  Scenario: 保存并回显
    When Charlie 保存用户配置（自选技能列表）
    Then 保存成功，查询回显 skills 与保存值一致、promptId=0、lockedSkills 为空

  @SKL-S6 @auto:e2e
  Scenario: 保存校验
    When Charlie 保存含他人个人技能的自选列表
    Then 400 存在不可用或无权使用的技能
    When Charlie 保存不可用的专家提示词 id
    Then 404 提示词不存在

  @SKL-S7 @auto:e2e
  Scenario: 覆盖语义与非成员
    When Charlie 再次保存（skills 为空列表）
    Then 覆盖先前自选技能，查询 skills 为空
    When 非团队成员查询该应用用户配置
    Then 404

  @SKL-S8 @manual
  Scenario: 运行时技能并集生效
    Given 应用绑定技能 A（应用所有者锁定），Charlie 自选技能 B
    When Charlie 发起对话并触发 list_tools
    Then 工具列表同时含 skill_A 与 skill_B
    And 自选技能被删除/禁用后，后续对话静默剔除且不影响技能 A

  @SKL-S9 @auto:vitest
  Scenario: 应用绑定技能对用户锁定
    When Charlie 打开对话页「应用设置」面板
    Then 应用绑定技能呈现勾选且禁用（应用必选标记），不可取消
    And 保存仅提交自选技能与新会话默认专家

  @SKL-S10 @auto:vitest
  Scenario: 应用配置页绑定技能
    When 团队管理员在应用配置页选择技能并保存
    Then 绑定写入 app_agent_config.skills，选项来自系统内置∪公开∪本团队（不含个人技能）
```
