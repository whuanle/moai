import { useEffect, useState } from 'react'
import { Avatar, Descriptions, Modal, Space, Tag, Typography, theme } from 'antd'
import { useTranslation } from 'react-i18next'
import { Card as DSCard } from '@/design-system'
import { spacing } from '@/design-system/theme'
import { resolveStorageUrl } from '@/utils/storage'
import { getTeamPlugins, type TeamPluginItemType } from '@/api/team-plugin'
import { getWikis, type WikiItem } from '@/api/wiki'
import { getKnowledgeGraphs, type KnowledgeGraphItem } from '@/api/knowledgeGraph'
import type { TeamAgentDetail } from '@/api/team-agent'

const { Paragraph, Text } = Typography

interface NameMaps {
  plugins: Map<string, string>
  wikis: Map<string, string>
  graphs: Map<string, string>
}

/** 团队智能体只读详情：主智能体信息 + 子智能体定义列表；依赖 id 经选项映射为名称 */
export function TeamAgentDetailModal({
  open,
  detail,
  teamId,
  onClose,
}: {
  open: boolean
  detail: TeamAgentDetail | null
  teamId: number
  onClose: () => void
}) {
  const { t } = useTranslation()
  const { token } = theme.useToken()
  const [maps, setMaps] = useState<NameMaps>({ plugins: new Map(), wikis: new Map(), graphs: new Map() })

  useEffect(() => {
    if (!open) return
    let cancelled = false
    ;(async () => {
      try {
        const [plugins, wikis, graphs] = await Promise.all([
          getTeamPlugins(teamId),
          getWikis(teamId),
          getKnowledgeGraphs(teamId),
        ])
        if (cancelled) return
        setMaps({
          plugins: new Map(
            ((plugins.items ?? []) as TeamPluginItemType[])
              .filter((x) => x.pluginId)
              .map((x) => [String(x.pluginId), x.title || x.pluginName || String(x.pluginId)]),
          ),
          wikis: new Map(
            ((wikis.items ?? []) as WikiItem[]).filter((x) => x.wikiId != null).map((x) => [String(x.wikiId), x.name || String(x.wikiId)]),
          ),
          graphs: new Map(
            ((graphs.items ?? []) as KnowledgeGraphItem[])
              .filter((x) => x.kgId != null)
              .map((x) => [String(x.kgId), x.name || String(x.kgId)]),
          ),
        })
      } catch {
        // 错误已由全局请求中间件统一提示；名称映射失败仅影响展示
      }
    })()
    return () => {
      cancelled = true
    }
  }, [open, teamId])

  const renderDepTags = (ids: string[] | null | undefined, map: Map<string, string>, emptyText: string) => {
    const list = ids ?? []
    if (list.length === 0) return <Text type="secondary">{emptyText}</Text>
    return (
      <Space size={4} wrap>
        {list.map((id) => (
          <Tag key={id}>{map.get(String(id)) ?? String(id)}</Tag>
        ))}
      </Space>
    )
  }

  return (
    <Modal
      open={open}
      title={t('teamAgents.detailTitle', { name: detail?.name ?? '' })}
      onCancel={onClose}
      footer={null}
      width={760}
      destroyOnHidden
      maskClosable={false}
    >
      {detail && (
        <Space direction="vertical" size={spacing.md} style={{ width: '100%' }}>
          <Descriptions column={2} size="small">
            <Descriptions.Item label={t('teamAgents.name')} span={2}>
              <Space size={spacing.sm}>
                <Avatar shape="square" size={28} src={resolveStorageUrl(detail.avatarObjectKey ?? null) || undefined}>
                  {(detail.name || '?').slice(0, 1).toUpperCase()}
                </Avatar>
                <Text code style={{ fontSize: 12 }}>{detail.key}</Text>
                {detail.name || '-'}
              </Space>
            </Descriptions.Item>
            <Descriptions.Item label={t('teamAgents.status')} span={1}>
              <Space size={4}>
                {detail.enabled ? <Tag color="success">{t('teamAgents.statusEnabled')}</Tag> : <Tag>{t('teamAgents.statusDisabled')}</Tag>}
                {detail.isSystem && <Tag color="purple">{t('teamAgents.builtin')}</Tag>}
              </Space>
            </Descriptions.Item>
            <Descriptions.Item label={t('teamAgents.colSubCount')} span={1}>
              {detail.subs?.length ?? 0}
            </Descriptions.Item>
            <Descriptions.Item label={t('teamAgents.plugins')} span={1}>
              {renderDepTags(detail.mainPluginIds, maps.plugins, t('teamAgents.depsEmpty'))}
            </Descriptions.Item>
            <Descriptions.Item label={t('teamAgents.wikis')} span={1}>
              {renderDepTags(detail.mainWikiIds, maps.wikis, t('teamAgents.depsEmpty'))}
            </Descriptions.Item>
            <Descriptions.Item label={t('teamAgents.graphs')} span={2}>
              {renderDepTags(detail.mainGraphIds, maps.graphs, t('teamAgents.depsEmpty'))}
            </Descriptions.Item>
          </Descriptions>

          <DSCard title={t('teamAgents.mainPromptTitle')} styles={{ body: { paddingTop: spacing.sm } }}>
            <Paragraph style={{ whiteSpace: 'pre-wrap', marginBottom: 0, maxHeight: 220, overflow: 'auto' }}>
              {detail.mainPrompt || '-'}
            </Paragraph>
          </DSCard>

          <DSCard title={`${t('teamAgents.subSectionTitle')}（${detail.subs?.length ?? 0}）`} styles={{ body: { paddingTop: spacing.sm } }}>
            {(detail.subs ?? []).length === 0 ? (
              <Text type="secondary">{t('teamAgents.subsEmpty')}</Text>
            ) : (
              <Space direction="vertical" size={spacing.sm} style={{ width: '100%' }}>
                {(detail.subs ?? []).map((sub) => (
                  <div
                    key={sub.key}
                    style={{ background: token.colorFillQuaternary, borderRadius: 8, padding: `${spacing.sm}px ${spacing.md}px` }}
                  >
                    <Space size={spacing.sm} wrap align="center">
                      <Text strong>{sub.name}</Text>
                      <Text code style={{ fontSize: 12 }}>{sub.key}</Text>
                    </Space>
                    <div style={{ marginTop: 4 }}>
                      <Text type="secondary">{sub.description || '-'}</Text>
                    </div>
                    <div style={{ marginTop: 4 }}>
                      <Text style={{ fontSize: 12, whiteSpace: 'pre-wrap' }}>{sub.prompt}</Text>
                    </div>
                    <div style={{ marginTop: 6 }}>
                      <Space size={4} wrap>
                        {renderDepTags(sub.pluginIds, maps.plugins, '')}
                        {renderDepTags(sub.wikiIds, maps.wikis, '')}
                        {renderDepTags(sub.graphIds, maps.graphs, '')}
                        {(sub.pluginIds?.length ?? 0) === 0 && (sub.wikiIds?.length ?? 0) === 0 && (sub.graphIds?.length ?? 0) === 0 && (
                          <Text type="secondary" style={{ fontSize: 12 }}>{t('teamAgents.depsEmpty')}</Text>
                        )}
                      </Space>
                    </div>
                  </div>
                ))}
              </Space>
            )}
          </DSCard>
        </Space>
      )}
    </Modal>
  )
}
