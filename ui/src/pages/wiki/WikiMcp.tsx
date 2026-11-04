import { useCallback, useEffect, useState } from 'react'
import { Button, Input, Space, Switch, Typography } from 'antd'
import { CopyOutlined } from '@ant-design/icons'
import { useTranslation } from 'react-i18next'
import { feedback } from '@/design-system'
import { spacing } from '@/design-system/theme'
import { getServerInfo } from '@/api/auth'
import { getWikiMcpConfig, updateWikiMcpConfig, type WikiMcpConfig } from '@/api/wiki'
import { ResourceKeyCard } from '@/pages/teams/apps/ResourceKeyCard'

const { Text } = Typography

interface WikiMcpProps {
  wikiId: number
  /** 归属团队 id */
  teamId?: number
  /** 团队 Admin+ 才能开启 MCP 与管理资源 key */
  canManage?: boolean
}

/**
 * 知识库「MCP」页：开启 MCP 接入（开启时后端自动生成/复用该库唯一的团队资源 key），
 * 展示该知识库的 MCP 服务器地址（streamable HTTP，无状态）与资源 key，供外部 MCP 客户端复制接入。
 * 地址取 serverinfo 的 serviceUrl（对外完整 URL），未取到时回退当前页面 origin。
 */
export function WikiMcp({ wikiId, canManage }: WikiMcpProps) {
  const { t } = useTranslation()
  const [address, setAddress] = useState('')
  const [config, setConfig] = useState<WikiMcpConfig | null>(null)

  const load = useCallback(async () => {
    if (!canManage) return
    try {
      setConfig(await getWikiMcpConfig(wikiId))
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }, [wikiId, canManage])

  useEffect(() => {
    if (canManage) void load()
  }, [canManage, load])

  useEffect(() => {
    let cancelled = false
    const fallback = `${window.location.origin.replace(/\/$/, '')}/api/teamres/wiki/${wikiId}/mcp`
    getServerInfo()
      .then((info) => {
        if (cancelled) return
        const base = info?.serviceUrl?.trim()?.replace(/\/$/, '')
        setAddress(base ? `${base}/api/teamres/wiki/${wikiId}/mcp` : fallback)
      })
      .catch(() => {
        if (cancelled) return
        setAddress(fallback)
      })
    return () => {
      cancelled = true
    }
  }, [wikiId])

  const handleToggle = async (checked: boolean) => {
    try {
      setConfig(await updateWikiMcpConfig(wikiId, checked))
      feedback.success(checked ? t('wiki.mcpEnabled') : t('wiki.mcpDisabled'))
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }

  const copyAddress = async () => {
    try {
      await navigator.clipboard.writeText(address)
      feedback.success(t('common.copySuccess'))
    } catch {
      feedback.error(t('common.copyFailed'))
    }
  }

  return (
    <Space direction="vertical" size={spacing.md} style={{ display: 'flex' }}>
      <div>
        <Text strong style={{ fontSize: 13 }}>{t('wiki.mcpEnable')}</Text>
        <Switch checked={config?.isMcpEnabled ?? false} onChange={(v) => void handleToggle(v)} style={{ marginLeft: spacing.sm }} />
      </div>
      <div>
        <Text strong style={{ fontSize: 13 }}>{t('wiki.mcpAddress')}</Text>
        <Space.Compact style={{ width: '100%', marginTop: spacing.xs }}>
          <Input readOnly value={address} placeholder={t('wiki.mcpAddressPlaceholder')} />
          <Button icon={<CopyOutlined />} disabled={!address} onClick={() => void copyAddress()} aria-label={t('common.copy')} />
        </Space.Compact>
      </div>
      <ResourceKeyCard
        featureEnabled={config?.isMcpEnabled ?? false}
        canManage={canManage ?? false}
        keyInfo={config ? { teamResourceKeyId: config.teamResourceKeyId, key: config.key, keyPrefix: config.keyPrefix, status: config.status } : null}
        onChanged={() => void load()}
        hintKey="teamresKey.hintWiki"
      />
      <Text type="secondary" style={{ fontSize: 12 }}>{t('wiki.mcpAuthHint')}</Text>
    </Space>
  )
}
