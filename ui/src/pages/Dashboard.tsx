import { useEffect, useMemo, useState } from 'react'
import {
  ApiOutlined,
  AppstoreAddOutlined,
  AppstoreOutlined,
  BookOutlined,
  ClusterOutlined,
  RightOutlined,
  TeamOutlined,
  UserAddOutlined,
} from '@ant-design/icons'
import { Avatar, Button, Col, Empty, List, Row, Spin, Typography, theme } from 'antd'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router'
import { refreshUserProfile } from '@/api/auth'
import { useAppStore } from '@/store/app'
import { brandGradient, Card, Page, StatCard } from '@/design-system'
import { spacing } from '@/design-system/theme'
import { resolveStorageUrl } from '@/utils/storage'
import { getMyTeams, type TeamItem } from '@/api/team'
import { getWikis, type WikiCardItem } from '@/api/wiki'
import { getKnowledgeGraphs, type KnowledgeGraphItem } from '@/api/knowledgeGraph'
import { getPublicApps } from '@/api/app'

/** 知识图谱卡片项（前端聚合：合并所属团队名） */
interface GraphCardItem extends KnowledgeGraphItem {
  teamName?: string | null
}

/** 首页：真实统计（我的团队/知识库/图谱/市场应用）+ 我加入团队的知识库与知识图谱列表 */
export function Dashboard() {
  const { t } = useTranslation()
  const { token } = theme.useToken()
  const navigate = useNavigate()
  const userName = useAppStore((state) => state.userInfo?.nickName ?? state.userInfo?.userName)

  const [loading, setLoading] = useState(true)
  const [teams, setTeams] = useState<TeamItem[]>([])
  const [wikis, setWikis] = useState<WikiCardItem[]>([])
  const [graphs, setGraphs] = useState<GraphCardItem[]>([])
  const [plazaAppCount, setPlazaAppCount] = useState(0)

  useEffect(() => {
    void refreshUserProfile().catch(() => undefined)
  }, [])

  useEffect(() => {
    let cancelled = false
    void (async () => {
      setLoading(true)
      try {
        // 市场应用数（一次调用）；知识库/知识图谱按我加入的团队逐一聚合（仅团队成员可见的分区数据源）
        const [myTeams, plazaApps] = await Promise.all([getMyTeams(), getPublicApps().catch(() => [])])
        if (cancelled) return
        setTeams(myTeams)
        setPlazaAppCount(plazaApps.length)

        const wikiResults = await Promise.all(
          myTeams.map(async (team) => {
            const teamId = Number(team.teamId)
            if (!Number.isFinite(teamId) || teamId <= 0) return []
            try {
              const res = await getWikis(teamId)
              return (res.items ?? []).map((item) => ({ ...item, teamName: team.name, myRole: res.myRole }))
            } catch {
              return []
            }
          }),
        )
        if (cancelled) return
        setWikis(wikiResults.flat())

        const graphResults = await Promise.all(
          myTeams.map(async (team) => {
            const teamId = Number(team.teamId)
            if (!Number.isFinite(teamId) || teamId <= 0) return []
            try {
              const res = await getKnowledgeGraphs(teamId)
              return (res.items ?? []).map((item) => ({ ...item, teamName: team.name }))
            } catch {
              return []
            }
          }),
        )
        if (cancelled) return
        setGraphs(graphResults.flat())
      } catch {
        // 错误已由全局请求中间件统一提示
      } finally {
        if (!cancelled) setLoading(false)
      }
    })()
    return () => {
      cancelled = true
    }
  }, [])

  const stats = useMemo(
    () => [
      { title: t('dashboard.statTeam'), value: teams.length, icon: <TeamOutlined /> },
      { title: t('dashboard.statWikis'), value: wikis.length, icon: <BookOutlined /> },
      { title: t('dashboard.statGraphs'), value: graphs.length, icon: <ClusterOutlined /> },
      { title: t('dashboard.statPlazaApps'), value: plazaAppCount, icon: <ApiOutlined /> },
    ],
    [teams.length, wikis.length, graphs.length, plazaAppCount, t],
  )

  // 知识库/知识图谱入口统一收敛到团队详情分区，仪表盘不再提供全局快捷入口
  const quickActions = [
    {
      icon: <AppstoreAddOutlined />,
      title: t('dashboard.quickApp'),
      desc: t('dashboard.quickAppDesc'),
      path: '/team',
    },
    {
      icon: <UserAddOutlined />,
      title: t('dashboard.quickTeam'),
      desc: t('dashboard.quickTeamDesc'),
      path: '/team',
    },
  ]

  const renderResourceItem = (
    key: string,
    avatarPath: string | null | undefined,
    name: string,
    teamName: string | null | undefined,
    onClick: () => void,
  ) => (
    <List.Item
      key={key}
      style={{ cursor: 'pointer', padding: `${spacing.sm}px 0` }}
      onClick={onClick}
    >
      <div style={{ display: 'flex', alignItems: 'center', gap: spacing.sm, minWidth: 0, flex: 1 }}>
        <Avatar shape="square" size={32} src={avatarPath ? resolveStorageUrl(avatarPath) : undefined}>
          {name.slice(0, 1).toUpperCase()}
        </Avatar>
        <div style={{ minWidth: 0, flex: 1 }}>
          <div
            style={{
              fontWeight: 500,
              whiteSpace: 'nowrap',
              overflow: 'hidden',
              textOverflow: 'ellipsis',
            }}
          >
            {name}
          </div>
          <Typography.Text type="secondary" style={{ fontSize: 12 }}>
            {teamName || '-'}
          </Typography.Text>
        </div>
        <RightOutlined style={{ color: token.colorTextQuaternary, fontSize: 12 }} />
      </div>
    </List.Item>
  )

  return (
    <Page
      title={t('dashboard.title')}
      subtitle={t('dashboard.subtitle', { name: userName ?? t('app.name') })}
      extra={
        <Button type="primary" icon={<AppstoreAddOutlined />} onClick={() => navigate('/team')}>
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
          background: brandGradient,
          color: '#FFFFFF',
        }}
      >
        <Typography.Title level={3} style={{ color: '#FFFFFF', marginBottom: 8 }}>
          {t('dashboard.welcomeTitle')}
        </Typography.Title>
        <Typography.Paragraph style={{ color: 'rgba(255,255,255,0.9)', maxWidth: 620, marginBottom: 20 }}>
          {t('dashboard.welcomeDesc')}
        </Typography.Paragraph>
        <Button size="large" style={{ background: '#FFFFFF', color: token.colorPrimary, fontWeight: 600 }}>
          {t('home.getStarted')}
        </Button>
      </div>

      <Row gutter={[16, 16]}>
        {stats.map((stat) => (
          <Col xs={24} sm={12} lg={6} key={stat.title}>
            <StatCard title={stat.title} value={stat.value} icon={stat.icon} />
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
                      border: `1px solid ${token.colorBorderSecondary}`,
                      background: token.colorFillQuaternary,
                      transition: 'border-color 0.2s, background 0.2s',
                    }}
                  >
                    <span style={{ fontSize: 22, color: token.colorPrimary }}>{action.icon}</span>
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
            title={t('dashboard.myWikis')}
            extra={
              <Button type="link" size="small" onClick={() => navigate('/team')}>
                {t('dashboard.viewAll')}
              </Button>
            }
          >
            <Spin spinning={loading}>
              {!loading && wikis.length === 0 ? (
                <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={t('dashboard.resourceEmpty')} />
              ) : (
                <List
                  dataSource={wikis.slice(0, 6)}
                  rowKey={(item) => `${item.teamId}-${item.wikiId}`}
                  renderItem={(item) =>
                    renderResourceItem(
                      String(item.wikiId ?? ''),
                      item.avatarPath,
                      item.name || '-',
                      item.teamName,
                      () => navigate(`/team/${item.teamId}/wiki/${item.wikiId}`),
                    )
                  }
                />
              )}
            </Spin>
          </Card>
        </Col>

        <Col xs={24} lg={12} xl={12}>
          <Card
            title={
              <span>
                <AppstoreOutlined style={{ marginRight: spacing.xs }} />
                {t('dashboard.myGraphs')}
              </span>
            }
            extra={
              <Button type="link" size="small" onClick={() => navigate('/team')}>
                {t('dashboard.viewAll')}
              </Button>
            }
          >
            <Spin spinning={loading}>
              {!loading && graphs.length === 0 ? (
                <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={t('dashboard.resourceEmpty')} />
              ) : (
                <List
                  dataSource={graphs.slice(0, 6)}
                  rowKey={(item) => `${item.teamId}-${item.kgId}`}
                  renderItem={(item) =>
                    renderResourceItem(
                      String(item.kgId ?? ''),
                      item.avatarPath,
                      item.name || '-',
                      item.teamName,
                      () => navigate(`/team/${item.teamId}/kg/${item.kgId}/canvas`),
                    )
                  }
                />
              )}
            </Spin>
          </Card>
        </Col>
      </Row>
    </Page>
  )
}
