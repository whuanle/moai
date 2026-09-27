import { useCallback } from 'react'
import { useNavigate, useSearchParams } from 'react-router'
import { useTranslation } from 'react-i18next'
import { Card, Steps } from 'antd'
import { spacing } from '@/design-system/theme'
import type { KnowledgeGraphDetail as GraphDetail } from '@/api/knowledgeGraph'
import { KnowledgeGraphEntities } from './KnowledgeGraphEntities'
import { KnowledgeGraphRelations } from './KnowledgeGraphRelations'
import { KnowledgeGraphSchema } from './KnowledgeGraphSchema'

const MAINTENANCE_STEPS = ['schema', 'entities', 'relations'] as const
type MaintenanceStep = (typeof MAINTENANCE_STEPS)[number]

function isStep(value: string | null): value is MaintenanceStep {
  return value !== null && (MAINTENANCE_STEPS as readonly string[]).includes(value)
}

interface KnowledgeGraphMaintenanceProps {
  graph: GraphDetail
}

/** 图谱维护：头部步骤条承载 模型 → 实例 → 关系 三步，点步骤直接切换维护内容 */
export function KnowledgeGraphMaintenance({ graph }: KnowledgeGraphMaintenanceProps) {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const [searchParams] = useSearchParams()
  const graphId = Number(graph.kgId)
  const teamId = Number(graph.teamId)

  const rawStep = searchParams.get('step')
  const step: MaintenanceStep = isStep(rawStep) ? rawStep : 'schema'

  // 步骤间跳转保留无关参数（typeId/nodeId/relationTypeId 过滤条件跟随目标页语义），仅换 step
  const gotoStep = useCallback(
    (next: MaintenanceStep) => {
      const params = new URLSearchParams(searchParams)
      params.set('step', next)
      navigate(`/team/${teamId}/kg/${graphId}/maintenance?${params.toString()}`, { replace: true })
    },
    [searchParams, navigate, teamId, graphId],
  )

  return (
    <div>
      <Steps
        size="small"
        type="navigation"
        style={{ marginBottom: spacing.md }}
        current={MAINTENANCE_STEPS.indexOf(step)}
        onChange={(current) => gotoStep(MAINTENANCE_STEPS[current])}
        items={[
          { title: t('knowledgegraph.guide.step1') },
          { title: t('knowledgegraph.guide.step2') },
          { title: t('knowledgegraph.guide.step3') },
        ]}
      />
      {step === 'schema' ? (
        <Card styles={{ body: { padding: spacing.lg } }}>
          <KnowledgeGraphSchema graphId={graphId} teamId={teamId} myRole={graph.myRole ?? null} mode={graph.mode} />
        </Card>
      ) : step === 'entities' ? (
        <Card styles={{ body: { padding: spacing.lg } }}>
          <KnowledgeGraphEntities
            graphId={graphId}
            teamId={teamId}
            graphEnabled={graph.enabled !== false}
            myRole={graph.myRole ?? null}
          />
        </Card>
      ) : (
        <Card styles={{ body: { padding: spacing.lg } }}>
          <KnowledgeGraphRelations
            graphId={graphId}
            teamId={teamId}
            graphEnabled={graph.enabled !== false}
            myRole={graph.myRole ?? null}
          />
        </Card>
      )}
    </div>
  )
}
