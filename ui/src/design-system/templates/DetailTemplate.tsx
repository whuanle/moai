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
