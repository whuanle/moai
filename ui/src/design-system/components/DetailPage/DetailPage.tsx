import { Button, ConfigProvider, Descriptions, Skeleton, Space, Typography } from 'antd'
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
          <ConfigProvider button={{ autoInsertSpace: false }}>
            <Space>
              {onBack && <Button onClick={onBack}>{t('ds.detail.back')}</Button>}
              {onEdit && <Button type="primary" onClick={onEdit}>{t('ds.detail.edit')}</Button>}
              {extra}
            </Space>
          </ConfigProvider>
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
