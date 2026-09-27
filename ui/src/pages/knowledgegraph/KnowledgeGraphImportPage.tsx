import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Card, Tabs } from 'antd'
import { RobotOutlined, CodeOutlined } from '@ant-design/icons'
import { spacing } from '@/design-system/theme'
import type { KnowledgeGraphDetail as GraphDetail } from '@/api/knowledgeGraph'
import { KnowledgeGraphAiImportForm } from './KnowledgeGraphAiImportForm'
import { KnowledgeGraphJsonImportForm } from './KnowledgeGraphJsonImportForm'

interface KnowledgeGraphImportPageProps {
  graph: GraphDetail
  /** 导入落库后回调（刷新详情/画布数据） */
  onChanged: () => void
}

/** 图谱导入页：AI 智能导入（文档提取 + 模型抽取）与 JSON 结构化导入（反序列化直接落库） */
export function KnowledgeGraphImportPage({ graph, onChanged }: KnowledgeGraphImportPageProps) {
  const { t } = useTranslation()
  const [refreshKey, setRefreshKey] = useState(0)
  const graphId = Number(graph.kgId)
  const teamId = Number(graph.teamId)

  const handleImported = () => {
    setRefreshKey((k) => k + 1)
    onChanged()
  }

  return (
    <Card styles={{ body: { padding: spacing.lg } }}>
      <Tabs
        items={[
          {
            key: 'ai',
            label: (
              <span>
                <RobotOutlined /> {t('knowledgegraph.importPage.aiTab')}
              </span>
            ),
            children: <KnowledgeGraphAiImportForm key={refreshKey} teamId={teamId} graphId={graphId} onImported={handleImported} />,
          },
          {
            key: 'json',
            label: (
              <span>
                <CodeOutlined /> {t('knowledgegraph.importPage.jsonTab')}
              </span>
            ),
            children: <KnowledgeGraphJsonImportForm graphId={graphId} onImported={handleImported} />,
          },
        ]}
      />
    </Card>
  )
}
