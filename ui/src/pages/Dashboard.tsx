import { useEffect } from 'react'
import {
  ApiOutlined,
  AppstoreAddOutlined,
  AppstoreOutlined,
  BookOutlined,
  FileAddOutlined,
  TeamOutlined,
  UserAddOutlined,
} from '@ant-design/icons'
import { Button, Col, Empty, Row, Typography } from 'antd'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router'
import { refreshUserProfile } from '@/api/auth'
import { useAppStore } from '@/store/app'
import { Card, Page, StatCard } from '@/design-system'

export function Dashboard() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const userName = useAppStore((state) => state.userInfo?.nickName ?? state.userInfo?.userName)

  useEffect(() => {
    void refreshUserProfile().catch(() => undefined)
  }, [])

  const stats = [
    { title: t('dashboard.statApps'), value: 12, icon: <AppstoreOutlined />, trend: 8 },
    { title: t('dashboard.statWikis'), value: 8, icon: <BookOutlined />, trend: 12 },
    { title: t('dashboard.statTeam'), value: 24, icon: <TeamOutlined />, trend: 4 },
    { title: t('dashboard.statRequests'), value: 2048, icon: <ApiOutlined />, trend: 22 },
  ]

  const quickActions = [
    {
      icon: <AppstoreAddOutlined />,
      title: t('dashboard.quickApp'),
      desc: t('dashboard.quickAppDesc'),
      path: '/app',
    },
    {
      icon: <FileAddOutlined />,
      title: t('dashboard.quickWiki'),
      desc: t('dashboard.quickWikiDesc'),
      path: '/wiki',
    },
    {
      icon: <UserAddOutlined />,
      title: t('dashboard.quickTeam'),
      desc: t('dashboard.quickTeamDesc'),
      path: '/team',
    },
  ]

  return (
    <Page
      title={t('dashboard.title')}
      subtitle={t('dashboard.subtitle', { name: userName ?? t('app.name') })}
      extra={
        <Button type="primary" icon={<AppstoreAddOutlined />} onClick={() => navigate('/app')}>
          {t('dashboard.quickApp')}
        </Button>
      }
    >
      <div
        style={{
          position: 'relative',
          overflow: 'hidden',
          borderRadius: 12,
          padding: '32px 32px 28px',
          marginBottom: 24,
          background:
            'linear-gradient(124deg, #2A6FFF 0%, #3D8BFF 45%, #68A9FF 100%)',
          color: '#FFFFFF',
        }}
      >
        <Typography.Title level={3} style={{ color: '#FFFFFF', marginBottom: 8 }}>
          {t('dashboard.welcomeTitle')}
        </Typography.Title>
        <Typography.Paragraph style={{ color: 'rgba(255,255,255,0.9)', maxWidth: 620, marginBottom: 20 }}>
          {t('dashboard.welcomeDesc')}
        </Typography.Paragraph>
        <Button size="large" style={{ background: '#FFFFFF', color: '#2970FF', fontWeight: 600 }}>
          {t('home.getStarted')}
        </Button>
      </div>

      <Row gutter={[16, 16]}>
        {stats.map((stat) => (
          <Col xs={24} sm={12} lg={6} key={stat.title}>
            <StatCard title={stat.title} value={stat.value} icon={stat.icon} trend={stat.trend} />
          </Col>
        ))}
      </Row>

      <Row gutter={[16, 16]} style={{ marginTop: 16 }}>
        <Col xs={24} lg={12}>
          <Card title={t('dashboard.quickActions')}>
            <Row gutter={[12, 12]}>
              {quickActions.map((action) => (
                <Col xs={24} key={action.title}>
                  <div
                    onClick={() => navigate(action.path)}
                    style={{
                      display: 'flex',
                      alignItems: 'center',
                      gap: 12,
                      padding: '12px 16px',
                      borderRadius: 8,
                      cursor: 'pointer',
                      border: '1px solid rgba(16, 24, 40, 0.08)',
                      background: '#F9FAFB',
                      transition: 'border-color 0.2s, background 0.2s',
                    }}
                  >
                    <span style={{ fontSize: 22, color: '#2970FF' }}>{action.icon}</span>
                    <div>
                      <Typography.Text strong>{action.title}</Typography.Text>
                      <br />
                      <Typography.Text type="secondary">{action.desc}</Typography.Text>
                    </div>
                  </div>
                </Col>
              ))}
            </Row>
          </Card>
        </Col>

        <Col xs={24} lg={12}>
          <Card
            title={t('dashboard.recentTitle')}
            extra={
              <Button type="link" size="small" onClick={() => navigate('/app')}>
                {t('dashboard.viewAll')}
              </Button>
            }
          >
            <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={t('dashboard.recentEmpty')} />
          </Card>
        </Col>
      </Row>
    </Page>
  )
}
