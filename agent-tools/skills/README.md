# MoAI 项目 Skills 分层总入口

> 唯一真源（Obsidian `MoAI/SOP-端到端场景/98-框架代码开发Skill规范` 为镜像）。新增 skill 必须先读本文件。
> 方法论沿用 SMS-System 的五层职责分层：层号即调用顺序粗粒度参考，跨层只能上层调下层，禁止反向。

## 分层总览

```
agent-tools/skills/
├── README.md                                  ← 本文件：架构图 + 标准调用链 + 新增规则
├── L1-orchestration/                          编排调度层（1）
│   └── moai-feature/                          全栈新功能总入口：后端→syncapi→前端→验证 调度
├── L2-code-standards/                         代码规范执行层（2）
│   ├── moai-cqrs-backend/                     后端 CQRS 三层细则（Shared/Core/Api）
│   └── moai-frontend-ui/                      前端页面细则（design-system/Kiota/i18n）
└── L3-fix-standards/                          修复与审查标准层（1）
    └── moai-cqrs-review/                      CQRS 铁律审查清单（修 bug / review 用）
```

## 标准调用链

```
用户："做 XX 功能 / 新增接口 / 加个管理页"
  → L1 moai-feature（编排）
      → 后端部分 → L2 moai-cqrs-backend（三层骨架 + 铁律）
      → 前端部分 → L2 moai-frontend-ui（syncapi + design-system + i18n）
      → 验证（build + 三件套 + e2e）发现问题
          → L3 moai-cqrs-review（按铁律清单定位违规 → 修复 → 复验）
      → 回填 Obsidian 99-问题台账（如踩新坑）
```

## 新增 skill 规则

1. 先判断职责归层：调度入口 → L1；代码规范执行 → L2；修复/审查标准 → L3；生产运维 → L4（暂无）；通用无关 → L5（暂无）。
2. 新 skill 必须同时在 `skills/README.md`（本文件）与 `AGENTS.md` 对应层登记，并在 Obsidian `98-框架代码开发Skill规范` 镜像表登记。
3. 层内 skill 互不依赖；跨层只能上层调下层，禁止反向。
4. skill 内引用其他 skill 一律用分层后完整路径（如 `L2-code-standards/moai-cqrs-backend`）。
5. SKILL.md 格式：frontmatter（`moai-<域>-<动作>` + description 含"仅限 MoAI 项目 / Use only for the MoAI project"）+ 六段（PROJECT SCOPE / WHEN / WHAT / HOW / REFERENCE / LIMITS）。
6. 权威规范唯一真源在 `docs/`：后端 `docs/cqrs-conventions.md`，前端 `ui/docs/frontend-conventions.md` + `ui/docs/design-system/`。skill 只写浓缩铁律与实踩坑，用 REQUIRED REFERENCE 指向真源，**不要复制规范全文**。
