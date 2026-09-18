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

## 增量验证映射（2026-09-16：归属三级权限 + 用户级应用配置）

| 场景 | 验证物 | 结果（日期） |
|---|---|---|
| @SKL-S1~S4 | local-dev/skill-userconfig-e2e.mjs（SK-01~12） | PASS 20/20（2026-09-16） |
| @SKL-S5~S7 | local-dev/skill-userconfig-e2e.mjs（UC-01~08） | PASS 20/20（2026-09-16） |
| @SKL-S8 | @manual（需沙箱+模型，浏览器走查，见 sop.md 第 5 节） | 待走查 |
| @SKL-S9 | ui/src/pages/teams/apps/__tests__/AppUserSettings.test.tsx | PASS 3/3（2026-09-16） |
| @SKL-S10 | ui/src/pages/teams/apps/__tests__/AppConfigSection.test.tsx | PASS 7/7（2026-09-16） |

同批回归：`dotnet build src/MoAI/MoAI.csproj` 0 error；前端 vitest **289/289**、typecheck 0、lint 0 error（10 个存量 warning）；存量 `publication-e2e` 34/34、`prompt-e2e` 46/46（PublicationReviewEntity.ReviewTime 修复后回归）。

实踩坑（2026-09-16）：
- **路由回填字段不得进 Validate**：`UpdateSkillCommand.SkillId` / `SaveAppUserConfigCommand.AppId` 的 NotEmpty 规则使 PUT `/api/skill/{id}`、`/api/app/{id}/userconfig` 必 400（SharpGrip 自动校验发生在 Controller 路由回填之前，与 rounds-log #81 promptId 同坑）。修复：命令 Validate 只校验请求体字段。
- **PublicationReviewEntity.ReviewTime 类型错误**：实体为 `DateTime?` 但 DB 列 timestamptz、DTO/Handler 均用 `DateTimeOffset?`，赋值 `DateTimeOffset.Now` 直接 CS0029 编译失败；修复为 `DateTimeOffset?`（对齐 cqrs-conventions 时间约定）。

## 增量验证映射（2026-09-17：技能市场 + 个人维护 + 下载）

| 场景 | 验证物 | 结果（日期） |
|---|---|---|
| @SKM-S1~S2 | local-dev/skill-market-e2e.mjs（SM-01~03、20~22） | PASS 28/28（2026-09-17） |
| @SKM-S3~S4 | local-dev/skill-market-e2e.mjs（SM-04~07、18~19；SM-06 断言含 response-content-disposition） | PASS 28/28（2026-09-17，附件头修复后回归） |
| @SKM-S5 | local-dev/skill-market-e2e.mjs（SM-08~16） | PASS 28/28（2026-09-17） |
| @SKM-S6~S7 | local-dev/skill-market-e2e.mjs（SM-17、23~25） | PASS 28/28（2026-09-17） |
| @SKM-S8 | local-dev/skill-market-e2e.mjs（SM-26~28，无种子库自动 SKIP） | PASS（2026-09-17，本地库无 SkillSeed 种子） |
| @SKM-S9 | local-dev/skill-market-e2e.mjs（SM-29~31） | PASS 28/28（2026-09-17） |

同批回归：`dotnet build src/MoAI/MoAI.csproj` 0 error；前端 typecheck 0、lint 0 error（存量 warning）、vitest **307/307**；存量 `skill-userconfig-e2e` 20/20、`publication-e2e` 34/34、`prompt-e2e` 46/46、`app-e2e` 113/113、`wiki-e2e` 32/32（预签名 URL 附件头修复后回归）。

实现落点（2026-09-17）：
- 审批链路：`PublicationResourceType.Skill=2`；`Apply/Review/Withdraw` Handler 各加 skill 分支（个人技能按归属人、团队技能按 Admin+；内置技能拒绝申请）。
- 查询：`GET /api/skill/my_list|team_list|market_list`（`QuerySkillListHelper` 统一映射待审 id + 用户名填充）；`GET /api/skill/{id}` 详情改可见性校验（`SkillAccessGuard.EnsureCanViewAsync`）；`GET /api/skill/{id}/download` 返回逐文件预签名地址；`GET /api/skill/list` 仍仅管理员。
- 删除联动：`DeleteSkillCommandHandler` 同步移除待审核上架申请（对齐 DeletePromptCommandHandler）。
- 下载附件化：`S3Client.GeneratePreSignedDownloadUrlAsync` 支持 fileName→`ResponseHeaderOverrides.ContentDisposition`（`filename*=UTF-8''` 编码），文本类文件不再被浏览器内联渲染；wiki 文档下载与沙箱产物链接同链路受益。
- 前端：`/skill-market`+`/skills` 双 Tab 技能中心（卡片流，参考提示词中心）；团队详情新增「团队技能」分区（TeamSkills）；审批上架页类型筛选加「技能」。

## 增量验证映射（2026-09-18：技能分类）

| 场景 | 验证物 | 结果（日期） |
|---|---|---|
| @SKM-S10（前端 chip 过滤） | ui/src/pages/skills/__tests__/Skills.test.tsx | PASS 2/2（2026-09-18，全仓 vitest 346/346） |
| @SKM-S10（后端校验/列表返回） | @manual（`dotnet build` 0 error；后端重启后建议补 skill-market-e2e 分类用例） | 待回归（2026-09-18） |

实现落点（2026-09-18）：
- 后端：`ClassifyTypes.Skill="skill"`；`SkillEntity.ClassifyId`（skill.classify_id，DBA 已补列）；Create/Update 校验分类存在且 Type=skill（404「技能分类不存在」）；四个列表 + 详情返回 `classifyId`，列表支持 `ClassifyId` 过滤（`QuerySkillListHelper.WhereClassify`）；`MoAI.Skill.Core` 增加对 `MoAI.Classify.Shared` 的项目引用。
- 前端：Kiota 客户端重新生成；`ClassifyType.Skill`；分类管理页新增「技能」页签；技能中心两 Tab 头部固定分类 chip（emoji）；`SkillEditModal` 分类下拉；`api/skills.ts` 封装扩展。
