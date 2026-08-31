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
| `Card` | 通用卡片 | - |
| `StatCard` | 统计/指标卡 | `title`, `value` |
| `Chat` | 对话布局 | `messages` |
| `useFeedback` | 消息/通知统一出口 | -（hook） |

## 反馈约定

`useFeedback`（`Feedback` 模块）是**唯一**的消息/通知入口，详见 `feedback.md`。业务错误走 `message`，服务器 500 / 网络异常 / 严重错误 / 系统通知走 `notification`。

## 组合约束矩阵
| 组件 | 允许出现的位置 | 禁止出现的位置 |
|---|---|---|
| `Page` | 页面根节点 | 嵌套于卡片/表格内 |
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
import { Page, QueryBar, DataTable } from '@/design-system'

export function ExampleList() {
  return (
    <Page title="列表">
      <QueryBar onSearch={(v) => console.log(v)} />
      <DataTable<Item> rowKey="id" columns={columns} dataSource={data} />
    </Page>
  )
}
```

完整可运行范例见 `ui/src/design-system/templates/`。
