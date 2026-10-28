import { useCallback, useEffect, useState } from 'react'
import { FireFilled, ReloadOutlined, SearchOutlined, TeamOutlined } from '@ant-design/icons'
import { Avatar, Button, Col, Empty, Input, Row, Spin, Tag, Typography } from 'antd'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router'
import { Card, Page, useNeutralColors } from '@/design-system'
import { brandColors, colorPrimary, spacing } from '@/design-system/theme'
import { ExternalChannelEntry } from '@/components/ExternalChannelEntry'
import { resolveStorageUrl } from '@/utils/storage'
import { classifyApi, ClassifyType, classifyLabel, type Classify } from '@/api/classify'
import { getPublicApps, type AppItem } from '@/api/app'

const { Text, Paragraph } = Typography

/**
 * 应用广场：展示平台内所有「公开到平台」的内部应用（已发布、未禁用），
 * 任意登录用户可见并可进入对话；支持名称/描述关键字搜索与分类（带 emoji）过滤。
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

  const renderKind = (kind: AppItem['appType']) => {
    if (kind === 'workflow') return <Tag color="purple">{t('appManage.typeWorkflow')}</Tag>
    return <Tag color="blue">{t('appManage.typeAgent')}</Tag>
  }

  return (
    <Page>
      {/* 搜索/刷新与分类标签同一行：antd Space 默认 inline-flex 会导致两段拼在同一行且无间距，改用块级 flex 容器 */}
      <div
        style={{
          display: 'flex',
          flexWrap: 'wrap',
          alignItems: 'center',
          gap: spacing.sm,
          marginBottom: spacing.md,
        }}
      >
        <Input.Search
          value={searchText}
          onChange={(e) => setSearchText(e.target.value)}
          onSearch={(value) => {
            setKeywords(value.trim() || undefined)
          }}
          placeholder={t('appPlaza.searchPlaceholder')}
          prefix={<SearchOutlined style={{ color: 'inherit' }} />}
          allowClear
          maxLength={50}
          style={{ width: 280 }}
        />
        <Button icon={<ReloadOutlined />} onClick={() => void load()} loading={loading}>
          {t('ds.table.refresh')}
        </Button>
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
                        {/* 已开通外部渠道（如飞书）时在卡片右上角展示渠道入口 */}
                        <div style={{ marginLeft: 'auto', alignSelf: 'flex-start' }}>
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
