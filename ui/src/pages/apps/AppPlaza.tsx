import { useCallback, useEffect, useState } from 'react'
import { FireFilled, SearchOutlined, StarFilled, StarOutlined, TeamOutlined } from '@ant-design/icons'
import { Avatar, Button, Col, Empty, Input, Row, Spin, Tag, Tooltip, Typography } from 'antd'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router'
import { Card, Page, useNeutralColors } from '@/design-system'
import { brandColors, colorPrimary, spacing } from '@/design-system/theme'
import { ExternalChannelEntry } from '@/components/ExternalChannelEntry'
import { resolveStorageUrl } from '@/utils/storage'
import { classifyApi, ClassifyType, classifyLabel, type Classify } from '@/api/classify'
import { favoriteApp, getPublicApps, unfavoriteApp, type AppItem } from '@/api/app'

const { Text, Paragraph } = Typography

/**
 * 应用广场：展示平台内所有「公开到平台」的内部应用（已发布、未禁用），
 * 任意登录用户可见并可进入对话；上架可视范围限定组织的应用仅范围内用户可见；
 * 支持名称/描述关键字搜索与分类（带 emoji）过滤；收藏的应用置顶（组内仍按热度排序）。
 */
export function AppPlaza() {
  const { t } = useTranslation()
  const neutral = useNeutralColors()
  const navigate = useNavigate()
  const [loading, setLoading] = useState(true)
  const [items, setItems] = useState<AppItem[]>([])
  const [classifies, setClassifies] = useState<Classify[]>([])
  const [searchText, setSearchText] = useState('')
  const [keywords, setKeywords] = useState<string | undefined>(undefined)
  const [classifyId, setClassifyId] = useState<number | undefined>(undefined)
  const [togglingId, setTogglingId] = useState<string | null>(null)

  const load = useCallback(async () => {
    setLoading(true)
    try {
      setItems(await getPublicApps({ keywords, classifyId }))
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setLoading(false)
    }
  }, [keywords, classifyId])

  // 收藏切换后的静默刷新：不触发全屏 Spin，由后端返回收藏置顶后的新排序
  const refresh = useCallback(async () => {
    try {
      setItems(await getPublicApps({ keywords, classifyId }))
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }, [keywords, classifyId])

  useEffect(() => {
    void load()
  }, [load])

  useEffect(() => {
    classifyApi
      .getClassifies(ClassifyType.App)
      .then(setClassifies)
      .catch(() => {
        // 错误已由全局请求中间件统一提示
      })
  }, [])

  const openChat = (item: AppItem) => {
    if (!item.appId || item.teamId == null) return
    navigate(`/team/${item.teamId}/app/${item.appId}/chat`)
  }

  const toggleFavorite = async (item: AppItem) => {
    if (!item.appId) return
    setTogglingId(String(item.appId))
    try {
      if (item.isFavorite) {
        await unfavoriteApp(String(item.appId))
      } else {
        await favoriteApp(String(item.appId))
      }
      await refresh()
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setTogglingId(null)
    }
  }

  // 查询：回车或点击「查询」按钮生效；关键词未变化时直接刷新列表
  const handleQuery = () => {
    const kw = searchText.trim() || undefined
    if (kw === keywords) {
      void load()
    } else {
      setKeywords(kw)
    }
  }

  const renderKind = (kind: AppItem['appType']) => {
    if (kind === 'workflow') return <Tag color="purple">{t('appManage.typeWorkflow')}</Tag>
    return <Tag color="blue">{t('appManage.typeAgent')}</Tag>
  }

  return (
    <Page>
      {/* 搜索/查询同一行，分类标签单独一行置于工具栏下方（antd Space 默认 inline-flex 会拼行且无间距，统一用块级 flex 容器） */}
      <div
        style={{
          display: 'flex',
          flexWrap: 'wrap',
          alignItems: 'center',
          gap: spacing.sm,
          marginBottom: spacing.sm,
        }}
      >
        <Input
          value={searchText}
          onChange={(e) => {
            const next = e.target.value
            setSearchText(next)
            // 清空输入立即重置过滤，避免残留关键字
            if (!next) setKeywords(undefined)
          }}
          onPressEnter={handleQuery}
          placeholder={t('appPlaza.searchPlaceholder')}
          prefix={<SearchOutlined style={{ color: 'inherit' }} />}
          allowClear
          maxLength={50}
          style={{ width: 280 }}
        />
        <Button onClick={handleQuery} loading={loading}>
          {t('ds.query.search')}
        </Button>
      </div>
      <div
        style={{
          display: 'flex',
          flexWrap: 'wrap',
          alignItems: 'center',
          gap: spacing.sm,
          marginBottom: spacing.md,
        }}
      >
        <Tag.CheckableTag
          checked={classifyId === undefined}
          onChange={() => setClassifyId(undefined)}
        >
          {t('appPlaza.classifyAll')}
        </Tag.CheckableTag>
        {classifies.map((c) => (
          <Tag.CheckableTag
            key={String(c.classifyId ?? '')}
            checked={classifyId === Number(c.classifyId)}
            onChange={() => setClassifyId(Number(c.classifyId) || undefined)}
          >
            {classifyLabel(c)}
          </Tag.CheckableTag>
        ))}
      </div>
      <Spin spinning={loading}>
        {!loading && items.length === 0 ? (
          <Empty description={t('appPlaza.empty')} />
        ) : (
          <Row gutter={[spacing.md, spacing.md]}>
            {items.map((item) => {
              const name = item.name || '-'
              return (
                <Col key={String(item.appId ?? '')} xs={24} sm={12} md={8} lg={6} xxl={4}>
                  <Card style={{ height: '100%' }} styles={{ body: { padding: spacing.md } }}>
                    <div style={{ display: 'flex', flexDirection: 'column', gap: spacing.sm, height: '100%' }}>
                      <div style={{ display: 'flex', alignItems: 'center', gap: spacing.sm }}>
                        <Avatar
                          shape="square"
                          size={44}
                          src={resolveStorageUrl(item.avatarPath ?? null) || undefined}
                          alt={name}
                        >
                          {name.slice(0, 1).toUpperCase()}
                        </Avatar>
                        <div style={{ minWidth: 0 }}>
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
                          <div style={{ marginTop: 2 }}>{renderKind(item.appType)}</div>
                        </div>
                        {/* 右上角：收藏星标 + 已开通外部渠道（如飞书）入口 */}
                        <div style={{ marginLeft: 'auto', alignSelf: 'flex-start', display: 'flex', alignItems: 'center', gap: 2 }}>
                          <Tooltip title={t(item.isFavorite ? 'appPlaza.unfavorite' : 'appPlaza.favorite')}>
                            <Button
                              type="text"
                              size="small"
                              aria-label={t(item.isFavorite ? 'appPlaza.unfavorite' : 'appPlaza.favorite')}
                              icon={
                                item.isFavorite ? (
                                  <StarFilled style={{ color: brandColors.warning }} />
                                ) : (
                                  <StarOutlined />
                                )
                              }
                              loading={togglingId === String(item.appId ?? '')}
                              onClick={() => void toggleFavorite(item)}
                            />
                          </Tooltip>
                          <ExternalChannelEntry channels={item.externalChannels} />
                        </div>
                      </div>
                      <Paragraph
                        type="secondary"
                        style={{ fontSize: 13, marginBottom: 0, minHeight: 38 }}
                        ellipsis={{ rows: 2 }}
                      >
                        {item.description || '-'}
                      </Paragraph>
                      {/* Agent 与已发布流程应用均可对话：流程应用一轮对话 = 一次已发布流程执行 */}
                      <Button type="primary" size="small" block onClick={() => openChat(item)}>
                        {t('appManage.enterChat')}
                      </Button>
                      <div
                        style={{
                          display: 'flex',
                          alignItems: 'center',
                          justifyContent: 'space-between',
                          gap: spacing.sm,
                          color: neutral.textTertiary,
                          fontSize: 12,
                          marginTop: 'auto',
                          borderTop: `1px solid ${neutral.border}`,
                          paddingTop: spacing.sm,
                          minWidth: 0,
                        }}
                      >
                        <Text
                          type="secondary"
                          style={{
                            fontSize: 12,
                            display: 'inline-flex',
                            alignItems: 'center',
                            gap: 4,
                            minWidth: 0,
                            overflow: 'hidden',
                          }}
                        >
                          <TeamOutlined style={{ flexShrink: 0, color: colorPrimary }} />
                          <span style={{ overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                            {item.teamName || '-'}
                          </span>
                        </Text>
                        <Text
                          type="secondary"
                          style={{ fontSize: 12, display: 'inline-flex', alignItems: 'center', gap: 4, flexShrink: 0 }}
                        >
                          <FireFilled style={{ color: brandColors.warning }} />
                          {t('appPlaza.useCount', { count: item.useCount ?? 0 })}
                        </Text>
                      </div>
                    </div>
                  </Card>
                </Col>
              )
            })}
          </Row>
        )}
      </Spin>
    </Page>
  )
}
