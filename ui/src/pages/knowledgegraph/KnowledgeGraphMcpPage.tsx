import { useEffect, useState } from 'react'
import { Card, Button, Input, Space, Tag, Typography } from 'antd'
import { CopyOutlined } from '@ant-design/icons'
import { useTranslation } from 'react-i18next'
import { feedback } from '@/design-system'
import { spacing } from '@/design-system/theme'
import { getServerInfo } from '@/api/auth'

const { Text } = Typography

const MCP_TOOLS = ['list_knowledge_graphs', 'get_knowledge_graph_schema', 'search_knowledge_graph_nodes', 'search_knowledge_graph_recall']

/**
 * 知识图谱「MCP」页：展示该图谱的 MCP 服务器地址（streamable HTTP，无状态），
 * 供外部 MCP 客户端接入；鉴权携带勾选「知识图谱 MCP」范围的团队/应用接入 key。
 * 地址取 serverinfo 的 serviceUrl（对外完整 URL），未取到时回退当前页面 origin。
 */
export function KnowledgeGraphMcpPage({ kgId }: { kgId: number }) {
  const { t } = useTranslation()
  const [address, setAddress] = useState('')

  useEffect(() => {
    let cancelled = false
    const fallback = `${window.location.origin.replace(/\/$/, '')}/api/external/knowledge-graph/${kgId}/mcp`
    getServerInfo()
      .then((info) => {
        if (cancelled) return
        const base = info?.serviceUrl?.trim()?.replace(/\/$/, '')
        setAddress(base ? `${base}/api/external/knowledge-graph/${kgId}/mcp` : fallback)
      })
      .catch(() => {
        if (cancelled) return
        setAddress(fallback)
      })
    return () => {
      cancelled = true
    }
  }, [kgId])

  const copyAddress = async () => {
    try {
      await navigator.clipboard.writeText(address)
      feedback.success(t('common.copySuccess'))
    } catch {
      feedback.error(t('common.copyFailed'))
    }
  }

  return (
    <Card>
      <Space direction="vertical" size={spacing.md} style={{ display: 'flex' }}>
        <div>
          <Text strong style={{ fontSize: 13 }}>{t('knowledgegraph.mcpAddress')}</Text>
          <Space.Compact style={{ width: '100%', marginTop: spacing.xs }}>
            <Input readOnly value={address} placeholder={t('knowledgegraph.mcpAddressPlaceholder')} />
            <Button icon={<CopyOutlined />} disabled={!address} onClick={() => void copyAddress()} aria-label={t('common.copy')} />
          </Space.Compact>
        </div>
        <div>
          <Text strong style={{ fontSize: 13 }}>{t('knowledgegraph.mcpTools')}</Text>
          <div style={{ marginTop: spacing.xs }}>
            {MCP_TOOLS.map((name) => (
              <Tag key={name} style={{ fontFamily: 'monospace' }}>{name}</Tag>
            ))}
          </div>
        </div>
        <Text type="secondary" style={{ fontSize: 12 }}>{t('knowledgegraph.mcpAuthHint')}</Text>
      </Space>
    </Card>
  )
}
