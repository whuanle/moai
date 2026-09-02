---
name: moai-feature
description: Orchestrate a full-stack feature in the MoAI project (backend CQRS module + frontend page + i18n + verification). Use when the user asks to add a feature module, API endpoint, or management page, or says "做XX功能/新增接口/加个管理页". 仅限 MoAI 项目。Use only for the MoAI project.
---

# MoAI 全栈新功能编排（L1）

## PROJECT SCOPE

只服务 MoAI 项目（`/Users/wen/project/maomi/moai`）。本 skill 是**调度入口**，不承载代码细则——细则在 L2 两个分层 skill，铁律审查在 L3。

## WHEN

- "做 XX 功能"、"前后端一起做 XX"、"新增一个接口 + 页面"
- "给管理后台加个 XX 模块"

## WHAT

把一个功能需求拆成 后端 → API 同步 → 前端 → 验证 四步，调度对应分层 skill 执行，最终交付全绿。

## HOW

1. **澄清需求**：资源名、字段、谁有权操作（admin/root/所有人）、是否需要管理页。
2. **后端** → 按 `L2-code-standards/moai-cqrs-backend` 执行（Shared/Core/Api 三层）。
3. **API 同步**：后端起在 `:5210`（`MAI_FILE=/Users/wen/project/maomi/local-dev/system.local.json ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/MoAI`），然后 `cd ui && npm run syncapi http://127.0.0.1:5210/openapi/v1.json`。
4. **前端** → 按 `L2-code-standards/moai-frontend-ui` 执行（api 封装 + 页面 + i18n + 测试）。
5. **验证**（全绿才算完成）：
   ```bash
   dotnet build src/MoAI/MoAI.csproj
   cd ui && npm run typecheck && npm run lint && npm run test
   # e2e 按模块选：node local-dev/user-management-e2e.mjs 等（需后端 :5210）
   ```
6. **审查**：改动多时按 `L3-fix-standards/moai-cqrs-review` 清单过一遍。
7. **回填**：踩到新坑 → 登记 Obsidian `99-问题台账`；沉淀新流程 → 按 `skills/README.md` 新增规则落新 skill。

## REFERENCE

正例：user-management 全链路（后端 34 e2e 场景 + 前端 /users 页 + 3 组件测试）。

## LIMITS

- 不承载代码细则——后端看 `L2-code-standards/moai-cqrs-backend`，前端看 `L2-code-standards/moai-frontend-ui`。
- 纯 bug 修复/review 直接进 `L3-fix-standards/moai-cqrs-review`，不必走本编排。
- 不做数据库迁移、部署（超出当前分层体系范围时先问）。
