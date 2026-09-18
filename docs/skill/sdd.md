# 技能（Skill）模块设计规格（SDD）

> 关联：[BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 上游：[../ai/sdd.md](../ai/sdd.md)（Agent 运行时/沙箱/工具来源） ｜ [../app/sdd.md](../app/sdd.md)（应用配置） ｜ [../storage-file-layout.md](../storage-file-layout.md)（对象存储）

- 日期：2026-09-12
- 状态：全链路已实现（管理/挂载/沙箱加载/内置 docx·ppt 生成技能），`dotnet build` 0 错误、`MoAI.AI.Core.Tests` 27 通过；154 部署验证通过（沙箱执行因服务器内存不足暂缓，见 SOP）
- 领域：`src/skill`（MoAI.Skill.Shared / Core / Api）+ `src/ai/MoAI.AI.Core/Tools/SkillAppToolProvider.cs` + `SandboxAppToolProvider`（save_artifact）
- 前端：`ui/src/pages/skills/Skills.tsx`（管理页）、`ui/src/pages/teams/apps/AppManage.tsx`（挂载）

## 1. 目标

把"能力"做成**可装载的技能包**：系统内置/管理员上传的技能（使用说明 + 脚本/资源文件），应用像挂插件一样挂技能；Agent 对话中按需"加载"技能——脚本文件写入会话沙箱、使用说明注入对话——随后用沙箱工具执行脚本完成任务（如生成 docx/pptx），产物经 `sandbox_save_artifact` 回传 MinIO 给用户下载链接。

与插件的分工：**插件**（`src/aiplugin`）是平台侧预置/注册的函数能力；**技能**是面向 Agent 的"说明书+脚本包"，内容在运行期装载，不需要写 C# 代码。

## 2. 架构

```
ui /skills 管理页 ──CQRS──► src/skill（CRUD/启停/包文件上传→MinIO）
ui AppManage 挂载   ──────► app_agent_config.skills(JSON uuid[])
                                   │
Agent 运行时(src/ai)                 ▼
  SkillAppToolProvider(Order=14) ◄─ ISkillService.GetRuntimeSkillsAsync
    每技能注册 AppTool：skill_{key}（Kind="skill"）
    Invoke = 加载：技能文件→IAppSandboxService.WriteFile(/workspace/skills/{key}/)
                  Instructions 作为 tool result 返回
  Agent ──sandbox_run_code──► 执行技能脚本(python-docx/python-pptx)
       ──sandbox_save_artifact──► ReadFileBytesAsync→MinIO→下载链接
```

- 渐进披露不变：技能工具经 `list_tools`/`call_tool` 元工具暴露（见 `../ai/sdd.md` D12/D13）。
- 沙箱未启用时降级：仅返回使用说明并提示脚本无法执行。

## 3. 数据与存储

- **`skill` 表**（新）：`id` uuid（默认 `uuid_generate_v4()`，Guid 主键避开 identity 陷阱）；`key` 蛇形唯一（partial 索引 `is_deleted=0`，不可变更）；`name`/`description`/`instructions`（markdown）；`files` JSON `[{path,fileId,fileName}]`（同 app 模块 JSON 承载先例）；`is_system`/`team_id(0=系统)`/`is_disable`；审计字段齐全，软删除 **long ticks**（全站既有约定）。
- **`app_agent_config.skills` 列**（新）：JSON uuid 数组，同 `wiki_ids`/`plugins` 先例；保存时校验"存在且启用"，null 表示不覆盖（兼容旧前端）。
- **MinIO**：自定义技能包文件走 `PreUploadAsync/CompleteAsync`（objectKey 前缀 `skill/`，白名单 `FileStoreHelper.SkillPackageFormats`：py/md/json/txt/csv/yaml/j2/html/css/js）；内置技能**不占存储**，脚本以 `MoAI.Skill.Core/Resources/skills/{key}/` 内嵌资源分发（`BuiltinSkills` 清单映射资源名）。
- **产物**：`appagent/{sessionId:N}/{sha256}.{ext}` 前缀，`GetDownloadUrlAsync` 预签名 1 小时。

## 4. 组件

| 组件 | 职责 |
|---|---|
| `SkillController`（`/skill`） | 管理端点（list/{id}/create/update/delete/disable/file preupload+complete，IsAdmin 门禁）；`GET options` 登录用户可用（应用配置页挂载选择） |
| `SkillService : ISkillService` | 运行时加载：按 id 集合取启用技能→`SkillRuntimeInfo`；`ReadSkillFileAsync` 按来源（内嵌资源 / MinIO）读文本 |
| `BuiltinSkills` | 内置技能清单与资源名约定（`MoAI.Skill.Resources.skills.{key}.{path}`） |
| `SkillAppToolProvider : IAppToolProvider`（Order=14） | 挂载技能→`skill_{key}` 工具；加载=写沙箱+返回 instructions |
| `SandboxAppToolProvider.sandbox_save_artifact` | 沙箱产物字节→SHA256→MinIO→file 记录→下载链接（新增 `IAppSandboxService.ReadFileBytesAsync` 二进制通道，SDK `ReadBytesAsync`） |
| 内置技能 `docx_writer` / `ppt_writer` | 说明见 `SkillSeed`；脚本 `generate_docx.py`/`generate_pptx.py`（python-docx/python-pptx，`_ensure` 缺库自动 `pip install --break-system-packages`） |

## 5. 关键决策

- **D1 Guid 主键**：新表用 uuid 默认值而非 long identity，规避 PostgresScaffold `ValueGeneratedNever` 陷阱（99-问题台账 P 系列）。
- **D2 内置技能双形态**：元数据 HasData 种子 + 脚本内嵌资源；删除保护（`IsSystem` 不可删只可禁用），种子不依赖 MinIO 初始状态。
- **D3 Files 走 JSON 列**而非关联表：沿用 `app_agent_config` D10/D20"整体替换式保存"先例。
- **D4 生成引擎=沙箱 Python**（用户拍板）：镜像未预装 python-docx/pptx（spike 证实），技能脚本自带 pip 兜底（PEP 668 需 `--break-system-packages`，一次性沙箱可接受）；备选三级降级为自建镜像，暂不需要。
- **D5 产物通道二进制化**：`ReadFileAsync`（文本）不足以承载 docx/pptx，新增 `ReadFileBytesAsync`。
- **D6 key 规则**：`^[a-z][a-z0-9_]{0,29}$`，与动态插件实例 key 同风格；工具名 `skill_{key}` 应用内唯一。

## 6. 增量设计（2026-09-16：归属三级权限 + 用户级应用配置）

- **归属模型**：`skill.team_id` 语义扩展——`is_system=1` 平台内置；`team_id>0` 团队技能；`team_id=0 且非内置` 个人技能（归属 `create_user_id`）。判定与提示词模块完全一致。新增 `is_public` 列（bool，市场上架审批通过置 true，本期仅存储与 options 过滤，审批流接 `PublicationResourceType.Skill` 为后续增量）。
- **权限矩阵**：个人技能=归属人；团队技能=团队 Admin/Owner；平台管理员全通（`SkillAccessGuard`，Handler 目标保护）；`list`/`{id}` 详情仍 Controller IsAdmin 门禁；options/preupload/complete 登录即可。
- **D7 用户级应用配置表 `app_user_config`**：(app_id, user_id) 唯一（partial 索引 is_deleted=0），字段 `prompt_id`（新会话默认专家，0=未设置）+ `skills` JSON uuid 数组（用户自选）。接口 `GET/PUT /api/app/{id}/userconfig`。**专家语义=新会话默认值**：前端进入对话页加载用户配置初始化专家选择，会话级专家面板（app 模块 @AP-S44/S45）仍可单独覆盖且优先；技能语义=运行时并集。
- **D8 运行时并集与失效剔除**：`AppAgentFactory` 每次装配时查 `app_user_config`（调试会话跳过，保持应用默认视角），`生效技能 = config.Skills（锁定，不做可见性过滤）∪ FilterVisibleSkillIdsAsync(userConfig.Skills)`（系统内置∪公开∪本团队∪本人个人，且未禁用；失效项静默剔除）。会话不快照技能，用户配置变更即时对后续请求生效。
- **D9 市场直接引用**：用户自选直接引用技能 id（可见性运行时兜底），不做"安装副本"；sha256 内容寻址下后续如需"下架保护"可加副本引用，成本为零。
- 组件增量：`SkillAccessGuard`（权限断言）、`ISkillService.FilterVisibleSkillIdsAsync`、`Save/QueryAppUserConfigCommandHandler`（app 模块，校验复用 `SessionPromptHelper` 与 `FilterVisibleSkillIdsAsync`）、前端 `chat/AppUserSettings.tsx`（对话页应用设置面板）、`AppConfigSection` 技能绑定多选。
- 前端约定：应用设置面板关闭时不渲染（避免与专家面板重复挂载同名列表项，vitest 踩坑）；i18n `appChat.userSettings*`/`appManage.sectionSkills*` zh-CN 与 en-US 同步。

## 7. 增量设计（2026-09-17：技能市场 + 个人维护 + 下载）

- **对齐提示词模块**：`PublicationResourceType.Skill=2`，上架审批复用 publication 模块（apply/review/withdraw 各加 skill 分支）；个人技能归属人申请、团队技能 Admin+ 申请、内置技能拒绝申请；审批通过置 `skill.is_public=true`。
- **列表三入口**：`my_list`（本人个人技能）/`team_list`（团队成员）/`market_list`（is_public 全员）+ 保留 `list`（管理员全量分页）。列表项扩展 `teamId/isPublic/pendingPublicationId` 并继承 `AuditsInfo` 填充人名（`QuerySkillListHelper`）。
- **详情/下载可见性**：`SkillAccessGuard.EnsureCanViewAsync`——系统内置 ∪ 公开 ∪ 本团队 ∪ 本人 ∪ 平台管理员；下载端点 `{id}/download` 返回逐文件 MinIO 预签名地址（1 小时），内置技能 400（脚本随平台分发无对象存储文件）。
- **删除联动**：删技能同步删待审核上架申请（对齐提示词，防僵尸审核记录）。
- **前端**：`/skill-market`（市场）+`/skills`（我的技能）双 Tab 技能中心，卡片流对齐提示词中心（xl/xxl 一行 6 张，不展示条数统计，头部固定分类 chip 按 `classifyId` 过滤、`classifyLabel` 展示 emoji）；团队详情新增「团队技能」分区（TeamSkills，Admin+ 可管理）；创建/编辑收敛为共用 `SkillEditModal`（teamId 由入口决定，分类下拉同款 classifyLabel）；详情弹窗 Markdown 渲染 instructions + 逐文件下载；审批上架页类型筛选加「技能」。

## 8. 增量设计（2026-09-18：技能分类）

- **数据**：`skill.classify_id`（int，0=未分类；实体 `SkillEntity.ClassifyId` + `SkillConfiguration` 映射，存量库已由 DBA 手工补列）。分类类型常量 `ClassifyTypes.Skill="skill"`（分类管理按类型隔离，emoji 复用 classify 模块）。
- **命令**：`CreateSkillCommand/UpdateSkillCommand` 增加 `ClassifyId`（≥0）；Handler 校验 `ClassifyId>0` 时分类必须存在且 `Type=skill`，否则 404「技能分类不存在」。
- **查询**：`my_list/team_list/market_list/list` 四个列表与详情响应均携带 `classifyId`；列表支持 `classifyId` 过滤（`QuerySkillListHelper.WhereClassify`）。
- **前端**：技能中心两 Tab 头部固定分类 chip（CheckableTag + `classifyLabel` 展示 emoji），点击按分类过滤并回第一页；`SkillEditModal` 分类下拉；分类管理页新增「技能」页签。
