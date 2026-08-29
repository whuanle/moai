import { Card, Col, Row, Typography } from 'antd'
import { useTranslation } from 'react-i18next'

const { Title, Paragraph } = Typography

export function Dashboard() {
  const { t } = useTranslation()

  const features = [
    { title: t('home.feature1Title'), desc: t('home.feature1Desc') },
    { title: t('home.feature2Title'), desc: t('home.feature2Desc') },
    { title: t('home.feature3Title'), desc: t('home.feature3Desc') },
  ]

  return (
    <div>
      <Typography>
        <Title level={2}>{t('home.welcome')}</Title>
        <Paragraph type="secondary">{t('home.subtitle')}</Paragraph>
        <Paragraph>{t('home.description')}</Paragraph>
      </Typography>

      <Row gutter={[16, 16]} style={{ marginTop: 24 }}>
        {features.map((feature) => (
          <Col xs={24} md={8} key={feature.title}>
            <Card title={feature.title} hoverable>
              <Typography.Paragraph type="secondary">{feature.desc}</Typography.Paragraph>
            </Card>
          </Col>
        ))}
      </Row>
    </div>
  )
}
