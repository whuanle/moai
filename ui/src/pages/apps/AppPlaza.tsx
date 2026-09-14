import { useCallback, useEffect, useState } from 'react'
import { Avatar, Button, Col, Empty, Row, Spin, Tag, Typography } from 'antd'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router'
import { Card, Page } from '@/design-system'
import { neutralColors, spacing } from '@/design-system/theme'
import { resolveStorageUrl } from '@/utils/storage'
import { getPublicApps, type AppItem } from '@/api/app'

const { Text, Paragraph } = Typography

/**
 * 应用广场：展示平台内所有「公开到平台」的内部应用（已发布、未禁用），
 * 任意登录用户可见并可进入对话；数据来自跨团队的公开应用列表。
 */
export function AppPlaza() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const [loading, setLoading] = useState(true)
  const [items, setItems] = useState<AppItem[]>([])

  const load = useCallback(async () => {
    setLoading(true)
    try {
      setItems(await getPublicApps())
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    void load()
  }, [load])

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
                      </div>
                      <Paragraph
                        type="secondary"
                        style={{ fontSize: 13, marginBottom: 0, minHeight: 38 }}
                        ellipsis={{ rows: 2 }}
                      >
                        {item.description || '-'}
                      </Paragraph>
                      {item.appType !== 'workflow' && (
                        <Button type="primary" size="small" block onClick={() => openChat(item)}>
                          {t('appManage.enterChat')}
                        </Button>
                      )}
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
                        <Text type="secondary" style={{ fontSize: 12 }}>
                          {t('appPlaza.teamId', { id: item.teamId ?? '-' })}
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
