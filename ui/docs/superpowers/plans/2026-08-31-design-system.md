# 设计系统（Design System）实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 为 `moai-web` 建立一套基于 antd 5、以代码 + 规范文档双形态交付的设计系统（主题、共享组件、页面模板、约束文档），使后续 AI/开发者能保持全局一致并快速产出原型页。

**Architecture:** 在 `ui/src/design-system/` 内建单一可信源：`theme/`（tokens + 预设注册 + antd 配置）、`components/`（有主见强约束组件）、`templates/`（可复制骨架）。配套 `ui/docs/design-system/` 规范文档供 AI 引用。先搭测试基建，采用 TDD 逐组件推进，以 `@/design-system` 为唯一公共出口。

**Tech Stack:** React 19 + TypeScript + Ant Design 5 + react-router 7 + zustand 5 + i18next + Vitest + @testing-library/react + jsdom + Vite 6。

---

## 文件结构先行

**现有迁移/删除：**
- `ui/src/theme/config.ts`、`ui/src/theme/locale.ts` → 迁移/替换为 `ui/src/design-system/theme/`，删除 `ui/src/theme/`。
- `ui/docs/frontend-conventions.md` → 迁移到 `ui/docs/frontend-conventions.md`（根目录删）。**注**：spec 提及的迁移放本计划最后环节。
- `ui/src/store/app.ts` → `themeMode` 字段改为 `themeKey`。

**新增文件（设计系统代码）：**
```
ui/src/design-system/
├── theme/
│   ├── tokens.ts
│   ├── config.ts     # ThemeKey/ThemePreset/themePresets/getThemeConfig
│   ├── locale.ts     # getAntdLocale（迁移自旧 theme/locale.ts）
│   └── index.ts      # 主题 barrel
├── components/
│   ├── Page/{index.tsx,index.ts}
│   ├── QueryBar/{index.tsx,index.ts}
│   ├── DataTable/{index.tsx,index.ts}
│   ├── FormPage/{index.tsx,index.ts}
│   ├── DetailPage/{index.tsx,index.ts}
│   ├── Card/{Card.tsx,StatCard.tsx,index.ts}
│   └── Chat/{index.tsx,index.ts}
├── templates/
│   ├── ListTemplate.tsx
│   ├── FormTemplate.tsx
│   ├── DetailTemplate.tsx
│   ├── DashboardTemplate.tsx
│   └── ChatTemplate.tsx
├── index.ts          # 全局 barrel
└── README.md
```

**测试与配置：**
- `ui/src/test/setup.ts`、`ui/vite.config.ts`（加 test 配置）、`ui/package.json`（加脚本与 devDeps）、`ui/tsconfig.app.json`（加 types）。
- 组件测试文件：`ui/src/design-system/theme/__tests__/config.test.ts` 及各组件 `__tests__/index.test.tsx`。

**文档新增：**
```
ui/docs/design-system/
├── README.md
├── tokens.md
├── components.md
├── pages.md
└── theming.md
```

---

## Phase A：测试基建 + 主题系统

### Task 1：接入 Vitest 测试基建

**Files:**
- Modify: `ui/package.json`
- Modify: `ui/vite.config.ts`
- Modify: `ui/tsconfig.app.json`
- Create: `ui/src/test/setup.ts`

- [ ] **Step 1: 添加测试相关 devDependencies**

Run:
```bash
npm install -D vitest jsdom @testing-library/react @testing-library/jest-dom @testing-library/user-event
```

- [ ] **Step 2: package.json 添加测试脚本**

在 `ui/package.json` 的 `scripts` 内追加：
```jsonc
"test": "vitest run",
"test:watch": "vitest"
```

- [ ] **Step 3: vite.config.ts 增加 test 配置**

`ui/vite.config.ts` 改为：
```ts
/// <reference types="vitest/config" />
import { defineConfig } from 'vitest/config'
import react from '@vitejs/plugin-react'
import { fileURLToPath, URL } from 'node:url'

export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  server: {
    port: 4000,
    host: true,
    proxy: {
      '/openapi': {
        target: 'http://127.0.0.1:5000',
        changeOrigin: true,
      },
    },
  },
  build: {
    outDir: 'dist',
    sourcemap: true,
  },
  optimizeDeps: {
    include: ['react', 'react-dom'],
  },
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: ['./src/test/setup.ts'],
    css: false,
  },
})
```

- [ ] **Step 4: 创建测试 setup**

`ui/src/test/setup.ts`：
```ts
import '@testing-library/jest-dom'
```

- [ ] **Step 5: tsconfig.app.json 增加测试类型**

`ui/tsconfig.app.json` 的 `compilerOptions` 内追加：
```jsonc
"types": ["vitest/globals", "@testing-library/jest-dom"]
```

- [ ] **Step 6: 运行一次空测试确认基建可用**

Run: `npm test`
Expected: `No test files found`（无测试文件，报错可忽略）或成功退出。若报 vitest 找不到，则确认 devDeps 已装。

- [ ] **Step 7: Commit**

```bash
git add ui/package.json ui/package-lock.json ui/vite.config.ts ui/tsconfig.app.json ui/src/test/setup.ts
git commit -m "test(ui): 接入 vitest + testing-library 测试基建"
```

---

### Task 2：设计令牌 tokens.ts

**Files:**
- Create: `ui/src/design-system/theme/tokens.ts`
- Test: `ui/src/design-system/theme/__tests__/tokens.test.ts`

- [ ] **Step 1: 编写失败的测试**

`ui/src/design-system/theme/__tests__/tokens.test.ts`：
```ts
import { describe, expect, it } from 'vitest'
import { brandColors, colorPrimary, radius, spacing, fontFamily } from '../tokens'

describe('design tokens', () => {
  it('uses brand blue as the primary color', () => {
    expect(colorPrimary).toBe('#4A9EFF')
    expect(brandColors.primary).toBe(colorPrimary)
  })
  it('exposes a spacing scale on the 8px grid', () => {
    expect(spacing.xs).toBe(8)
    expect(spacing.md).toBe(16)
    expect(spacing.lg).toBe(24)
  })
  it('exposes a default radius and font family', () => {
    expect(radius.default).toBe(8)
    expect(fontFamily).toContain('sans-serif')
  })
})
```

- [ ] **Step 2: 运行测试确认失败**

Run: `npm test -- src/design-system/theme/__tests__/tokens.test.ts`
Expected: FAIL（`../tokens` 模块不存在）

- [ ] **Step 3: 实现 tokens.ts**

`ui/src/design-system/theme/tokens.ts`：
```ts
export const colorPrimary = '#4A9EFF'

export const brandColors = {
  primary: colorPrimary,
  primaryHover: '#69AFFF',
  primaryActive: '#1E7BE0',
  success: '#00B578',
  warning: '#FF9500',
  error: '#FF3B30',
  info: colorPrimary,
}

export const neutralColors = {
  textPrimary: '#1F2329',
  textSecondary: '#51565C',
  textTertiary: '#8A8F96',
  border: '#E5E8EC',
  background: '#F5F7FA',
  backgroundElevated: '#FFFFFF',
}

export const radius = {
  sm: 4,
  default: 8,
  lg: 12,
}

export const spacing = {
  xxs: 4,
  xs: 8,
  sm: 12,
  md: 16,
  lg: 24,
  xl: 32,
  xxl: 48,
}

export const fontFamily =
  "-apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif"

export const fontSize = {
  xs: 12,
  sm: 13,
  md: 14,
  lg: 16,
  xl: 20,
  xxl: 24,
}

export const controlHeight = 32
```

- [ ] **Step 4: 运行测试确认通过**

Run: `npm test -- src/design-system/theme/__tests__/tokens.test.ts`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add ui/src/design-system/theme/tokens.ts ui/src/design-system/theme/__tests__/tokens.test.ts
git commit -m "feat(ui/design-system): 新增主题设计令牌 tokens"
```

---

### Task 3：主题预设注册表与 antd 配置

**Files:**
- Create: `ui/src/design-system/theme/config.ts`
- Create: `ui/src/design-system/theme/index.ts`
- Test: `ui/src/design-system/theme/__tests__/config.test.ts`

- [ ] **Step 1: 编写失败的测试**

`ui/src/design-system/theme/__tests__/config.test.ts`：
```ts
import { describe, expect, it } from 'vitest'
import { getThemeConfig, themePresets, defaultThemeKey } from '../config'
import type { ThemeKey } from '../config'

describe('theme config', () => {
  it('returns a config for the default preset', () => {
    const config = getThemeConfig(defaultThemeKey)
    expect(config.token?.colorPrimary).toBe('#4A9EFF')
    expect(config.algorithm).toBeDefined()
  })
  it('supports light and dark presets', () => {
    expect(Object.keys(themePresets)).toEqual(['light', 'dark'])
    expect(getThemeConfig('dark').token?.colorPrimary).toBe('#4A9EFF')
  })
  it('keeps theme key type restricted', () => {
    const key: ThemeKey = 'light'
    expect(themePresets[key].name).toBeTruthy()
  })
})
```

- [ ] **Step 2: 运行测试确认失败**

Run: `npm test -- src/design-system/theme/__tests__/config.test.ts`
Expected: FAIL

- [ ] **Step 3: 实现 config.ts**

`ui/src/design-system/theme/config.ts`：
```ts
import { theme, type ThemeConfig } from 'antd'
import { brandColors, colorPrimary, radius, fontFamily } from './tokens'

export type ThemeKey = 'light' | 'dark'

export interface ThemePreset {
  key: ThemeKey
  name: string
  mode: 'light' | 'dark'
  config: ThemeConfig
}

export const defaultThemeKey: ThemeKey = 'light'

export const themePresets: Record<ThemeKey, ThemePreset> = {
  light: {
    key: 'light',
    name: '亮色',
    mode: 'light',
    config: {
      algorithm: theme.defaultAlgorithm,
      token: {
        colorPrimary,
        colorInfo: brandColors.info,
        colorSuccess: brandColors.success,
        colorWarning: brandColors.warning,
        colorError: brandColors.error,
        borderRadius: radius.default,
        fontFamily,
      },
      components: {
        Layout: {
          headerBg: '#ffffff',
          headerHeight: 60,
        },
      },
    },
  },
  dark: {
    key: 'dark',
    name: '暗色',
    mode: 'dark',
    config: {
      algorithm: theme.darkAlgorithm,
      token: {
        colorPrimary,
        colorInfo: brandColors.info,
        colorSuccess: brandColors.success,
        colorWarning: brandColors.warning,
        colorError: brandColors.error,
        borderRadius: radius.default,
        fontFamily,
      },
      components: {
        Layout: {
          headerBg: '#171C2B',
          headerHeight: 60,
        },
      },
    },
  },
}

export function getThemeConfig(themeKey: ThemeKey): ThemeConfig {
  return themePresets[themeKey].config
}
```

- [ ] **Step 4: 实现 theme/index.ts barrel**

`ui/src/design-system/theme/index.ts`：
```ts
export {
  defaultThemeKey,
  getThemeConfig,
  themePresets,
} from './config'
export type { ThemeKey, ThemePreset } from './config'
export * from './tokens'
```

- [ ] **Step 5: 运行测试确认通过**

Run: `npm test -- src/design-system/theme/__tests__/config.test.ts`
Expected: PASS

- [ ] **Step 6: Commit**

```bash
git add ui/src/design-system/theme/config.ts ui/src/design-system/theme/index.ts ui/src/design-system/theme/__tests__/config.test.ts
git commit -m "feat(ui/design-system): 新增主题预设注册表与 antd 配置"
```

---

### Task 4：把 locale.ts 迁移进 design-system/theme

**Files:**
- Create: `ui/src/design-system/theme/locale.ts`（迁移自 `ui/src/theme/locale.ts`）

- [ ] **Step 1: 创建 locale.ts（内容不变，路径迁移）**

`ui/src/design-system/theme/locale.ts`：
```ts
import type { Locale as AntdLocale } from 'antd/es/locale'
import enUS from 'antd/locale/en_US'
import zhCN from 'antd/locale/zh_CN'
import type { Locale } from '@/store/app'

const antdLocales: Record<Locale, AntdLocale> = {
  'zh-CN': zhCN,
  'en-US': enUS,
}

export function getAntdLocale(locale: Locale): AntdLocale {
  return antdLocales[locale]
}
```

- [ ] **Step 2: 在 theme/index.ts 追加导出**

在 `ui/src/design-system/theme/index.ts` 末尾追加：
```ts
export { getAntdLocale } from './locale'
```

- [ ] **Step 3: 运行类型检查**

Run: `npm run typecheck`
Expected: 通过（此时旧 `src/theme` 仍存在，无冲突）

- [ ] **Step 4: Commit**

```bash
git add ui/src/design-system/theme/locale.ts ui/src/design-system/theme/index.ts
git commit -m "feat(ui/design-system): 迁移 antd locale 映射进主题模块"
```

---

### Task 5：store 主题迁移 themeMode → themeKey

**Files:**
- Modify: `ui/src/store/app.ts`
- Modify: `ui/src/providers/AppProviders.tsx`
- Modify: `ui/src/layouts/components/AppHeader.tsx`

- [ ] **Step 1: 修改 store/app.ts**

将 `ui/src/store/app.ts` 中的主题类型与字段改为 `ThemeKey`：
```ts
import { create } from 'zustand'
import { persist } from 'zustand/middleware'
import type { ThemeKey } from '@/design-system/theme'

export type ThemeMode = ThemeKey
export type Locale = 'zh-CN' | 'en-US'
```
并把 `themeMode` 出现处改为 `themeKey`，`setThemeMode` 改为 `setThemeKey`，新增 `toggleTheme` 实现。核心 diff：
```ts
  themeKey: ThemeKey
  ...
  setThemeKey: (key: ThemeKey) => void
  toggleTheme: () => void
```
实现函数：
```ts
const getInitialTheme = (): ThemeKey => {
  const saved = localStorage.getItem('moai-web-theme')
  if (saved === 'light' || saved === 'dark') return saved
  if (window.matchMedia?.('(prefers-color-scheme: dark)').matches) return 'dark'
  return 'light'
}
```
store 对象内：
```ts
      themeKey: getInitialTheme(),
      ...
      setThemeKey: (key) => {
        localStorage.setItem('moai-web-theme', key)
        set({ themeKey: key })
      },
      toggleTheme: () => {
        const next = get().themeKey === 'dark' ? 'light' : 'dark'
        localStorage.setItem('moai-web-theme', next)
        set({ themeKey: next })
      },
```

- [ ] **Step 2: 修改 providers/AppProviders.tsx**

`ui/src/providers/AppProviders.tsx`：
```ts
import { getAntdLocale } from '@/design-system/theme'
import { getThemeConfig } from '@/design-system/theme'
...
const themeKey = useAppStore((state) => state.themeKey)
...
<ConfigProvider locale={getAntdLocale(locale)} theme={getThemeConfig(themeKey)}>
```
（保留 `locale` 取自 store 原逻辑。）

- [ ] **Step 3: 修改 layouts/components/AppHeader.tsx**

`ui/src/layouts/components/AppHeader.tsx` 将 `themeMode` 替换为 `themeKey`：
```ts
const themeKey = useAppStore((state) => state.themeKey)
...
checked={themeKey === 'dark'}
...
aria-label={themeKey === 'dark' ? t('common.switchToLight') : t('common.switchToDark')}
```
（`toggleTheme` 从 store 取值保持不变。）

- [ ] **Step 4: 删除旧 src/theme**

```bash
Remove-Item -Recurse -Force "F:\workspace\moai\ui\src\theme"
```
确认无残留引用：`npm run typecheck`。

- [ ] **Step 5: 验证**

Run: `npm run build`
Expected: 构建通过，无 `src/theme` 引用残留。

- [ ] **Step 6: Commit**

```bash
git add -A ui/src/store/app.ts ui/src/providers/AppProviders.tsx ui/src/layouts/components/AppHeader.tsx
git commit -m "refactor(ui): 主题迁移至设计系统模块并启用 themeKey"
```

---

## Phase B：强约束共享组件

> **组件文件模式（重要）**：为避免 `index.ts` 桶与组件同名导致循环自导出，组件请按「`<ComponentName>.tsx`（组件）+ `index.ts`（桶，`export { X } from './<ComponentName>'`）」组织，**不要**用 `index.tsx` 作组件文件名。以下 Task 6-12 均遵循「先失败测试 → 实现 → 通过 → 提交」的 TDD 循环；为省篇幅，各 Task 只给出一个代表性测试用例，实现时请为每个组件补 2-3 个关键用例。

### Task 6：Page（页面容器 + 页头）

**Files:**
- Create: `ui/src/design-system/components/Page/index.tsx`
- Create: `ui/src/design-system/components/Page/index.ts`
- Test: `ui/src/design-system/components/Page/__tests__/index.test.tsx`

- [ ] **Step 1: 编写测试**

`ui/src/design-system/components/Page/__tests__/index.test.tsx`：
```tsx
import { describe, expect, it } from 'vitest'
import { render, screen } from '@testing-library/react'
import { Page } from '../'

describe('Page', () => {
  it('renders title, subtitle and children', () => {
    render(<Page title="标题" subtitle="副标题">内容</Page>)
    expect(screen.getByText('标题')).toBeInTheDocument()
    expect(screen.getByText('副标题')).toBeInTheDocument()
    expect(screen.getByText('内容')).toBeInTheDocument()
  })
})
```

- [ ] **Step 2: 运行测试确认失败**

Run: `npm test -- src/design-system/components/Page/__tests__/index.test.tsx`
Expected: FAIL

- [ ] **Step 3: 实现 Page**

`ui/src/design-system/components/Page/index.tsx`：
```tsx
import { Breadcrumb, Typography } from 'antd'
import type { BreadcrumbProps } from 'antd'
import type { ReactNode } from 'react'
import { spacing } from '@/design-system/theme'

const { Title, Text } = Typography

export interface PageProps {
  title?: ReactNode
  subtitle?: ReactNode
  breadcrumb?: BreadcrumbProps['items']
  extra?: ReactNode
  children?: ReactNode
}

export function Page({ title, subtitle, breadcrumb, extra, children }: PageProps) {
  const hasHeader = Boolean(breadcrumb || title || subtitle || extra)
  return (
    <div style={{ width: '100%', maxWidth: 1200, margin: '0 auto' }}>
      {hasHeader && (
        <div style={{ marginBottom: spacing.md }}>
          {breadcrumb && (
            <Breadcrumb items={breadcrumb} style={{ marginBottom: spacing.sm }} />
          )}
          <div
            style={{
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'space-between',
              gap: spacing.md,
            }}
          >
            <div>
              {title && (
                <Title level={3} style={{ margin: 0 }}>
                  {title}
                </Title>
              )}
              {subtitle && <Text type="secondary">{subtitle}</Text>}
            </div>
            {extra && <div style={{ flexShrink: 0 }}>{extra}</div>}
          </div>
        </div>
      )}
      {children}
    </div>
  )
}
```

`ui/src/design-system/components/Page/index.ts`：
```ts
export { Page } from './index'
export type { PageProps } from './index'
```

- [ ] **Step 4: 运行测试确认通过**

Run: `npm test -- src/design-system/components/Page/__tests__/index.test.tsx`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add ui/src/design-system/components/Page
git commit -m "feat(ui/design-system): 新增 Page 页面容器组件"
```

---

### Task 7：QueryBar（列表筛选查询区）

**Files:**
- Create: `ui/src/design-system/components/QueryBar/index.tsx`
- Create: `ui/src/design-system/components/QueryBar/index.ts`
- Test: `ui/src/design-system/components/QueryBar/__tests__/index.test.tsx`
- Modify: 两个 locale JSON（加 `ds.query` 文案）

- [ ] **Step 1: 增加 ds 文案 key**

`ui/src/i18n/locales/zh-CN/common.json` 顶层追加：
```json
"ds": {
  "query": { "search": "查询", "reset": "重置" },
  "table": { "refresh": "刷新", "total": "共 {{total}} 条" },
  "form": { "submit": "提交", "cancel": "取消", "success": "保存成功" },
  "detail": { "back": "返回", "edit": "编辑" },
  "chat": { "placeholder": "输入消息...", "send": "发送" }
}
```
`ui/src/i18n/locales/en-US/common.json` 追加对应：
```json
"ds": {
  "query": { "search": "Search", "reset": "Reset" },
  "table": { "refresh": "Refresh", "total": "Total {{total}} items" },
  "form": { "submit": "Submit", "cancel": "Cancel", "success": "Saved" },
  "detail": { "back": "Back", "edit": "Edit" },
  "chat": { "placeholder": "Type a message...", "send": "Send" }
}
```

- [ ] **Step 2: 编写测试**

`ui/src/design-system/components/QueryBar/__tests__/index.test.tsx`：
```tsx
import { describe, expect, it, vi } from 'vitest'
import { render, screen, fireEvent } from '@testing-library/react'
import { QueryBar } from '../'

describe('QueryBar', () => {
  it('submits values on search', () => {
    const onSearch = vi.fn()
    render(<QueryBar onSearch={onSearch} />)
    fireEvent.click(screen.getByText('查询'))
    expect(onSearch).toHaveBeenCalled()
  })
  it('resets filters on reset', () => {
    const onReset = vi.fn()
    render(<QueryBar onReset={onReset} />)
    fireEvent.click(screen.getByText('重置'))
    expect(onReset).toHaveBeenCalled()
  })
})
```

- [ ] **Step 3: 运行测试确认失败**

Run: `npm test -- src/design-system/components/QueryBar/__tests__/index.test.tsx`
Expected: FAIL

- [ ] **Step 4: 实现 QueryBar**

`ui/src/design-system/components/QueryBar/index.tsx`：
```tsx
import { Button, Form, Space } from 'antd'
import type { FormInstance } from 'antd'
import type { ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { spacing } from '@/design-system/theme'

export interface QueryBarProps {
  form?: FormInstance
  onSearch?: (values: Record<string, unknown>) => void
  onReset?: () => void
  loading?: boolean
  children?: ReactNode
}

export function QueryBar({ form, onSearch, onReset, loading, children }: QueryBarProps) {
  const { t } = useTranslation()
  const [internalForm] = Form.useForm()
  const activeForm = form ?? internalForm

  return (
    <Form
      form={activeForm}
      layout="inline"
      style={{ marginBottom: spacing.lg }}
      onFinish={(values) => onSearch?.(values)}
    >
      {children}
      <Form.Item>
        <Space>
          <Button type="primary" htmlType="submit" loading={loading}>
            {t('ds.query.search')}
          </Button>
          <Button
            onClick={() => {
              activeForm.resetFields()
              onReset?.()
            }}
          >
            {t('ds.query.reset')}
          </Button>
        </Space>
      </Form.Item>
    </Form>
  )
}
```

`ui/src/design-system/components/QueryBar/index.ts`：
```ts
export { QueryBar } from './index'
export type { QueryBarProps } from './index'
```

- [ ] **Step 5: 运行测试确认通过**

Run: `npm test -- src/design-system/components/QueryBar/__tests__/index.test.tsx`
Expected: PASS

- [ ] **Step 6: Commit**

```bash
git add ui/src/design-system/components/QueryBar ui/src/i18n/locales
git commit -m "feat(ui/design-system): 新增 QueryBar 筛选查询组件"
```

---

### Task 8：DataTable（强约束表格）

**Files:**
- Create: `ui/src/design-system/components/DataTable/index.tsx`
- Create: `ui/src/design-system/components/DataTable/index.ts`
- Test: `ui/src/design-system/components/DataTable/__tests__/index.test.tsx`

- [ ] **Step 1: 编写测试**

`ui/src/design-system/components/DataTable/__tests__/index.test.tsx`：
```tsx
import { describe, expect, it } from 'vitest'
import { render, screen } from '@testing-library/react'
import { DataTable } from '../'

interface Row { id: number; name: string }

describe('DataTable', () => {
  it('renders columns and rows', () => {
    render(
      <DataTable<Row>
        rowKey="id"
        columns={[{ title: '名称', dataIndex: 'name' }]}
        dataSource={[{ id: 1, name: 'MoAI' }]}
        pagination={false}
      />,
    )
    expect(screen.getByText('名称')).toBeInTheDocument()
    expect(screen.getByText('MoAI')).toBeInTheDocument()
  })
  it('renders refresh action when onRefresh provided', () => {
    render(<DataTable dataSource={[]} columns={[{ title: 'c', dataIndex: 'x' }]} onRefresh={() => {}} pagination={false} />)
    expect(screen.getByText('刷新')).toBeInTheDocument()
  })
})
```

- [ ] **Step 2: 运行测试确认失败**

Run: `npm test -- src/design-system/components/DataTable/__tests__/index.test.tsx`
Expected: FAIL

- [ ] **Step 3: 实现 DataTable**

`ui/src/design-system/components/DataTable/index.tsx`：
```tsx
import { Button, Table } from 'antd'
import type { TableProps } from 'antd'
import type { ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { spacing } from '@/design-system/theme'

export interface DataTableProps<RecordType extends object>
  extends Omit<TableProps<RecordType>, 'pagination'> {
  pagination?: TableProps<RecordType>['pagination']
  toolbar?: ReactNode
  onRefresh?: () => void
  refreshLoading?: boolean
  rowKey?: TableProps<RecordType>['rowKey']
}

export function DataTable<RecordType extends object>({
  toolbar,
  onRefresh,
  refreshLoading,
  pagination,
  rowKey = 'id' as TableProps<RecordType>['rowKey'],
  ...rest
}: DataTableProps<RecordType>) {
  const { t } = useTranslation()
  const showTotal = (total: number) => t('ds.table.total', { total })
  const resolvedPagination =
    pagination === false
      ? false
      : ({ showSizeChanger: true, showTotal, ...pagination } as TableProps<RecordType>['pagination'])

  return (
    <div>
      {(toolbar || onRefresh) && (
        <div
          style={{
            display: 'flex',
            justifyContent: 'space-between',
            alignItems: 'center',
            marginBottom: spacing.md,
          }}
        >
          <div>{toolbar}</div>
          {onRefresh && (
            <Button onClick={onRefresh} loading={refreshLoading}>
              {t('ds.table.refresh')}
            </Button>
          )}
        </div>
      )}
      <Table<RecordType> rowKey={rowKey} pagination={resolvedPagination} {...rest} />
    </div>
  )
}
```

`ui/src/design-system/components/DataTable/index.ts`：
```ts
export { DataTable } from './index'
export type { DataTableProps } from './index'
```

- [ ] **Step 4: 运行测试确认通过**

Run: `npm test -- src/design-system/components/DataTable/__tests__/index.test.tsx`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add ui/src/design-system/components/DataTable
git commit -m "feat(ui/design-system): 新增 DataTable 强约束表格组件"
```

---

### Task 9：FormPage（表单页壳）

**Files:**
- Create: `ui/src/design-system/components/FormPage/index.tsx`
- Create: `ui/src/design-system/components/FormPage/index.ts`
- Test: `ui/src/design-system/components/FormPage/__tests__/index.test.tsx`

- [ ] **Step 1: 编写测试**

`ui/src/design-system/components/FormPage/__tests__/index.test.tsx`：
```tsx
import { describe, expect, it, vi } from 'vitest'
import { render, screen, fireEvent } from '@testing-library/react'
import { FormPage } from '../'

describe('FormPage', () => {
  it('renders title and submit/cancel buttons', () => {
    const { container } = render(<FormPage title="新建" onFinish={vi.fn()} onCancel={vi.fn()}><div /></FormPage>)
    expect(screen.getByText('新建')).toBeInTheDocument()
    expect(screen.getByText('提交')).toBeInTheDocument()
    expect(screen.getByText('取消')).toBeInTheDocument()
    expect(container.querySelector('form')).toBeInTheDocument()
  })
})
```

- [ ] **Step 2: 运行测试确认失败**

Run: `npm test -- src/design-system/components/FormPage/__tests__/index.test.tsx`
Expected: FAIL

- [ ] **Step 3: 实现 FormPage**

`ui/src/design-system/components/FormPage/index.tsx`：
```tsx
import { Button, Form, Space, Typography } from 'antd'
import type { FormProps } from 'antd'
import type { ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { spacing } from '@/design-system/theme'

export interface FormPageProps extends Omit<FormProps, 'onFinish'> {
  title?: ReactNode
  onFinish: (values: Record<string, unknown>) => void | Promise<void>
  onCancel?: () => void
  submitting?: boolean
  footerExtra?: ReactNode
  actionTop?: ReactNode
}

export function FormPage({
  title,
  onFinish,
  onCancel,
  submitting,
  footerExtra,
  actionTop,
  children,
  ...rest
}: FormPageProps) {
  const { t } = useTranslation()
  return (
    <div style={{ width: '100%', maxWidth: 720, margin: '0 auto' }}>
      {title && (
        <Typography.Title level={4} style={{ marginTop: 0, marginBottom: spacing.lg }}>
          {title}
        </Typography.Title>
      )}
      {actionTop}
      <Form layout="vertical" onFinish={onFinish as FormProps['onFinish']} {...rest}>
        {children}
        <Form.Item style={{ marginTop: spacing.lg, marginBottom: 0 }}>
          <Space>
            <Button type="primary" htmlType="submit" loading={submitting}>
              {t('ds.form.submit')}
            </Button>
            {onCancel && <Button onClick={onCancel}>{t('ds.form.cancel')}</Button>}
            {footerExtra}
          </Space>
        </Form.Item>
      </Form>
    </div>
  )
}
```

`ui/src/design-system/components/FormPage/index.ts`：
```ts
export { FormPage } from './index'
export type { FormPageProps } from './index'
```

- [ ] **Step 4: 运行测试确认通过**

Run: `npm test -- src/design-system/components/FormPage/__tests__/index.test.tsx`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add ui/src/design-system/components/FormPage
git commit -m "feat(ui/design-system): 新增 FormPage 表单页组件"
```

---

### Task 10：DetailPage（详情页壳）

**Files:**
- Create: `ui/src/design-system/components/DetailPage/index.tsx`
- Create: `ui/src/design-system/components/DetailPage/index.ts`
- Test: `ui/src/design-system/components/DetailPage/__tests__/index.test.tsx`

- [ ] **Step 1: 编写测试**

`ui/src/design-system/components/DetailPage/__tests__/index.test.tsx`：
```tsx
import { describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import { DetailPage } from '../'

describe('DetailPage', () => {
  it('renders title and description items', () => {
    render(
      <DetailPage
        title="详情"
        items={[{ key: 'a', label: '名称', children: 'MoAI' }]}
        onEdit={() => {}}
        onBack={() => {}}
      />,
    )
    expect(screen.getByText('详情')).toBeInTheDocument()
    expect(screen.getByText('名称')).toBeInTheDocument()
    expect(screen.getByText('MoAI')).toBeInTheDocument()
  })
})
```

- [ ] **Step 2: 运行测试确认失败**

Run: `npm test -- src/design-system/components/DetailPage/__tests__/index.test.tsx`
Expected: FAIL

- [ ] **Step 3: 实现 DetailPage**

`ui/src/design-system/components/DetailPage/index.tsx`：
```tsx
import { Button, Descriptions, Skeleton, Space, Typography } from 'antd'
import type { DescriptionsProps } from 'antd'
import type { ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { spacing } from '@/design-system/theme'

export interface DetailPageProps {
  title?: ReactNode
  loading?: boolean
  items: DescriptionsProps['items']
  column?: number
  onEdit?: () => void
  onBack?: () => void
  extra?: ReactNode
}

export function DetailPage({ title, loading, items, column, onEdit, onBack, extra }: DetailPageProps) {
  const { t } = useTranslation()
  const hasActions = Boolean(onEdit || onBack || extra)
  return (
    <div>
      {(title || hasActions) && (
        <div
          style={{
            display: 'flex',
            justifyContent: 'space-between',
            alignItems: 'center',
            marginBottom: spacing.lg,
          }}
        >
          <Typography.Title level={4} style={{ margin: 0 }}>
            {title}
          </Typography.Title>
          <Space>
            {onBack && <Button onClick={onBack}>{t('ds.detail.back')}</Button>}
            {onEdit && <Button type="primary" onClick={onEdit}>{t('ds.detail.edit')}</Button>}
            {extra}
          </Space>
        </div>
      )}
      {loading ? (
        <Skeleton active paragraph={{ rows: 6 }} />
      ) : (
        <Descriptions bordered column={column ?? 1} items={items} />
      )}
    </div>
  )
}
```

`ui/src/design-system/components/DetailPage/index.ts`：
```ts
export { DetailPage } from './index'
export type { DetailPageProps } from './index'
```

- [ ] **Step 4: 运行测试确认通过**

Run: `npm test -- src/design-system/components/DetailPage/__tests__/index.test.tsx`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add ui/src/design-system/components/DetailPage
git commit -m "feat(ui/design-system): 新增 DetailPage 详情页组件"
```

---

### Task 11：Card + StatCard（概览卡片）

**Files:**
- Create: `ui/src/design-system/components/Card/Card.tsx`
- Create: `ui/src/design-system/components/Card/StatCard.tsx`
- Create: `ui/src/design-system/components/Card/index.ts`
- Test: `ui/src/design-system/components/Card/__tests__/statCard.test.tsx`

- [ ] **Step 1: 编写测试**

`ui/src/design-system/components/Card/__tests__/statCard.test.tsx`：
```tsx
import { describe, expect, it } from 'vitest'
import { render, screen } from '@testing-library/react'
import { StatCard } from '../'

describe('StatCard', () => {
  it('renders title and value', () => {
    render(<StatCard title="用户数" value={128} />)
    expect(screen.getByText('用户数')).toBeInTheDocument()
    expect(screen.getByText('128')).toBeInTheDocument()
  })
})
```

- [ ] **Step 2: 运行测试确认失败**

Run: `npm test -- src/design-system/components/Card/__tests__/statCard.test.tsx`
Expected: FAIL

- [ ] **Step 3: 实现 Card 与 StatCard**

`ui/src/design-system/components/Card/Card.tsx`：
```tsx
import { Card as AntdCard } from 'antd'
import type { CardProps } from 'antd'

export type { CardProps }
export function Card(props: CardProps) {
  return <AntdCard bordered={false} {...props} />
}
```

`ui/src/design-system/components/Card/StatCard.tsx`：
```tsx
import { MinusOutlined, RiseOutlined, FallOutlined } from '@ant-design/icons'
import { Card } from './Card'
import { Typography } from 'antd'
import type { ReactNode } from 'react'
import { spacing } from '@/design-system/theme'

export interface StatCardProps {
  title: ReactNode
  value: ReactNode
  icon?: ReactNode
  suffix?: ReactNode
  loading?: boolean
  trend?: number
}

export type { StatCardProps }

export function StatCard({ title, value, icon, suffix, loading, trend }: StatCardProps) {
  const trendNode =
    typeof trend === 'number' ? (
      <span style={{ marginInlineStart: spacing.xs }}>
        {trend === 0 ? <MinusOutlined /> : trend > 0 ? <RiseOutlined /> : <FallOutlined />}
        {Math.abs(trend)}%
      </span>
    ) : null

  return (
    <Card loading={loading}>
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <div>
          <Typography.Text type="secondary">{title}</Typography.Text>
          <div style={{ marginTop: spacing.xs, fontSize: 24, fontWeight: 600 }}>
            {value}
            {suffix && <span style={{ marginInlineStart: spacing.xs, fontSize: 14 }}>{suffix}</span>}
          </div>
          {trendNode}
        </div>
        {icon && <div style={{ fontSize: 32 }}>{icon}</div>}
      </div>
    </Card>
  )
}
```

`ui/src/design-system/components/Card/index.ts`：
```ts
export { Card } from './Card'
export type { CardProps } from './Card'
export { StatCard } from './StatCard'
export type { StatCardProps } from './StatCard'
```

- [ ] **Step 4: 运行测试确认通过**

Run: `npm test -- src/design-system/components/Card/__tests__/statCard.test.tsx`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add ui/src/design-system/components/Card
git commit -m "feat(ui/design-system): 新增 Card / StatCard 概览卡片组件"
```

---

### Task 12：Chat（对话交互布局）

**Files:**
- Create: `ui/src/design-system/components/Chat/index.tsx`
- Create: `ui/src/design-system/components/Chat/index.ts`
- Test: `ui/src/design-system/components/Chat/__tests__/index.test.tsx`

- [ ] **Step 1: 编写测试**

`ui/src/design-system/components/Chat/__tests__/index.test.tsx`：
```tsx
import { describe, expect, it, vi } from 'vitest'
import { render, screen, fireEvent } from '@testing-library/react'
import { Chat, type ChatMessage } from '../'

const messages: ChatMessage[] = [
  { id: '1', role: 'user', content: '你好' },
  { id: '2', role: 'assistant', content: '你好，有什么可以帮你？' },
]

describe('Chat', () => {
  it('renders messages and triggers send', () => {
    const onSend = vi.fn()
    render(<Chat messages={messages} inputValue="hi" onSend={onSend} onInputChange={() => {}} />)
    expect(screen.getByText('你好')).toBeInTheDocument()
    expect(screen.getByText('你好，有什么可以帮你？')).toBeInTheDocument()
    fireEvent.click(screen.getByText('发送'))
    expect(onSend).toHaveBeenCalled()
  })
})
```

- [ ] **Step 2: 运行测试确认失败**

Run: `npm test -- src/design-system/components/Chat/__tests__/index.test.tsx`
Expected: FAIL

- [ ] **Step 3: 实现 Chat**

`ui/src/design-system/components/Chat/index.tsx`：
```tsx
import { Button, Input, Space } from 'antd'
import type { ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { spacing } from '@/design-system/theme'

export interface ChatMessage {
  id: string
  role: 'user' | 'assistant' | 'system'
  content: string
  time?: string
}

export interface ChatProps {
  messages: ChatMessage[]
  inputValue?: string
  onInputChange?: (value: string) => void
  onSend?: () => void
  sending?: boolean
  empty?: ReactNode
  height?: number | string
}

export type { ChatMessage }

export function Chat({ messages, inputValue = '', onInputChange, onSend, sending, empty, height = 480 }: ChatProps) {
  const { t } = useTranslation()
  return (
    <div style={{ display: 'flex', flexDirection: 'column', height, border: '1px solid rgba(128,128,128,0.2)', borderRadius: 8 }}>
      <div style={{ flex: 1, overflowY: 'auto', padding: spacing.lg }}>
        {messages.length === 0 ? (
          empty ?? <div style={{ textAlign: 'center', color: '#999', padding: spacing.xl }}>暂无消息</div>
        ) : (
          messages.map((m) => (
            <div
              key={m.id}
              style={{
                display: 'flex',
                justifyContent: m.role === 'user' ? 'flex-end' : 'flex-start',
                marginBottom: spacing.md,
              }}
            >
              <div
                style={{
                  maxWidth: '75%',
                  padding: `${spacing.xs}px ${spacing.sm}px`,
                  borderRadius: 8,
                  background: m.role === 'user' ? '#4A9EFF' : 'rgba(128,128,128,0.1)',
                  color: m.role === 'user' ? '#fff' : 'inherit',
                  whiteSpace: 'pre-wrap',
                }}
              >
                {m.content}
              </div>
            </div>
          ))
        )}
      </div>
      <div style={{ padding: spacing.md, borderTop: '1px solid rgba(128,128,128,0.2)', display: 'flex', gap: spacing.sm }}>
        <Input.TextArea
          autoSize={{ minRows: 1, maxRows: 4 }}
          value={inputValue}
          onChange={(e) => onInputChange?.(e.target.value)}
          placeholder={t('ds.chat.placeholder')}
        />
        <Button type="primary" onClick={onSend} loading={sending} style={{ height: 'auto' }}>
          {t('ds.chat.send')}
        </Button>
      </div>
    </div>
  )
}
```

`ui/src/design-system/components/Chat/index.ts`：
```ts
export { Chat } from './index'
export type { ChatProps, ChatMessage } from './index'
```

- [ ] **Step 4: 运行测试确认通过**

Run: `npm test -- src/design-system/components/Chat/__tests__/index.test.tsx`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add ui/src/design-system/components/Chat
git commit -m "feat(ui/design-system): 新增 Chat 对话交互布局组件"
```

---

### Task 13：设计系统全局 barrel + 模块 README

**Files:**
- Create: `ui/src/design-system/index.ts`
- Create: `ui/src/design-system/components/index.ts`
- Create: `ui/src/design-system/README.md`

- [ ] **Step 1: 创建 components barrel**

`ui/src/design-system/components/index.ts`：
```ts
export * from './Page'
export * from './QueryBar'
export * from './DataTable'
export * from './FormPage'
export * from './DetailPage'
export * from './Card'
export * from './Chat'
```

- [ ] **Step 2: 创建全局 barrel**

`ui/src/design-system/index.ts`：
```ts
export * from './theme'
export * from './components'
```

- [ ] **Step 3: 创建模块 README**

`ui/src/design-system/README.md`：
```md
# MoAI Design System

`@/design-system` 是 MoAI 前端设计系统的唯一公共出口。

## 组成
- `theme`：主题 tokens、预设注册、antd 配置。
- `components`：有主见的强约束共享组件。
- `templates`：可复制的页面骨架示例。

## 约定
- 业务页面一律从 `@/design-system` 导入组件，禁止直接散落 antd 原始 Table。
- 颜色/间距一律取 token，禁止 magic number。
- 主题切换由 store 的 `themeKey` 驱动，详见 `ui/docs/design-system/theming.md`。
```

- [ ] **Step 4: 校验**

Run: `npm run typecheck && npm run lint`
Expected: 通过（新增文件无未使用报错）

- [ ] **Step 5: Commit**

```bash
git add ui/src/design-system/index.ts ui/src/design-system/components/index.ts ui/src/design-system/README.md
git commit -m "feat(ui/design-system): 组装全局出口与模块说明"
```

---

## 提示：Templates 均复用上述组件，遵循「Page 包裹 + 设计系统组件 + i18n key + mock util」模式。每个模板独立提交。

### Task 14：ListTemplate（列表页骨架）

**Files:**
- Create: `ui/src/design-system/templates/ListTemplate.tsx`

- [ ] **Step 1: 实现模板**

`ui/src/design-system/templates/ListTemplate.tsx`：
```tsx
import { Button, Form, Input, Select, Tag, Space } from 'antd'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Page, QueryBar, DataTable } from '@/design-system'
import type { TableColumnsType } from 'antd'

interface Item {
  id: number
  name: string
  status: 'active' | 'disabled'
  createdAt: string
}

const mockData: Item[] = [
  { id: 1, name: '示例应用 A', status: 'active', createdAt: '2026-08-01' },
  { id: 2, name: '示例应用 B', status: 'disabled', createdAt: '2026-08-02' },
]

export function ListTemplate() {
  const { t } = useTranslation()
  const [loading, setLoading] = useState(false)

  const columns: TableColumnsType<Item> = [
    { title: t('ds.list.name'), dataIndex: 'name' },
    {
      title: t('ds.list.status'),
      dataIndex: 'status',
      render: (s: Item['status']) => (
        <Tag color={s === 'active' ? 'green' : 'default'}>
          {s === 'active' ? t('ds.list.statusActive') : t('ds.list.statusDisabled')}
        </Tag>
      ),
    },
    { title: t('ds.list.createdAt'), dataIndex: 'createdAt' },
    {
      title: t('ds.list.actions'),
      key: 'actions',
      render: () => (
        <Space>
          <Button type="link">{t('ds.list.edit')}</Button>
          <Button type="link" danger>{t('ds.list.delete')}</Button>
        </Space>
      ),
    },
  ]

  return (
    <Page title={t('ds.list.title')} subtitle={t('ds.list.subtitle')}>
      <QueryBar
        loading={loading}
        onSearch={() => setLoading(true)}
        onReset={() => setLoading(false)}
      >
        <Form.Item name="name" label={t('ds.list.nameLabel')}>
          <Input placeholder={t('ds.list.namePlaceholder')} />
        </Form.Item>
        <Form.Item name="status" label={t('ds.list.status')}>
          <Select
            allowClear
            style={{ width: 160 }}
            options={[
              { value: 'active', label: t('ds.list.statusActive') },
              { value: 'disabled', label: t('ds.list.statusDisabled') },
            ]}
          />
        </Form.Item>
      </QueryBar>
      <DataTable<Item>
        rowKey="id"
        columns={columns}
        dataSource={mockData}
        loading={loading}
        toolbar={<Button type="primary">{t('ds.list.create')}</Button>}
        onRefresh={() => setLoading(true)}
        refreshLoading={loading}
      />
    </Page>
  )
}
```

- [ ] **Step 2: 补充 ds.list 文案 key**

在两个 locale JSON 顶层 `ds` 内追加：
```json
"list": {
  "title": "列表页模板",
  "subtitle": "演示 Page + QueryBar + DataTable 的标准组合",
  "nameLabel": "名称",
  "namePlaceholder": "请输入名称",
  "status": "状态",
  "statusActive": "启用",
  "statusDisabled": "停用",
  "name": "名称",
  "createdAt": "创建时间",
  "actions": "操作",
  "edit": "编辑",
  "delete": "删除",
  "create": "新建"
}
```
（en-US 目录增加对应英文文案。）

- [ ] **Step 3: 校验**

Run: `npm run typecheck`
Expected: 通过

- [ ] **Step 4: Commit**

```bash
git add ui/src/design-system/templates/ListTemplate.tsx ui/src/i18n/locales
git commit -m "feat(ui/design-system): 新增 ListTemplate 列表页骨架"
```

---

### Task 15：FormTemplate（表单页骨架）

**Files:**
- Create: `ui/src/design-system/templates/FormTemplate.tsx`

- [ ] **Step 1: 实现模板**

`ui/src/design-system/templates/FormTemplate.tsx`：
```tsx
import { App, Form, Input, Select, InputNumber } from 'antd'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Page, FormPage } from '@/design-system'

export function FormTemplate() {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const [submitting, setSubmitting] = useState(false)

  return (
    <Page>
      <FormPage
        title={t('ds.form.title')}
        submitting={submitting}
        onCancel={() => {}}
        onFinish={() => {
          setSubmitting(true)
          setTimeout(() => {
            setSubmitting(false)
            message.success(t('ds.form.success'))
          }, 500)
        }}
      >
        <Form.Item name="name" label={t('ds.form.nameLabel')} rules={[{ required: true }]}>
          <Input placeholder={t('ds.form.namePlaceholder')} />
        </Form.Item>
        <Form.Item name="type" label={t('ds.form.type')} rules={[{ required: true }]}>
          <Select
            options={[
              { value: 'single', label: t('ds.form.typeSingle') },
              { value: 'group', label: t('ds.form.typeGroup') },
            ]}
          />
        </Form.Item>
        <Form.Item name="count" label={t('ds.form.count')}>
          <InputNumber style={{ width: '100%' }} />
        </Form.Item>
      </FormPage>
    </Page>
  )
}
```

- [ ] **Step 2: 补充 ds.form 文案 key**

在两个 locale JSON 顶层 `ds.form` 内追加：
```json
"title": "表单页模板",
"nameLabel": "名称",
"namePlaceholder": "请输入名称",
"type": "类型",
"typeSingle": "单条",
"typeGroup": "分组",
"count": "数量"
```
（en-US 目录增加对应英文。）

- [ ] **Step 3: 校验**

Run: `npm run typecheck`
Expected: 通过

- [ ] **Step 4: Commit**

```bash
git add ui/src/design-system/templates/FormTemplate.tsx ui/src/i18n/locales
git commit -m "feat(ui/design-system): 新增 FormTemplate 表单页骨架"
```

---

### Task 16：DetailTemplate（详情页骨架）

**Files:**
- Create: `ui/src/design-system/templates/DetailTemplate.tsx`

- [ ] **Step 1: 实现模板**

`ui/src/design-system/templates/DetailTemplate.tsx`：
```tsx
import { Tag } from 'antd'
import { useTranslation } from 'react-i18next'
import { Page, DetailPage } from '@/design-system'

export function DetailTemplate() {
  const { t } = useTranslation()
  return (
    <Page>
      <DetailPage
        title={t('ds.detail.title')}
        onBack={() => {}}
        onEdit={() => {}}
        items={[
          { key: 'name', label: t('ds.detail.name'), children: '示例应用 A' },
          {
            key: 'status',
            label: t('ds.detail.status'),
            children: <Tag color="green">{t('ds.detail.statusActive')}</Tag>,
          },
          { key: 'desc', label: t('ds.detail.desc'), children: t('ds.detail.descValue') },
          { key: 'createdAt', label: t('ds.detail.createdAt'), children: '2026-08-01' },
        ]}
      />
    </Page>
  )
}
```

- [ ] **Step 2: 补充 ds.detail 文案 key**

在两个 locale JSON 顶层 `ds.detail` 内追加：
```json
"title": "详情页模板",
"name": "名称",
"status": "状态",
"statusActive": "启用",
"desc": "描述",
"descValue": "这里是只读详情展示的示例描述文本。",
"createdAt": "创建时间"
```
（en-US 目录增加对应英文。）

- [ ] **Step 3: 校验**

Run: `npm run typecheck`
Expected: 通过

- [ ] **Step 4: Commit**

```bash
git add ui/src/design-system/templates/DetailTemplate.tsx ui/src/i18n/locales
git commit -m "feat(ui/design-system): 新增 DetailTemplate 详情页骨架"
```

---

### Task 17：DashboardTemplate（概览骨架）

**Files:**
- Create: `ui/src/design-system/templates/DashboardTemplate.tsx`

- [ ] **Step 1: 实现模板**

`ui/src/design-system/templates/DashboardTemplate.tsx`：
```tsx
import { AppstoreOutlined, UserOutlined, ApiOutlined } from '@ant-design/icons'
import { Col, Row } from 'antd'
import { useTranslation } from 'react-i18next'
import { Page, Card, StatCard } from '@/design-system'

export function DashboardTemplate() {
  const { t } = useTranslation()
  return (
    <Page title={t('ds.dash.title')} subtitle={t('ds.dash.subtitle')}>
      <Row gutter={[16, 16]}>
        <Col xs={24} sm={12} lg={6}>
          <StatCard title={t('ds.dash.users')} value={128} icon={<UserOutlined />} trend={12.5} />
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <StatCard title={t('ds.dash.apps')} value={32} icon={<AppstoreOutlined />} trend={-3} />
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <StatCard title={t('ds.dash.requests')} value={2048} icon={<ApiOutlined />} trend={15} />
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <StatCard title={t('ds.dash.tickets')} value={7} icon={<ApiOutlined />} />
        </Col>
      </Row>
      <Card title={t('ds.dash.chart')} style={{ marginTop: 16 }}>
        {t('ds.dash.chartPlaceholder')}
      </Card>
    </Page>
  )
}
```

- [ ] **Step 2: 补充 ds.dash 文案 key**

在两个 locale JSON 顶层 `ds` 内追加：
```json
"dash": {
  "title": "概览",
  "subtitle": "统计概览模板",
  "users": "用户数",
  "apps": "应用数",
  "requests": "调用次数",
  "tickets": "待处理工单",
  "chart": "趋势图",
  "chartPlaceholder": "此处放置图表占位。"
}
```
（en-US 目录增加对应英文。）

- [ ] **Step 3: 校验**

Run: `npm run typecheck`
Expected: 通过

- [ ] **Step 4: Commit**

```bash
git add ui/src/design-system/templates/DashboardTemplate.tsx ui/src/i18n/locales
git commit -m "feat(ui/design-system): 新增 DashboardTemplate 概览骨架"
```

---

### Task 18：ChatTemplate（对话骨架）

**Files:**
- Create: `ui/src/design-system/templates/ChatTemplate.tsx`

- [ ] **Step 1: 实现模板**

`ui/src/design-system/templates/ChatTemplate.tsx`：
```tsx
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Page, Chat, type ChatMessage } from '@/design-system'

export function ChatTemplate() {
  const { t } = useTranslation()
  const [inputValue, setInputValue] = useState('')
  const [messages, setMessages] = useState<ChatMessage[]>([])

  const handleSend = () => {
    const text = inputValue.trim()
    if (!text) return
    setMessages((prev) => [
      ...prev,
      { id: String(prev.length + 1), role: 'user', content: text },
    ])
    setInputValue('')
  }

  return (
    <Page title={t('ds.chat.title')} subtitle={t('ds.chat.subtitle')}>
      <Chat
        messages={messages}
        inputValue={inputValue}
        onInputChange={setInputValue}
        onSend={handleSend}
        height={600}
        empty={t('ds.chat.empty')}
      />
    </Page>
  )
}
```

- [ ] **Step 2: 补充 ds.chat 文案 key**

在两个 locale JSON 顶层 `ds.chat` 内追加：
```json
"title": "对话页",
"subtitle": "对话交互布局",
"empty": "开始一段对话吧。"
```
（en-US 目录增加对应英文。）

- [ ] **Step 3: 校验**

Run: `npm run typecheck`
Expected: 通过

- [ ] **Step 4: Commit**

```bash
git add ui/src/design-system/templates/ChatTemplate.tsx ui/src/i18n/locales
git commit -m "feat(ui/design-system): 新增 ChatTemplate 对话骨架"
```

---

## Phase C：规范文档（AI 参考资料）

### Task 19：迁移 frontend-conventions.md 到 ui/docs

**Files:**
- Move: `docs/frontend-conventions.md` → `ui/docs/frontend-conventions.md`

- [ ] **Step 1: 移动文件**

```bash
git mv "F:\workspace\moai\docs\frontend-conventions.md" "F:\workspace\moai\ui\docs\frontend-conventions.md"
```

- [ ] **Step 2: 路径修正**

在该文件内，任何指向旧位置或需要指向 `ui/docs/design-system/` / `@/design-system` 的说明做最小语义更新（如提及 `docs/` 的地方改为 `ui/docs/design-system/` 引用）。

- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "docs(ui): 前端约定文档迁移进 ui/docs"
```

---

### Task 20：设计系统规范文档

**Files:**
- Create: `ui/docs/design-system/README.md`
- Create: `ui/docs/design-system/tokens.md`
- Create: `ui/docs/design-system/components.md`
- Create: `ui/docs/design-system/pages.md`
- Create: `ui/docs/design-system/theming.md`

- [ ] **Step 1: 创建 AI 入口 README**

`ui/docs/design-system/README.md`：
```md
# MoAI 设计系统

> 做业务页面前必读本文档与下列分册。设计系统代码统一从 `@/design-system` 导入。

## 必读顺序
1. `tokens.md` —— 令牌与取用规则
2. `components.md` —— 组件与组合约束矩阵
3. `pages.md` —— 页面原型约束与自查清单
4. `theming.md` —— 主题切换与新增预设

## 目录索引
- 代码：`ui/src/design-system/`
- 模板活例子：`ui/src/design-system/templates/`
- 根规范：`../frontend-conventions.md`

## 核心红线
- 页面禁止直接 `import { Table } from 'antd'`，必须用 `@/design-system` 的 `DataTable`。
- 颜色/间距一律取 token，禁止魔法值与 `#hex` 硬编码。
- 文案一律走 `useTranslation()`，禁止硬编码。
```

- [ ] **Step 2: 创建 tokens 文档**

`ui/docs/design-system/tokens.md`：
```md
# 令牌 tokens

对应代码：`ui/src/design-system/theme/tokens.ts`。

## 色彩
| 令牌 | 值 | 用途 |
|---|---|---|
| `colorPrimary` | `#4A9EFF` | 品牌主色 |
| `brandColors.primary` | `#4A9EFF` | 主色别名 |
| `brandColors.success` | `#00B578` | 成功 |
| `brandColors.warning` | `#FF9500` | 警告 |
| `brandColors.error` | `#FF3B30` | 错误 |
| `brandColors.info` | `#4A9EFF` | 信息 |

## 间距（8px 网格）
| 令牌 | 值 |
|---|---|
| `spacing.xxs` | 4 |
| `spacing.xs` | 8 |
| `spacing.sm` | 12 |
| `spacing.md` | 16 |
| `spacing.lg` | 24 |
| `spacing.xl` | 32 |
| `spacing.xxl` | 48 |

## 圆角 / 字号
- `radius.sm|default|lg` = 4 / 8 / 12。
- `fontSize.xs..xxl` = 12 / 13 / 14 / 16 / 20 / 24。

## 规则
- 页面一律通过 `import { ... } from '@/design-system'` 取用令牌。
- 禁止在 jsx/style 内写魔法数字或硬编码色值。
```

- [ ] **Step 3: 创建组件文档（含组合约束矩阵）**

`ui/docs/design-system/components.md`：
```md
# 组件与组合约束

对应代码：`ui/src/design-system/components/`。

## 组件清单
| 组件 | 用途 | 必填 props |
|---|---|---|
| `Page` | 页面容器+页头 | - |
| `QueryBar` | 列表筛选区 | - |
| `DataTable` | 表格 | `columns`, `dataSource` |
| `FormPage` | 表单页壳 | `onFinish` |
| `DetailPage` | 详情展示 | `items` |
| `Card` / `StatCard` | 卡片/统计卡 | - |
| `Chat` | 对话布局 | `messages` |

## 组合约束矩阵
| 组件 | 允许出现的位置 | 禁止出现的位置 |
|---|---|---|
| `Page` | 页面根节点 | 嵌套于卡片/表格内 |
| `QueryBar` | 列表页顶部、`Page` 内 | 弹窗、详情页 |
| `DataTable` | `Page` 内 | 直接嵌套进卡片 |
| `FormPage` | 表单页根 | 表格内 |
| `DetailPage` | 详情页根 | 表格内 |
| `Chat` | 对话页根 | 表格内 |

## 使用示例

```tsx
import { Page, QueryBar, DataTable } from '@/design-system'
```
```

- [ ] **Step 4: 创建页面文档（含自查清单）**

`ui/docs/design-system/pages.md`：
```md
# 页面原型约束

对应活例子：`ui/src/design-system/templates/`。

## 五种页面骨架
1. 列表页：`Page` → `QueryBar` → `DataTable`（分页/loading/空态/操作列）。
2. 表单页：`Page` → `FormPage`（布局、校验、提交/取消）。
3. 详情页：`Page` → `DetailPage`（只读、返回/编辑）。
4. 概览页：`Page` → `Row/Col` + `StatCard` + 卡片。
5. 对话页：`Page` → `Chat`。

## 自查清单（AI 生成页面后逐项勾选）
- [ ] 页面根节点使用 `Page` 包裹。
- [ ] 列表页使用 `QueryBar`+`DataTable`，未散落 antd Table。
- [ ] 颜色/间距取 token，无魔法值。
- [ ] 文案走 `useTranslation()`，无硬编码。
- [ ] 暗色模式下对比度可用（经 token 感知）。
- [ ] 接口错误经 antd `App.useApp()` 的 message/notification 提示。
```

- [ ] **Step 5: 创建主题文档**

`ui/docs/design-system/theming.md`：
```md
# 主题系统

对应代码：`ui/src/design-system/theme/`。

## 结构
- `tokens.ts`：令牌基线。
- `config.ts`：`ThemeKey`、`ThemePreset`、`themePresets`、`getThemeConfig`。

## 切换
- store 的 `themeKey`（`'light' | 'dark'`）驱动；`AppProviders` 传 `getThemeConfig(themeKey)` 给 antd `ConfigProvider`。

## 新增主题预设
1. 在 `config.ts` 的 `themePresets` 中新增一条记录。
2. `ThemeKey` 联合类型加入新 key。
3. 在 `theming.md` 补说明与 token 覆盖要点。

## 注意事项
- 暗色模式请勿硬编码颜色，一律经 token 或 theme 感知取色，保证对比度。
```

- [ ] **Step 6: 校验文档引用一致性**

Run: `npm run typecheck`
Expected: 通过（文档不参与类型检查，仅确认无代码改动导致断裂）

- [ ] **Step 7: Commit**

```bash
git add ui/docs/design-system
git commit -m "docs(ui): 新增设计系统规范文档"
```

---

## 最终验收

### Task 21：全量校验与提交

- [ ] **Step 1: 全量测试**

Run: `npm test`
Expected: 全部测试通过

- [ ] **Step 2: 类型检查与 lint**

Run: `npm run typecheck && npm run lint`
Expected: 均通过

- [ ] **Step 3: 构建验证**

Run: `npm run build`
Expected: 构建成功

- [ ] **Step 4: 提交（若无改动则跳过）**

```bash
git add -A
git commit -m "chore(ui): 设计系统最终校验"
```

---

## Spec 覆盖检查（自审）

- 主题系统（tokens/预设/store 迁移/可扩展）→ Task 2,3,4,5
- 组件层（7 类强约束组件）→ Task 6-12
- 页面模板（5 类）→ Task 14-18
- 规范文档（5 册 + AI 入口 + 自查清单）→ Task 20
- 前端文档迁入 ui/docs（frontend-conventions）→ Task 19
- 测试基建 → Task 1
- 全局 barrel/README → Task 13
- 错误处理（App.useApp 约定）→ pages.md 清单 + 组件实现遵循
