import { MinusOutlined, RiseOutlined, FallOutlined } from '@ant-design/icons'
import { Card } from './Card'
import { Typography } from 'antd'
import type { ReactNode } from 'react'
import { neutralColors, spacing } from '@/design-system/theme'

export interface StatCardProps {
  title: ReactNode
  value: ReactNode
  icon?: ReactNode
  suffix?: ReactNode
  loading?: boolean
  trend?: number
}

export function StatCard({ title, value, icon, suffix, loading, trend }: StatCardProps) {
  const trendNode =
    typeof trend === 'number' ? (
      <span style={{ marginInlineStart: spacing.xs, fontSize: 13, color: neutralColors.textTertiary }}>
        {trend === 0 ? <MinusOutlined /> : trend > 0 ? <RiseOutlined /> : <FallOutlined />}
        {Math.abs(trend)}%
      </span>
    ) : null

  return (
    <Card loading={loading}>
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <div style={{ minWidth: 0 }}>
          <Typography.Text type="secondary" style={{ fontSize: 13 }}>
            {title}
          </Typography.Text>
          <div style={{ marginTop: spacing.xs, fontSize: 24, fontWeight: 600 }}>
            {value}
            {suffix && <span style={{ marginInlineStart: spacing.xs, fontSize: 14 }}>{suffix}</span>}
          </div>
          {trendNode}
        </div>
        {icon && (
          <div
            style={{
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              width: 44,
              height: 44,
              borderRadius: 10,
              fontSize: 20,
              color: '#2970FF',
              background: '#EFF4FF',
              flexShrink: 0,
            }}
          >
            {icon}
          </div>
        )}
      </div>
    </Card>
  )
}
