import { useCallback, useEffect, useState } from 'react'
import { Avatar, Button, Col, Empty, Row, Spin, Typography } from 'antd'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router'
import { Card, Page } from '@/design-system'
import { spacing } from '@/design-system/theme'
import { resolveStorageUrl } from '@/utils/storage'
import { getAvailableSubsystems, type AvailableSubsystemItem } from '@/api/subsystem'

const { Paragraph, Text } = Typography

/**
 * 子系统门户：展示当前用户获授权可进入的子系统（应用接入中启用、已开放委托登录的子系统），
 * 点击进入走委托 SSO（/sso/authorize?client=<appid>），由 MoAI 签发一次性委托码并回跳子系统注册回调。
 * 后端只下发经「团队范围 ∪ 组织授权」放通的子系统，因此无权限用户看不到入口（菜单也不出现）。
 */
export function SubsystemPortal() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const [loading, setLoading] = useState(true)
  const [items, setItems] = useState<AvailableSubsystemItem[]>([])

  const load = useCallback(async () => {
    setLoading(true)
    try {
      setItems(await getAvailableSubsystems())
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    void load()
  }, [load])

  const enter = (item: AvailableSubsystemItem) => {
    if (!item.key) return
    navigate(`/sso/authorize?client=${encodeURIComponent(item.key)}`)
  }

  return (
    <Page>
      <Spin spinning={loading}>
        {!loading && items.length === 0 ? (
          <Empty description={t('subsystemPortal.empty')}>
            <Text type="secondary">{t('subsystemPortal.emptyHint')}</Text>
          </Empty>
        ) : (
          <Row gutter={[spacing.md, spacing.md]}>
            {items.map((item) => {
              const name = item.name || '-'
              return (
                <Col key={String(item.subsystemId ?? item.key ?? '')} xs={24} sm={12} md={8} lg={6} xxl={4}>
                  <Card style={{ height: '100%' }} styles={{ body: { padding: spacing.md } }}>
                    <div style={{ display: 'flex', flexDirection: 'column', gap: spacing.sm, height: '100%' }}>
                      <div style={{ display: 'flex', alignItems: 'center', gap: spacing.sm }}>
                        <Avatar
                          shape="square"
                          size={44}
                          src={resolveStorageUrl(item.avatarPath) || undefined}
                          alt={name}
                        >
                          {name.slice(0, 1).toUpperCase()}
                        </Avatar>
                        <div
                          style={{
                            minWidth: 0,
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
                      </div>
                      <Paragraph
                        type="secondary"
                        style={{ fontSize: 13, marginBottom: 0, minHeight: 38 }}
                        ellipsis={{ rows: 2 }}
                      >
                        {item.description || '-'}
                      </Paragraph>
                      <Button type="primary" size="small" block onClick={() => enter(item)}>
                        {t('subsystemPortal.enter')}
                      </Button>
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
