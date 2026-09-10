import { useCallback, useEffect, useState } from 'react'
import {
  BookOutlined,
  GlobalOutlined,
} from '@ant-design/icons'
import { Avatar, Col, Empty, Row, Tag, Tooltip, Typography } from 'antd'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router'
import { Card, Page } from '@/design-system'
import { neutralColors, spacing } from '@/design-system/theme'
import { useAppStore } from '@/store/app'
import { getMyTeams, type TeamItem } from '@/api/team'
import { getWikis, type WikiCardItem } from '@/api/wiki'
import { formatDateTime } from '@/utils/datetime'

const { Paragraph } = Typography

/** 角色：0=Member 1=Admin 2=Owner（对齐后端 TeamRole 枚举） */
const ROLE_MEMBER = 0

export function Wiki() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const myTeams = useAppStore((state) => state.myTeams)
  const setMyTeams = useAppStore((state) => state.setMyTeams)

  const [loading, setLoading] = useState(true)
  const [cards, setCards] = useState<WikiCardItem[]>([])

  const load = useCallback(async () => {
    setLoading(true)
    try {
      const fetchedTeams: TeamItem[] = myTeams.length > 0 ? myTeams : await getMyTeams()
      // 同步到 store，供侧边栏等其它入口复用，保持最新
      if (myTeams.length === 0) setMyTeams(fetchedTeams)

      const items: WikiCardItem[] = []
      for (const team of fetchedTeams) {
        const teamId = Number(team.teamId)
        if (!Number.isFinite(teamId) || teamId <= 0) continue
        try {
          const res = await getWikis(teamId)
          for (const w of res.items ?? []) {
            items.push({
              ...w,
              teamName: team.name,
              myRole: res.myRole ?? team.myRole ?? ROLE_MEMBER,
            })
          }
        } catch {
          // 单个团队查询失败不中断整体聚合
        }
      }
      setCards(items)
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setLoading(false)
    }
  }, [myTeams, setMyTeams])

  useEffect(() => {
    void load()
  }, [load])

  return (
    <Page>
      {loading ? null : cards.length === 0 ? (
        <Empty description={t('wiki.empty')} />
      ) : (
        <Row gutter={[spacing.md, spacing.md]}>
          {cards.map((card) => {
            const name = card.name || '-'
            return (
              <Col xs={24} sm={12} md={8} lg={6} xxl={4} key={String(card.wikiId)}>
                <Card style={{ height: '100%', cursor: 'pointer' }} styles={{ body: { padding: spacing.md } }}>
                  <div
                    style={{ display: 'flex', flexDirection: 'column', gap: spacing.sm, height: '100%' }}
                    onClick={() => navigate(`/team/${card.teamId}/wiki/${card.wikiId}`)}
                  >
                    <div style={{ display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between' }}>
                      <div style={{ display: 'flex', alignItems: 'center', gap: spacing.sm, minWidth: 0 }}>
                        <Avatar shape="square" size={44} icon={<BookOutlined />} />
                        <div style={{ minWidth: 0, alignSelf: 'center' }}>
                          <div
                            style={{
                              fontWeight: 600,
                              fontSize: 15,
                              lineHeight: 1.4,
                              whiteSpace: 'nowrap',
                              overflow: 'hidden',
                              textOverflow: 'ellipsis',
                            }}
                          >
                            {name}
                          </div>
                          <div style={{ marginTop: 2 }}>
                            <Tag style={{ marginInlineEnd: 0 }}>{card.teamName || '-'}</Tag>
                          </div>
                        </div>
                      </div>
                      {card.isPublic && (
                        <Tooltip title={t('wiki.public')}>
                          <GlobalOutlined style={{ color: neutralColors.textTertiary }} aria-label={t('wiki.public')} />
                        </Tooltip>
                      )}
                    </div>
                    <Paragraph
                      type="secondary"
                      style={{ fontSize: 13, marginBottom: 0, minHeight: 38 }}
                      ellipsis={{ rows: 2 }}
                    >
                      {card.description || '-'}
                    </Paragraph>
                    <div
                      style={{
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'space-between',
                        gap: spacing.sm,
                        color: neutralColors.textTertiary,
                        fontSize: 12,
                        marginTop: 'auto',
                        borderTop: `1px solid ${neutralColors.border}`,
                        paddingTop: spacing.sm,
                      }}
                    >
                      <span>{t('wiki.colCreateTime')}: {formatDateTime(card.createTime)}</span>
                    </div>
                  </div>
                </Card>
              </Col>
            )
          })}
        </Row>
      )}
    </Page>
  )
}
