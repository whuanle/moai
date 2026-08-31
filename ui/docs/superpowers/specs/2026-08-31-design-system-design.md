# 设计系统（Design System）设计规格

- 日期：2026-08-31
- 项目：`moai-web`（`F:\workspace\moai\ui`）
- 状态：已确认设计，待撰写实施计划

## 目标与背景

`moai-web` 目前只有基础框架（React 19 + Vite 6 + TypeScript + Ant Design 5 + react-router 7 + zustand 5 + i18next），仅含 Dashboard / 登录 / 注册示例页。Header 导航规划了 chat / app / wiki / team / prompt / plugin 等业务模块。

本项目要先建立一套**设计系统**作为单一可信源：统一布局、组件、表格样式与各类样式系统，并提供模板、设计约束、页面约束、组件组合约束。目的是：后续 AI（或开发者）按这些资料做业务页面时，能保证全局设计一致、快速产出原型页面。

## 已确认决策

| 决策点 | 结论 |
|---|---|
| 交付形态 | 可复用代码 + AI 可读的规范文档（双形态） |
| 与 antd 关系 | 基于 antd 5 深化定制，外观接近默认风格，成本低、兼容好 |
| 页面覆盖类型 | 列表表格型、表单型、详情型、对话交互型、概览统计型 |
| 品牌基调 | 延续科技蓝主色 `#4A9EFF` |
| 主题体系 | 可扩展：支持多主题预设与切换（现 light/dark，预留扩展点） |
| 约束力度 | 强约束：有主见组件 + TS 类型强制，配合约束文档 |
| 架构方案 | 方案 A：`ui/src` 内的统一 `design-system` 模块 |
| 文档位置 | 前端文档统一放 `ui/docs/`，不进根目录 |

## 架构方案（方案 A）

设计系统作为单一可信源集中在一个命名空间下，组件只从此处导出，业务页面统一经公共入口引用。

```
ui/src/design-system/
├── theme/
│   ├── tokens.ts         # 设计令牌：色板/间距/圆角/字体/功能色
│   ├── presets.ts        # 主题预设注册表（light/dark，预留扩展）
│   ├── config.ts         # getThemeConfig() → antd ThemeConfig
│   └── css-vars.ts       # 输出 CSS 变量（非 antd 场景复用；可选）
├── components/
│   ├── Page/             # 页面容器 + 页头
│   ├── DataTable/        # 表格（分页/工具栏/列规范/loading/空态）
│   ├── FormPage/         # 表单页壳（布局/校验/提交）
│   ├── DetailPage/       # 详情页壳（描述列表/只读字段）
│   ├── QueryBar/         # 列表上方筛选查询区
│   ├── Card/             # 业务卡片（含 StatCard 概览统计卡）
│   └── Chat/             # 对话交互布局
├── templates/            # 可复制页面骨架（列表/表单/详情/概览/对话）
├── index.ts              # 公共出口（barrel）
└── README.md             # 设计系统概况与使用说明
```

迁移说明：
- 现有 `src/theme/` 逻辑收敛进 `design-system/theme/`；`providers/AppProviders.tsx` 改为从新入口取用。
- `store/app.ts` 的 `themeMode` 扩展为 `themeKey`（见主题系统一节）。
- 每类组件以「目录 + `index.ts` 导出」组织，便于独立测试与阅读。

## 主题系统

目标：单一品牌基线 + 可扩展的多主题预设，未来增主题/切换不动既有代码。

1. **设计令牌 `tokens.ts`**（单一常量来源，禁魔法值）
   - 色彩：品牌蓝主色 `#4A9EFF`、家族色阶、功能色（成功/警告/错误/信息）、中性灰阶、浅/深背景色。
   - 其他：圆角、间距刻度、字体族/字号、边框、阴影、组件高度。
   - 以对象导出，供 antd token 映射及文档引用。

2. **预设注册表 `presets.ts`**（主题扩展点）
   - 定义 `ThemePreset` 类型：`{ key, name, mode: 'light' | 'dark', overrides }`。
   - 内置 `light`、`dark`；未来新增主题只需追加一条记录。
   - 预设内部只做「相对基线覆盖」，基线统一。

3. **生成 antd 配置 `config.ts`**
   - `getThemeConfig(presetKey)` → 取预设 + tokens → 组装 antd `ThemeConfig`（algorithm + token + components 覆盖）。
   - 品牌色收敛、组件级覆盖（Layout header、Table/Form 细节）集中一处，不散落在页面。

4. **与 store 衔接**
   - `store/app.ts` 的 `themeMode` 扩展为 `themeKey`（`'light' | 'dark'`，预期未来加值）。
   - `ThemePreset` 统一暴露；`toggleTheme` 语义改为「切换明/暗预设」。

5. **可选 css-vars.ts**
   - 输出 CSS 变量，供非 antd 场景（自定义样式/图表色）复用，保持 token 单源。

## 共享组件层（强约束组件）

强约束 = 固定类型化 props + 统一内部行为，同时以 `...rest` 透传底层 antd 能力作逃生口。

| 组件 | 职责与强约束点 |
|---|---|
| `Page` | 页面容器：统一 padding、内容区宽度上限、标题/面包屑；约束页面顶部结构 |
| `DataTable` | 表格一揽子：统一 columns 规范、loading/空态、pagination、工具栏（刷新/导出）、行选择、合计；限定必须传 dataSource/columns |
| `QueryBar` | 列表上方筛选区：内部用 Form 布局，统一 label 宽度、按钮组、重置 |
| `FormPage` | 表单页壳：页面标题 + Form 布局/间距/校验入口、提交/取消按钮位置、onSubmit 契约 |
| `DetailPage` | 详情页壳：Descriptions 式只读展示、字段 label/value 规范、只读外观、加载态 |
| `Card` / `StatCard` | 概览统计卡：统一卡片属性、图标/数值/趋势区、hover |
| `Chat` | 对话交互布局：消息列表 + 输入区，统一气泡/间距/滚动容器 |

通用约束：
- 每个组件有类型化 props（`interface XxxProps`），必填项缺失时 TS 报错。
- 对外只保留语义化 props；底层 antd 通过 `...rest` 透传（TS 继承对应组件类型）。
- 内部统一处理 theme mode 感知（读 token）、i18n 走 `useTranslation`、loading/空态/错误态。
- 遵循 antd 现有使用习惯，不改基础 API 语义，只做约定层封装。

## 页面模板骨架

`design-system/templates/` 存放可直接复制改写的参考骨架，对应五种页面类型。每份骨架是完整可运行示例（占位数据 + i18n key + 设计系统组件的正确用法），是「快速原型」的核心。

| 模板 | 结构要点 |
|---|---|
| `ListTemplate` | Page + QueryBar → DataTable（分页/工具栏/行操作/筛选）→ mock 数据；演示列规范、状态 tag、操作列、分页 |
| `FormTemplate` | Page 标题 + FormPage（字段分组、校验、布局、提交/取消）；演示必填/联动 |
| `DetailTemplate` | Page + DetailPage（只读字段、状态、返回/编辑操作）；演示描述列表排版 |
| `DashboardTemplate` | Page 顶部 StatCard 区（Row/Col 栅格）+ 卡片可视化占位；演示概览统计 |
| `ChatTemplate` | Chat 布局：消息气泡区 + 输入框 + 发送；演示对话页交互 |

约定：
- 模板内不写死业务数据，用显式 mock 变量与 `utils` 分离，便于替换为 API 数据。
- 文案全部走 i18n key，避免硬编码中文。
- 模板同时是文档活例子：`pages.md` 引用模板文件名作对照。

## 规范约束文档

集中放 `ui/docs/design-system/`。

| 文档 | 内容 |
|---|---|
| `README.md`（AI 入口） | 用途说明 + 置顶「做页面前必读」清单（tokens → components → pages → 对照 templates）+ 目录索引 |
| `tokens.md` | 色彩/间距/圆角/字号/功能色说明与变量名对照表；明确何时用何种 token，禁止魔法值 |
| `components.md` | 组件清单、props 表、使用示例、**组合约束矩阵**（XX 组件只允许出现在 XX 处，例：DataTable 必须包在 Page 内、QueryBar 只在列表页顶部） |
| `pages.md` | 五种页面类型统一骨架约束：页头规则、内容区结构、查询栏+分页规则、表单布局/校验、详情只读呈现、dashboard 栅格规范；引用 templates 活例子 |
| `theming.md` | 主题预设如何新增/切换、token 覆盖规则、暗色模式注意事项 |

关键约束示例（写入 components.md / pages.md）：
- 页面不得直接 import antd 原始 Table，必须用 `@/design-system` 的 `DataTable`。
- 列表页必须：Page → QueryBar → DataTable；统一分页、loading、空态、操作列。
- 颜色一律取 token，禁止 `#hex` 硬编码；暗色模式须经 token 或 theme 感知。

自检机制：`pages.md` 内置「页面自查 checklist」，AI 生成后逐项勾选，作为一致性验收。

## 错误处理、测试与质量保障

- 错误处理：组件统一走 antd `App.useApp()` 的 `message/notification/modal`（全局唯一上下文，不用静态 message）；接口错误在 `DataTable` / `FormPage` 内统一提示。
- 测试：设计系统组件与主题预设写单元测试（Vitest + Testing Library），覆盖 tokens 生成、主题切换、必填 props 校验、组合约束。业务模板不做单测，靠模板示例 + lint 保证。当前项目未配置测试栈，实施阶段补齐 Vitest。
- 自查 Checklist：`pages.md` 内置页面一致性自查清单。
- 类型约束：组件 props 强类型 + lint（现有 eslint 配置扩展），确保按既定 API 使用。

## 与现有约定（frontend-conventions.md）的关系

根目录 `docs/frontend-conventions.md` 记录了当前架构约定（技术栈、目录、Kiota 客户端工厂、RSA 加密、`App.useApp()`、主题切换入口等）。处理：
- 将该文件迁移到 `ui/docs/frontend-conventions.md`（根目录 `docs/` 仅留后端/服务端文档）。
- 设计系统文档建立其上，作为前置约定，与本 spec 相互印证。

## 范围边界（YAGNI）

- 不拆独立 npm 包、不引入 pnpm workspace（当前单应用，收益低）。
- 不做完整重绘视觉语言（基于 antd 深化定制即可）。
- 不预先构造尚未规划的多主题，只预留可扩展的预设注册点。
- 业务页面本身不在本设计系统交付范围内；本系统只提供代码资产、模板与规范。

## 成功标准

- 五种页面类型各有一份可直接复制的模板骨架，且均来自设计系统组件。
- 主题可切换（明/暗）且可低成本新增主题预设。
- 页面可借此快速一致产出：禁止魔法值、禁止散落 antd 原始 Table/颜色硬编码。
- AI 仅凭 `ui/docs/design-system/` 文档 + `@/design-system` 组件即可写出符合全局规范的原型页。
- 设计系统组件通过单元测试与 lint 校验。
