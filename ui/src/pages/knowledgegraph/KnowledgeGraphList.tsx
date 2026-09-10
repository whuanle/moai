import { useCallback, useEffect, useState } from 'react'
import { useNavigate } from 'react-router'
import { useTranslation } from 'react-i18next'
import { Alert, Empty } from 'antd'
import { Card, Page } from '@/design-system'
import { fontSize, neutralColors, spacing } from '@/design-system/theme'
import { useAppStore } from '@/store/app'
import { getMyTeams, type TeamItem } from '@/api/team'
import { getKnowledgeGraphs, type KnowledgeGraphItem } from '@/api/knowledgeGraph'

interface CardItem extends KnowledgeGraphItem {
  teamName?: string
  myRole?: number | null
  graphEnabled?: boolean
}

export function KnowledgeGraphList() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const myTeams = useAppStore((state) => state.myTeams)
  const setMyTeams = useAppStore((state) => state.setMyTeams)
  const [items, setItems] = useState<CardItem[]>([])
  const [enabled, setEnabled] = useState(true)

  const load = useCallback(async () => {
    let teams: TeamItem[]
    if (myTeams.length > 0) {
      teams = myTeams
    } else {
      try {
        teams = await getMyTeams()
        setMyTeams(teams)
      } catch {
        teams = []
      }
    }
    const collected: CardItem[] = []
    let anyEnabled = false
    let queried = false
    for (const team of teams) {
      const teamId = Number(team.teamId)
      if (!teamId) continue
      queried = true
      try {
        const res = await getKnowledgeGraphs(teamId)
        if (res.enabled) anyEnabled = true
        for (const g of res.items ?? []) {
          collected.push({ ...g, teamName: team.name ?? undefined, myRole: res.myRole, graphEnabled: res.enabled ?? false })
        }
      } catch {
        // 单个团队失败不中断
      }
    }
    setEnabled(!queried || anyEnabled)
    setItems(collected)
  }, [myTeams, setMyTeams])

  useEffect(() => { void load() }, [load])

  return (
    <Page>
      {!enabled && (
        <Alert type="warning" showIcon message={t('knowledgegraph.disabled')} style={{ marginBottom: spacing.md }} />
      )}
      {items.length === 0 ? (
        <Empty description={t('knowledgegraph.empty')} />
      ) : (
        <div
          style={{
            display: 'grid',
            gridTemplateColumns: 'repeat(auto-fill, minmax(260px, 1fr))',
            gap: spacing.md,
          }}
        >
          {items.map((item) => (
            <Card
              key={String(item.kgId)}
              hoverable
              style={{ cursor: 'pointer' }}
              onClick={() => navigate(`/team/${item.teamId}/kg/${item.kgId}/entities`)}
            >
              <div style={{ fontSize: fontSize.lg, fontWeight: 600 }}>{item.name}</div>
              <div style={{ color: neutralColors.textSecondary, marginTop: spacing.xs }}>{item.description || '-'}</div>
              <div style={{ color: neutralColors.textSecondary, marginTop: spacing.sm, fontSize: fontSize.xs }}>{item.teamName}</div>
            </Card>
          ))}
        </div>
      )}
    </Page>
  )
}
