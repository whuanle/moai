import { useCallback, useEffect, useState } from 'react'
import { Button, Divider, Input, Space, Switch, Tag, Typography } from 'antd'
import { CopyOutlined } from '@ant-design/icons'
import { useTranslation } from 'react-i18next'
import { spacing } from '@/design-system/theme'
import { getServerInfo } from '@/api/auth'
import { getWikiMcpConfig, updateWikiMcpConfig, type WikiMcpConfig } from '@/api/wiki'
import { createTeamResourceKey } from '@/api/teamres-key'
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
export function WikiMcp({ wikiId, teamId, canManage }: WikiMcpProps) {
  const { t } = useTranslation()
  const [address, setAddress] = useState('')
  const [config, setConfig] = useState<WikiMcpConfig | null>(null)

  const load = useCallback(async () => {
    if (!canManage) return
    try {
      let cfg = await getWikiMcpConfig(wikiId)
      // 功能已开启但 key 缺失（历史异常场景）：自动 get-or-create 补齐后重取
      if (cfg.isMcpEnabled && !cfg.key && teamId != null && teamId > 0) {
        await createTeamResourceKey({ teamId, resourceType: 'wiki', resourceId: wikiId })
        cfg = await getWikiMcpConfig(wikiId)
      }
      setConfig(cfg)
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }, [wikiId, teamId, canManage])

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
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }

  const copyAddress = async () => {
    try {
      await navigator.clipboard.writeText(address)
    } catch {
      // 剪贴板失败静默（地址只读可见可手选）
    }
  }

  const mcpOn = config?.isMcpEnabled ?? false

  return (
    <Space direction="vertical" size={spacing.md} style={{ display: 'flex', maxWidth: 880 }}>
      {/* 功能开关行 */}
      <Space size={spacing.sm} align="center">
        <Switch checked={mcpOn} onChange={(v) => void handleToggle(v)} />
        <Text strong>{t('wiki.mcpEnable')}</Text>
        <Tag color={mcpOn ? 'success' : 'default'} style={{ marginRight: 0 }}>
          {mcpOn ? t('teamresKey.stateOn') : t('teamresKey.stateOff')}
        </Tag>
      </Space>

      <Divider style={{ margin: 0 }} />

      {/* MCP 地址 */}
      <div>
        <Text strong style={{ fontSize: 13 }}>{t('wiki.mcpAddress')}</Text>
        <Space.Compact style={{ width: '100%', marginTop: spacing.xs }}>
          <Input readOnly value={address} placeholder={t('wiki.mcpAddressPlaceholder')} />
          <Button icon={<CopyOutlined />} disabled={!address} onClick={() => void copyAddress()} aria-label={t('common.copy')} />
        </Space.Compact>
      </div>

      <Divider style={{ margin: 0 }} />

      {/* 资源 Key 盒子 */}
      <ResourceKeyCard
        featureEnabled={mcpOn}
        canManage={canManage ?? false}
        keyInfo={config ? { teamResourceKeyId: config.teamResourceKeyId, key: config.key, keyPrefix: config.keyPrefix } : null}
        onChanged={() => void load()}
        hintKey="teamresKey.hintWiki"
        titleKey="teamresKey.boxTitleWiki"
      />

      <Text type="secondary" style={{ fontSize: 12 }}>{t('wiki.mcpAuthHint')}</Text>
    </Space>
  )
}
