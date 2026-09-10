# 知识库向量化设置 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在知识库设置页让团队管理员配置向量模型、维度和切片参数，并在已向量化后锁定该配置。

**Architecture:** 复用 `ui/src/api/wiki.ts` 已有的 `getWikiModelOptions` 与 `updateWikiEmbedding` 经认证请求封装。`WikiDetail` 将基础设置和向量化设置拆为两个 Form 与两个保存动作；页面仅在管理员访问时请求模型候选项，后端仍是权限、模型类型和锁定状态的权威。

**Tech Stack:** React 19、TypeScript、Ant Design 5、Vitest、React Testing Library、i18next。

---

## 文件结构

- 修改 `ui/src/pages/wiki/WikiDetail.tsx`：详情类型、模型选项加载、向量化配置表单和锁定状态。
- 修改 `ui/src/pages/wiki/__tests__/WikiDetail.test.tsx`：覆盖管理员保存、手填维度、锁定与成员只读。
- 修改 `ui/src/i18n/locales/zh-CN/common.json`：中文设置文案。
- 修改 `ui/src/i18n/locales/en-US/common.json`：对应英文设置文案。
- 修改 `docs/wiki/{sdd,bdd,tdd}.md`：记录配置能力、行为场景与验证映射。

### Task 1: 扩展详情页单测

**Files:**
- Modify: `ui/src/pages/wiki/__tests__/WikiDetail.test.tsx`

- [ ] **Step 1: 扩展 API mock 与默认详情数据**

```ts
import { getWikiDetail, getWikiModelOptions, updateWiki, updateWikiEmbedding, getWikiDocuments } from '@/api/wiki'

vi.mock('@/api/wiki', () => ({
  getWikiDetail: vi.fn(),
  getWikiModelOptions: vi.fn(),
  updateWiki: vi.fn().mockResolvedValue(undefined),
  updateWikiEmbedding: vi.fn().mockResolvedValue(undefined),
  getWikiDocuments: vi.fn(),
}))

vi.mocked(getWikiDetail).mockResolvedValue({
  wikiId: '7', teamId: '3', name: '产品文档', description: '产品相关', isPublic: true,
  myRole: 2, embeddingModelId: 'embedding-id', embeddingDimensions: 1024,
  metadataModelId: 'metadata-id', chunkSize: 800, chunkOverlap: 100, isLock: false,
} as never)
vi.mocked(getWikiModelOptions).mockResolvedValue({
  embeddingModels: [{ id: 'embedding-id', name: 'text-embedding-3-large', modelKind: 'embedding' }],
  conversationModels: [{ id: 'metadata-id', name: 'gpt-4.1-mini', modelKind: 'conversation' }],
})
```

- [ ] **Step 2: 添加失败测试**

```ts
it('管理员可保存向量化配置并支持手填维度', async () => {
  renderDetail('/team/3/wiki/7/settings')
  const dimension = await screen.findByLabelText('向量维度')
  fireEvent.change(dimension, { target: { value: '1536' } })
  fireEvent.click(screen.getByRole('button', { name: '保存向量化配置' }))
  await waitFor(() => expect(updateWikiEmbedding).toHaveBeenCalledWith(7, {
    embeddingModelId: 'embedding-id', embeddingDimensions: 1536,
    metadataModelId: 'metadata-id', chunkSize: 800, chunkOverlap: 100,
  }))
})

it('锁定的向量化配置只读', async () => {
  vi.mocked(getWikiDetail).mockResolvedValue({ ...lockedWiki, isLock: true } as never)
  renderDetail('/team/3/wiki/7/settings')
  expect(await screen.findByText('向量化配置已锁定')).toBeInTheDocument()
  expect(screen.getByLabelText('向量维度')).toBeDisabled()
  expect(screen.queryByRole('button', { name: '保存向量化配置' })).toBeNull()
})
```

- [ ] **Step 3: 运行测试，确认现有页面尚不满足新场景**

Run: `cd ui && npm run test -- src/pages/wiki/__tests__/WikiDetail.test.tsx`

Expected: FAIL，缺少向量化字段、控件或保存调用。

### Task 2: 实现管理员向量化配置表单

**Files:**
- Modify: `ui/src/pages/wiki/WikiDetail.tsx`

- [ ] **Step 1: 补齐类型、状态和 API 导入**

```ts
import { Alert, Avatar, Button, Form, Input, InputNumber, Layout, Menu, Select, Space, Spin, Switch, Tag, Typography, Upload } from 'antd'
import { getWikiDetail, getWikiModelOptions, updateWiki, updateWikiEmbedding, uploadWikiAvatar, type WikiModelOptionsResult } from '@/api/wiki'

const EMBEDDING_DIMENSIONS = [256, 512, 1024, 2048, 2560, 4096]

interface EmbeddingFormValues {
  embeddingModelId: string
  embeddingDimensions: number
  metadataModelId: string
  chunkSize: number
  chunkOverlap: number
}
```

在 `WikiDetail` 与 `SettingsFormValues` 中分别补齐详情配置字段、`embeddingForm`、`embeddingSaving`、`modelOptions`、`modelOptionsLoading` 与 `modelOptionsFailed` 状态。

- [ ] **Step 2: 在管理员加载路径请求候选模型并回显配置**

```ts
const [detail, options] = await Promise.all([
  getWikiDetail(wikiId),
  isAdminPlus ? getWikiModelOptions(teamId) : Promise.resolve(null),
])

embeddingForm.setFieldsValue({
  embeddingModelId: detail.embeddingModelId ?? undefined,
  embeddingDimensions: detail.embeddingDimensions ?? 1024,
  metadataModelId: detail.metadataModelId ?? undefined,
  chunkSize: detail.chunkSize ?? 800,
  chunkOverlap: detail.chunkOverlap ?? 100,
})
setModelOptions(options)
```

以 `detail.myRole !== ROLE_MEMBER` 而不是旧状态判断管理员身份，防止首次渲染漏请求；仅管理员调用 `getWikiModelOptions`。候选项加载失败时保留基础设置表单，并令向量化分区无法提交。

- [ ] **Step 3: 添加独立的保存处理函数**

```ts
const handleSaveEmbedding = async () => {
  const values = await embeddingForm.validateFields()
  setEmbeddingSaving(true)
  try {
    await updateWikiEmbedding(wikiId, values)
    feedback.success(t('wiki.embedding.saveSuccess'))
    void load()
  } catch {
    void load()
  } finally {
    setEmbeddingSaving(false)
  }
}
```

- [ ] **Step 4: 在管理员设置表单后渲染向量化配置分区**

使用第二个 `<Form layout="vertical">`，并提供 `Select` 控件选择向量模型和元数据模型。维度使用 `Select<number>` 的 `showSearch`、`mode` 不设置、`options={EMBEDDING_DIMENSIONS.map((value) => ({ value, label: String(value) }))}` 与 `onSearch` 不适用；改用 `InputNumber` 搭配常用值 `Select` 会破坏一个字段的单一回显。改为 `Select` 的 `showSearch`、`optionFilterProp="label"`、`dropdownRender` 不支持自由值，因此采用 `AutoComplete` 包裹 `InputNumber`，最终向 Form 保存 number。

实际控件实现：`Form.Item` 中渲染 `<InputNumber min={1} max={4096} precision={0} controls />`，其下用无标签 `Select` 展示预设快捷选择；预设变更调用 `embeddingForm.setFieldValue('embeddingDimensions', value)`。这使手填、范围校验、旧值回显均可靠。

每个字段规则：模型必选；`embeddingDimensions` 必须为整数且在 $1$–$4096$；`chunkSize` 为整数 $1$–$8192$；`chunkOverlap` 为整数且不小于 $0$。`wiki.isLock` 时向量化表单字段 `disabled`，显示锁定 `Alert`，不渲染保存按钮。

- [ ] **Step 5: 运行详情页测试，确认通过**

Run: `cd ui && npm run test -- src/pages/wiki/__tests__/WikiDetail.test.tsx`

Expected: PASS，覆盖已有详情行为与新增配置场景。

### Task 3: 补齐双语文案和全部前端验证

**Files:**
- Modify: `ui/src/i18n/locales/zh-CN/common.json`
- Modify: `ui/src/i18n/locales/en-US/common.json`

- [ ] **Step 1: 在两个 `wiki` 节点加入同构的 `embedding` 文案**

```json
"embedding": {
  "title": "向量化配置",
  "hint": "该配置用于后续上传文件的切片和向量化。",
  "model": "向量模型",
  "dimension": "向量维度",
  "metadataModel": "元数据模型",
  "chunkSize": "切片长度",
  "chunkOverlap": "切片重叠",
  "locked": "向量化配置已锁定",
  "lockedHint": "已有文件完成向量化，不能再修改模型、维度或切片参数。",
  "save": "保存向量化配置",
  "saveSuccess": "向量化配置已保存",
  "optionsFailed": "无法加载可用模型"
}
```

英文文件使用对应英文值，JSON 键严格一致。

- [ ] **Step 2: 执行前端静态和单元验证**

Run: `cd ui && npm run typecheck && npm run lint && npm run test`

Expected: 三个命令均以退出码 `0` 完成。

### Task 4: 同步知识库领域规格

**Files:**
- Modify: `docs/wiki/sdd.md`
- Modify: `docs/wiki/bdd.md`
- Modify: `docs/wiki/tdd.md`

- [ ] **Step 1: 在 SDD 增加向量化配置与锁定决策**

记录已存在 API、团队授权候选模型、维度预设加手填规则，以及首次向量化后锁定的决定。

- [ ] **Step 2: 在 BDD 新增永久编号场景**

```gherkin
@WK-S13 @auto:vitest
Scenario: 管理员设置向量化配置
  Given 管理员打开未锁定知识库的设置页
  When 选择已授权向量模型并输入有效维度
  Then 配置保存成功并回显

@WK-S14 @auto:vitest
Scenario: 已向量化知识库锁定配置
  Given 知识库已有完成向量化的文件
  When 管理员打开设置页
  Then 向量化模型、维度和切片参数不可修改
```

- [ ] **Step 3: 在 TDD 增加验证映射和实际执行结果**

将 `@WK-S13`、`@WK-S14` 映射至 `ui/src/pages/wiki/__tests__/WikiDetail.test.tsx`；填写本次 `npm run test` 的实际 PASS 结果与日期，不填写未执行结果。

- [ ] **Step 4: 检查文档和改动格式**

Run: `git diff --check -- docs/wiki/sdd.md docs/wiki/bdd.md docs/wiki/tdd.md ui/src/pages/wiki/WikiDetail.tsx ui/src/pages/wiki/__tests__/WikiDetail.test.tsx ui/src/i18n/locales/zh-CN/common.json ui/src/i18n/locales/en-US/common.json`

Expected: 无输出，退出码 `0`。
