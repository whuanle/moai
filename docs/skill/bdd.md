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
