# 组件与组合约束

对应代码：`ui/src/design-system/components/`。

## 组件清单
| 组件 | 用途 | 必填 props |
|---|---|---|
| `Page` | 页面容器+页头 | - |
| `PageToolbar` | 页头工具行（搜索/筛选 → 查询类按钮 → `|` → 操作类按钮，整体靠左）；裸排在页面底色上 | - |
| `QueryBar` | 独立查询表单（查询/重置内置，`extra` 为分隔线右侧的操作按钮）；裸排在页面底色上 | - |
| `DataTable` | 表格；容器底色卡片承载（灰表头/分页收在卡片内） | `columns`, `dataSource` |
| `AuditCell` | 表格「创建/更新」审计列两行单元格（操作人在上、时间在下；无操作人降级单行时间） | -（`user`/`time` 均可空，全空显示 `-`） |
| `FormPage` | 表单页壳 | `onFinish` |
| `DetailPage` | 详情展示 | `items` |
| `Card` | 通用卡片 | - |

> `Card` 传入的 `style`/`styles` 与组件默认值（边框/阴影/body padding 20/header 加粗底线）**合并**而非整体替换，覆盖同名属性即可定制。
| `StatCard` | 统计/指标卡 | `title`, `value` |
| `Chat` | 对话布局 | `messages` |
| `useFeedback` | 消息/通知统一出口 | -（hook） |
| `CronExpressionInput` | 5 段 cron 调度表达式选择器（常用/自定义双模式，人话描述 + 下次执行预览，展示时区换算，文案走 `ds.cron.*`） | 受控 `value`/`onChange` |

## CronExpressionInput

表单内的 cron 表达式输入（`ui/src/design-system/components/CronExpressionInput/`），替代散落各处的「裸 Input + 快选链接」写法（首站：团队自动化定时器；wiki 外部源后续接入）：

- **受控协议**：`value` 为 5 段表达式（**提交口径 UTC**），非法或未完成选择时 `onChange('')` 交由表单必填校验兜底。
- **时区**：`displayOffsetMinutes` 指定展示/选择时区相对 UTC 的分钟偏移（如北京 480）；控件按该时区选择，生成时换算为 UTC。每月 1 日叠加向西借位（跨月界）无法用标准 cron 表达时该日期动态禁用。
- **纯函数**：`cron.ts` 单独导出（`isValidCron`/`presetToCron`/`cronToPreset`/`nextCronRun` 等），可脱离组件复用。

## AuditCell

表格审计信息两行单元格（`ui/src/design-system/components/AuditCell/`），实体 CRUD 列表页「创建/更新」列的标准形态（规范见 frontend-conventions 第 8 条）：

```tsx
import { AuditCell } from '@/design-system'

{ title: t('xxx.colCreate'), key: 'create', width: 150, render: (_, r) => <AuditCell user={r.createUserName} time={r.createTime} /> }
```

- 操作人名称在上、`formatDateTime()` 时间在下（次要色小字号）。
- `user` 为空时降级为单行时间；`user`/`time` 全空显示 `-`。禁止再手写内联的两行审计 JSX 或拆四个扁平列。

## 反馈约定

`useFeedback`（`Feedback` 模块）是**唯一**的消息/通知入口，详见 `feedback.md`。业务错误走 `message`，服务器 500 / 网络异常 / 严重错误 / 系统通知走 `notification`。

## 组合约束矩阵
| 组件 | 允许出现的位置 | 禁止出现的位置 |
|---|---|---|
| `Page` | 页面根节点 | 嵌套于卡片/表格内 |
| `PageToolbar` | 页头标题下、工具行位置 | 嵌套于弹窗/表格单元格 |
| `QueryBar` | 列表页顶部、`Page` 内 | 弹窗、详情页 |
| `DataTable` | `Page` 内 | 直接嵌套进卡片 |
| `FormPage` | 表单页根 | 表格内 |
| `DetailPage` | 详情页根 | 表格内 |
| `Chat` | 对话页根 | 表格内 |

## 危险操作确认（Popconfirm）

凡**不可撤销**的重要操作，必须用 antd `Popconfirm` 包住触发按钮，二次确认后再执行：

- 删除资源、批量删除
- 清空列表 / 覆盖 / 停用 / 重置 等危险操作
- 任何会破坏数据或消耗资源、误触代价高的动作

### 约束
- 危险操作按钮**必须**包 `Popconfirm`，禁止裸 `onClick` 直接执行并凭感觉弹成功提示。
- `title` 描述操作后果，`okText`/`cancelText` 用明确动词，文案一律走 `useTranslation()`。
- 删除按钮统一 `danger`，与普通操作区分。
- `onConfirm` 为异步/耗时操作时设置 `okButtonProps={{ loading }}`，防止重复提交。
- 确认通过后，反馈统一走 `useFeedback()`（成功 `feedback.success`、失败 `feedback.error`）。

### 示例
```tsx
import { Popconfirm } from 'antd'

<Popconfirm
  title={t('confirms.deleteTitle')}
  description={t('confirms.deleteDesc')}
  okText={t('confirms.ok')}
  cancelText={t('confirms.cancel')}
  okButtonProps={{ loading: deleting }}
  onConfirm={handleDelete}
>
  <Button danger>{t('common.delete')}</Button>
</Popconfirm>
```

## 使用示例

```tsx
import { Page, PageToolbar, DataTable } from '@/design-system'

export function ExampleList() {
  return (
    <Page title="列表">
      {/* 排序规则：筛选 → 查询类按钮 → | → 操作类按钮（见 frontend-conventions 第 7 条） */}
      <PageToolbar
        filters={<><Select /><Input /></>}
        queries={<Button>刷新</Button>}
        actions={<Button type="primary">新建</Button>}
      />
      {/* 无筛选与查询按钮：操作直接左对齐、无分隔线 */}
      {/* <PageToolbar actions={<Button type="primary">新建</Button>} /> */}
      <DataTable<Item> rowKey="id" columns={columns} dataSource={data} />
    </Page>
  )
}
```

完整可运行范例见 `ui/src/design-system/templates/`。
