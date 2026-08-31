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
