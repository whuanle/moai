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
