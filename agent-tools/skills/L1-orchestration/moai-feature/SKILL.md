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
3. **API 同步**：先起后端，再生成客户端。**必须带 `CODEBUDDY_SAFE_DELETE_ENABLED=0`**——`npm run syncapi` 会 `rmSync(ui/src/api/client)` 重建，否则被 safe-delete 拦截而中止：
   ```bash
   cd src/MoAI && ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://localhost:5000 \
     dotnet run --project MoAI.csproj --no-build --no-restore   # openapi: /openapi/v1.json
   cd ui && CODEBUDDY_SAFE_DELETE_ENABLED=0 npm run syncapi
   ```
   用 `dotnet run --project ...`，别用 `dotnet MoAI.dll`（dll 在 `bin/Debug/net10.0/`，且 content root 不对会读不到 `appsettings.Development.json`）。
   端口不同则给 syncapi 显式传参；后端实在起不来可用 `src/MoAI/MoAI.json` 离线生成。细节见 `docs/api_interface.md`。
4. **前端** → 按 `L2-code-standards/moai-frontend-ui` 执行（api 封装 + 页面 + i18n + 测试）。
5. **验证**（全绿才算完成）：
   ```bash
   dotnet build src/MoAI/MoAI.csproj --no-restore    # 0 error
   cd ui && npm run typecheck && npm run lint && npm run test
   # e2e 按模块选：node local-dev/app-e2e.mjs 等（需后端运行中）
   ```
   ⚠️ **NuGet 还原失败时不要急着放弃**：本机若报 `NuGet ... Value cannot be null. (Parameter 'path1')`
   （根因 NuGet `ConfigurationDefaults` 静态构造取不到系统目录），`dotnet restore` 与不带 `--no-restore` 的 build 会挂，
   但 **`dotnet build <proj> --no-restore` 在已还原过的项目上可用**：2026-09 实测 `src/MoAI` 宿主、`src/app`、`src/database`
   都能 0 error 编译并 `dotnet run` 起服务（能起才能跑 syncapi 与 E2E）。
   所以顺序是「先按 `--no-restore` + `dotnet run` 试」→ 真编不过才如实标注「后端 build / E2E 待系统终端复验」并把命令清单交给用户。
   不要伪造 `obj/project.assets.json`。另注意：对**未改动**项目 `--no-restore` 会**增量跳过**而显示 0 error，别当成编译通过。
   ⚠️ 也别用 `-t:Rebuild` 兜底：它会先 `Clean` 掉 `obj/project.assets.cache`，逼 `ResolvePackageAssets` 重跑 → NuGet 必挂。
   取真实编译证据的可行做法（2026-09-11 实测）：`dotnet build <proj> --no-restore -p:GenerateDependencyFile=false`
   会连带把依赖项目一起过 Csc，既拿 0 error 也能看到真实告警；改过的文件先 `touch` 再编，避免被增量跳过。
   ⚠️ `-t:CoreCompile` **单独执行不可信**——不解析引用，会误报成片 `CS0400/CS0246`（连 `System`/`Maomi` 都「找不到」），
   别按它的输出判断有没有改坏。<br>
   ⚠️ 另：`obj/**` 下的中间产物别删（如 `obj/Debug/net10.0/<X>.dll`），会毁掉 obj 状态并让后续编译出现假错误。
6. **功能依赖外部 HTTP 服务时的验证**（自带「桩服务」跑真实后端，无需真实 Key / 不消耗额度）：
   1) 先让上游地址可配置（如博查 `MoAI:BoCha:Endpoint`，默认官方地址），否则 `BaseAddress` 硬编码、无法指向桩；
   2) 写一个 node 脚本自起桩服务回放厂商文档样例报文，再 `spawn('dotnet', ['run','--project','src/MoAI/MoAI.csproj','--no-build','--no-restore'])`
      拉起独立后端（env 传 `MoAI__Port` 换端口、`MoAI__BoCha__Endpoint` 指向桩），`taskkill /pid <pid> /T /F` 收尾；
   3) 断言要覆盖「参数怎么下去 + 结果怎么解析上来」两侧。范例：`local-dev/bocha-search-e2e.mjs`（22 断言）。
   注意沙箱**禁止用编译器直接编译任意 C# 代码**，临时「桩客户端单测」走不通，只能用这种方式。
   另注意本机 shell 的 `http_proxy/https_proxy`：`curl` 探活要加 `--noproxy '*'`；.NET 请求行会变 absolute-form，
   桩服务必须 `new URL(req.url, 'http://x').pathname` 再匹配路径。
7. **审查**：改动多时按 `L3-fix-standards/moai-cqrs-review` 清单过一遍。
8. **回填**：踩到新坑 → 登记 Obsidian `99-问题台账`；沉淀新流程 → 按 `skills/README.md` 新增规则落新 skill。

## REFERENCE

正例：user-management 全链路（后端 34 e2e 场景 + 前端 /users 页 + 3 组件测试）。

## LIMITS

- 不承载代码细则——后端看 `L2-code-standards/moai-cqrs-backend`，前端看 `L2-code-standards/moai-frontend-ui`。
- 纯 bug 修复/review 直接进 `L3-fix-standards/moai-cqrs-review`，不必走本编排。
- 不做数据库迁移、部署（超出当前分层体系范围时先问）。
