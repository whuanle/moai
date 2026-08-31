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
