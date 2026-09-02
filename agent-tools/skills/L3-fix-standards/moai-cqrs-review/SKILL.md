---
name: moai-cqrs-review
description: CQRS iron-law review checklist and fix standard for MoAI code. Use when reviewing code, fixing bugs, or after L2 code changes to verify compliance. Trigger words include "review 代码/按规范修 bug/检查合规". 仅限 MoAI 项目。Use only for the MoAI project.
---

# MoAI CQRS 铁律审查与修复标准（L3）

## PROJECT SCOPE

只服务 MoAI 项目。对 L2 产出做合规审查，或修 bug 时按标准流程走。

## WHEN

- "review 代码"、"检查合规"、"按规范修 bug"
- L2 skill 执行完成后的复核
- Obsidian 99-问题台账登记新缺陷时

## WHAT

按铁律清单逐项核对，修复遵循"证据 → 根因 → 修复 → 文档 → 回归"五步，全程可追溯。

## HOW

### 审查清单（逐项 ✅/❌）

**Shared 层**
- [ ] Command/Query 继承 `IModelValidator<T>` 且 Validate 只校请求体字段（路由回填字段校验 = 恒 400）
- [ ] IUserIdContext 只在真正需要用户上下文的命令上
- [ ] 公开成员中文 XML 注释齐全

**Core 层**
- [ ] Handler 无 IUserContextProvider/UserContext 注入
- [ ] BusinessException 全部带 StatusCode（400/403/404/409）
- [ ] 查询过滤 `IsDeleted == 0`；未手动赋值审计属性
- [ ] 写用户数据后 `RemoveUserStateAsync`
- [ ] 目标保护基于 DB 事实（root = setting.key="root"）

**Api 层**
- [ ] Controller 无业务逻辑；角色门禁 EnsureAdmin/EnsureRoot 在 Controller
- [ ] 路由参数显式回填 + SetUserContext

**前端**
- [ ] 管理页裸 `<Page>`（无 title/subtitle）；无 maxWidth 居中
- [ ] 工具栏从左排列（非 space-between）；表单 Modal `maskClosable={false}`
- [ ] 图标操作列带 aria-label；危险操作红色 Popconfirm；`<DataTable sticky>`
- [ ] 无硬编码颜色（token/antd token）；i18n 双语同步
- [ ] 测试按 aria-label 断言图标按钮

### 修复五步标准

1. **证据**：复现 + 截图/接口响应/日志（不许凭感觉修）
2. **根因**：定位到具体层与文件；对照本文档清单确认违反哪条铁律
3. **修复**：最小改动，不扩大接口范围、不做无关重构
4. **文档**：行为变化 → 更新对应模块 sdd/bdd；踩新坑 → Obsidian `99-问题台账` + 视情况补进 L2 skill 反例
5. **回归**：`dotnet build` + ui 三件套 + 相关 e2e 全绿；前端改动浏览器实机走查

## REFERENCE

- 已知缺陷账本：Obsidian `99-问题台账与决策清单`（P1~P11）
- 修复史范本：`docs/user-management/sop.md` 历史验收存档（三轮 12 缺陷修复记录）

## LIMITS

- 不承载代码细则（L2 两册）；不做功能编排（L1 moai-feature）
- 审查只对 MoAI 仓库代码；Obsidian 镜像文档不直接改（以仓库 docs 为真源，重新同步）
