import type { ReactNode } from 'react'
import { Divider } from 'antd'
import { spacing } from '@/design-system/theme'

export interface PageToolbarProps {
  /**
   * 搜索输入框、筛选/下拉组件等查询控件，靠左。
   */
  filters?: ReactNode
  /**
   * 查询类按钮（查询/重置/刷新等），紧跟筛选控件，位于竖分隔线左侧。
   */
  queries?: ReactNode
  /**
   * 操作类按钮（新建/同步等），位于竖分隔线右侧。
   */
  actions?: ReactNode
}

/**
 * 页头工具栏，统一内联布局规则：
 * 搜索/筛选 → 查询类按钮 → 竖分隔线（|）→ 操作类按钮，整体靠左；
 * 无筛选与查询按钮时，操作按钮直接靠左、无分隔线。
 *
 * 视觉：裸排在页面底色上（Dify 模式，不另起卡片），与下方 DataTable 的容器底色卡片
 * 之间仅隔同色留白；表格的灰表头收在卡片内，天然分层。
 */
export function PageToolbar({ filters, queries, actions }: PageToolbarProps) {
  const hasFilters = Boolean(filters)
  const hasQueries = Boolean(queries)
  return (
    <div
      style={{
        display: 'flex',
        alignItems: 'center',
        gap: spacing.md,
        marginBottom: spacing.md,
      }}
    >
      {hasFilters && (
        <div
          style={{
            display: 'flex',
            alignItems: 'center',
            gap: spacing.xs,
            minWidth: 0,
            flexWrap: 'wrap',
          }}
        >
          {filters}
        </div>
      )}
      {hasQueries && (
        <div
          style={{
            display: 'flex',
            alignItems: 'center',
            flexWrap: 'wrap',
            gap: spacing.xs,
          }}
        >
          {queries}
        </div>
      )}
      {actions && (
        <div
          style={{
            display: 'flex',
            alignItems: 'center',
            flexWrap: 'wrap',
            gap: spacing.xs,
          }}
        >
          {(hasFilters || hasQueries) && (
            <Divider type="vertical" style={{ marginInline: spacing.sm }} />
          )}
          {actions}
        </div>
      )}
    </div>
  )
}
