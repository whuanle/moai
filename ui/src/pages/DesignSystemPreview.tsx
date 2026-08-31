import { Alert, Card, Divider, Space } from 'antd'
import { useTranslation } from 'react-i18next'
import { ListTemplate } from '@/design-system/templates/ListTemplate'
import { FormTemplate } from '@/design-system/templates/FormTemplate'
import { DetailTemplate } from '@/design-system/templates/DetailTemplate'
import { DashboardTemplate } from '@/design-system/templates/DashboardTemplate'
import { ChatTemplate } from '@/design-system/templates/ChatTemplate'

const sections = [
  { key: 'list', label: '列表页', component: <ListTemplate /> },
  { key: 'form', label: '表单页', component: <FormTemplate /> },
  { key: 'detail', label: '详情页', component: <DetailTemplate /> },
  { key: 'dashboard', label: '概览页面', component: <DashboardTemplate /> },
  { key: 'chat', label: '对话页', component: <ChatTemplate /> },
]

export function DesignSystemPreview() {
  const { t } = useTranslation()
  return (
    <div style={{ padding: 24 }}>
      <Alert
        type="info"
        showIcon
        message={t('ds.preview.title')}
        description={t('ds.preview.desc')}
        style={{ marginBottom: 24 }}
      />
      {sections.map((section) => (
        <Card key={section.key} title={section.label} style={{ marginBottom: 24 }}>
          {section.component}
        </Card>
      ))}
      <Divider />
      <Space style={{ color: '#999' }}>{t('ds.preview.footer')}</Space>
    </div>
  )
}
