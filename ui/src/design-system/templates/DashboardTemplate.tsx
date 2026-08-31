import { AppstoreOutlined, UserOutlined, ApiOutlined } from '@ant-design/icons'
import { Col, Row } from 'antd'
import { useTranslation } from 'react-i18next'
import { Page, Card, StatCard } from '@/design-system'

export function DashboardTemplate() {
  const { t } = useTranslation()
  return (
    <Page title={t('ds.dash.title')} subtitle={t('ds.dash.subtitle')}>
      <Row gutter={[16, 16]}>
        <Col xs={24} sm={12} lg={6}>
          <StatCard title={t('ds.dash.users')} value={128} icon={<UserOutlined />} trend={12.5} />
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <StatCard title={t('ds.dash.apps')} value={32} icon={<AppstoreOutlined />} trend={-3} />
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <StatCard title={t('ds.dash.requests')} value={2048} icon={<ApiOutlined />} trend={15} />
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <StatCard title={t('ds.dash.tickets')} value={7} icon={<ApiOutlined />} />
        </Col>
      </Row>
      <Card title={t('ds.dash.chart')} style={{ marginTop: 16 }}>
        {t('ds.dash.chartPlaceholder')}
      </Card>
    </Page>
  )
}
