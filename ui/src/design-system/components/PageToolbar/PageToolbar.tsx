import type { ReactNode } from 'react'
import { spacing } from '@/design-system/theme'

export interface PageToolbarProps {
  /**
   * 左侧筛选栏内容（类型/标签/排序/搜索等）。缺省时 actions 自动左移。
   */
  filters?: ReactNode
  /**
   * 操作按钮组（新建、Snippets 等）。有 filters 时靠右，无 filters 时靠左。
   */
  actions?: ReactNode
}

/**
 * 页头工具栏，统一 Dify 布局规则：
 * - 有筛选栏：筛选栏在左、操作按钮在右。
 * - 无筛选栏：操作按钮直接靠左。
 */
export function PageToolbar({ filters, actions }: PageToolbarProps) {
  const hasFilters = Boolean(filters)
  return (
    <div
      style={{
        display: 'flex',
        alignItems: 'center',
        gap: spacing.md,
        marginBottom: spacing.lg,
      }}
    >
      {hasFilters && (
        <div
          style={{
            display: 'flex',
            alignItems: 'center',
            gap: spacing.xs,
            flex: 1,
            minWidth: 0,
            flexWrap: 'wrap',
          }}
        >
          {filters}
        </div>
      )}
      {actions && (
        <div
          style={{
            display: 'flex',
            alignItems: 'center',
            flexWrap: 'wrap',
            gap: spacing.xs,
            flexShrink: 0,
            marginInlineStart: hasFilters ? 'auto' : 0,
          }}
        >
          {actions}
        </div>
      )}
    </div>
  )
}
