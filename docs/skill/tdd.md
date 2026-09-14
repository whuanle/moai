# 技能（Skill）模块测试规格（TDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [SOP](./sop.md)

## 单元测试（`tests/MoAI.AI.Core.Tests/SkillAppToolProviderTests.cs`，27/27 通过）

| 用例 | 断言 |
|---|---|
| GetTools_NoSkillBound_ReturnsEmpty | 未挂载技能返回空，且不查技能服务 |
| GetTools_SkillMissingOrDisabled_ReturnsEmpty | 服务返回空列表→空工具 |
| GetTools_WithSkill_RegistersSkillTool | `skill_{key}` 注册，Kind=skill，描述含技能描述 |
| Invoke_SandboxDisabled_ReturnsInstructionsOnly | 返回 instructions + `sandboxEnabled=false`，**零沙箱写入** |
| Invoke_SandboxEnabled_WritesFilesAndReturnsInstructions | 文件按 `/workspace/skills/{key}/{path}` 写入一次，内容来自 `ISkillService.ReadSkillFileAsync` |

同批扩展：`SandboxAppToolProviderTests` 增加 `sandbox_save_artifact` 在启用沙箱时的工具清单断言；构造函数注入 `IStorageService`。

## 集成验证记录（2026-09-12，本地 + 154）

- `dotnet build MoAI.sln` 0 错误；`dotnet test tests/MoAI.AI.Core.Tests` 27/27。
- 数据库删库重建：`DROP SCHEMA public CASCADE` → EnsureCreated **38 表**；种子 admin/classify 99/setting/skill 2 条（固定 Guid）；`team_variable.id` identity 正常（先删除上游 re-scaffold 带回的 `ValueGeneratedNever()`）；`uuid-ossp`/`vector` 扩展由 `HasPostgresExtension` 自动创建。
- 本地接口冒烟：login 200 → `GET /api/skill/options` 200（2 内置）→ `GET /api/skill/list` 200（fileCount=1，内嵌清单生效）。
- **沙箱 spike（OpenSandbox 本地 55900 + code-interpreter:v1.1.0）**：
  - 服务端 `uvx opensandbox-server`（~/.sandbox.toml，api_key=moai-sandbox-dev，bridge + host_ip=127.0.0.1）；
  - 镜像 cpython-3.14.5 **未预装** python-docx/python-pptx；shell `pip install` 被 PEP 668 拦截；
  - 技能脚本 `_ensure` 以 `pip install --break-system-packages` 兜底**成功**（出站网络默认开放）；
  - 端到端：写脚本入沙箱 → 生成 `output.docx`(36833B)/`output.pptx`(30092B) → `read_bytes` 回读 → ZIP magic PK → 本机 `unzip -l`/`textutil` 校验内容正确（标题/章节/段落/加粗、封面+要点页）。
- 154 验证：serverinfo/login 200；skill/options·list 200；插件执行 `static_current_time` 返回时间、`static_text_extract` 参数校验生效、`static_markdown_to_html` 成功。

## 前端

- `tsc --noEmit` 0 错误（含 Kiota long→string 收敛修正）。
- `/skills` 管理页：QueryBar 搜索、分页、图标操作列（详情/编辑/删除 Popconfirm，fixed right+sticky）、启停 Switch、新建/编辑（key 创建后禁改）、包文件上传（preupload→PUT→complete）、详情弹窗 instructions 预览。
- `AppManage`：技能多选挂载（选项来自 `GET /api/skill/options`），保存走 agent-config 整体替换。

## 已知限制

- 154（1.9G 内存）不部署 OpenSandbox，沙箱执行类技能在该环境不可用（管理/挂载/说明降级可用）——见 SOP。
- 自定义技能包文件仅文本（写入沙箱走文本通道）；二进制资源待后续按需扩展。
- 内置技能 `createTime` 种子固定 2026-01-01（HasData 静态值）。
