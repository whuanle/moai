import { useCallback, useEffect, useMemo, useState } from 'react'
import { Avatar, Button, Col, Empty, Form, Input, Modal, Row, Segmented, Space, Tag, Typography } from 'antd'
import { SearchOutlined, TeamOutlined } from '@ant-design/icons'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router'
import { Card, feedback, Page } from '@/design-system'
import { neutralColors, spacing } from '@/design-system/theme'
import { useAppStore } from '@/store/app'
import { formatDateTime } from '@/utils/datetime'
import {
  getMyTeams as fetchMyTeams,
  createTeam,
  getMyTeams,
  type TeamItem,
} from '@/api/team'

/** 角色：0=Member 1=Admin 2=Owner（对齐后端 TeamRole 枚举） */
const ROLE_OWNER = 2
const ROLE_ADMIN = 1

type FilterKey = 'all' | 'created' | 'managed'

interface CreateFormValues {
  name: string
  description?: string
}

export function Teams() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const setMyTeams = useAppStore((state) => state.setMyTeams)

  /** 团队增删后同步侧边栏的团队列表 */
  const syncSidebarTeams = useCallback(async () => {
    try {
      setMyTeams(await fetchMyTeams())
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }, [setMyTeams])

  const [loading, setLoading] = useState(true)
  const [teams, setTeams] = useState<TeamItem[]>([])
  const [createOpen, setCreateOpen] = useState(false)
  const [creating, setCreating] = useState(false)
  const [createForm] = Form.useForm<CreateFormValues>()
  const [filter, setFilter] = useState<FilterKey>('all')
  const [searchText, setSearchText] = useState('')

  const load = useCallback(async () => {
    setLoading(true)
    try {
      setTeams(await getMyTeams())
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    void load()
  }, [load])

  const filteredTeams = useMemo(() => {
    const keyword = searchText.trim().toLowerCase()
    return teams.filter((team) => {
      if (filter === 'created' && team.myRole !== ROLE_OWNER) return false
      if (filter === 'managed' && team.myRole !== ROLE_ADMIN) return false
      if (!keyword) return true
      return (
        (team.name ?? '').toLowerCase().includes(keyword) ||
        (team.description ?? '').toLowerCase().includes(keyword)
      )
    })
  }, [teams, filter, searchText])

  const handleCreate = async () => {
    const values = await createForm.validateFields()
    setCreating(true)
    try {
      await createTeam({ name: values.name, description: values.description })
      feedback.success(t('team.createSuccess'))
      setCreateOpen(false)
      createForm.resetFields()
      void load()
      void syncSidebarTeams()
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setCreating(false)
    }
  }

  const renderRole = (role: number | null | undefined) => {
    if (role === ROLE_OWNER) return <Tag color="gold">{t('team.roleOwner')}</Tag>
    if (role === ROLE_ADMIN) return <Tag color="blue">{t('team.roleAdmin')}</Tag>
    return <Tag>{t('team.roleMember')}</Tag>
  }

  return (
    <Page>
      <div
        style={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          flexWrap: 'wrap',
          gap: spacing.md,
          marginBottom: spacing.lg,
        }}
      >
        <Segmented<FilterKey>
          value={filter}
          onChange={(value) => setFilter(value)}
          options={[
            { label: t('team.filterAll'), value: 'all' },
            { label: t('team.filterCreated'), value: 'created' },
            { label: t('team.filterManaged'), value: 'managed' },
          ]}
        />
        <Space size={spacing.sm}>
          <Input
            value={searchText}
            onChange={(e) => setSearchText(e.target.value)}
            allowClear
            prefix={<SearchOutlined style={{ color: neutralColors.textTertiary }} />}
            placeholder={t('team.searchPlaceholder')}
            style={{ width: 240 }}
          />
          <Button type="primary" icon={<TeamOutlined />} onClick={() => setCreateOpen(true)}>
            {t('team.create')}
          </Button>
        </Space>
      </div>
      {loading ? null : filteredTeams.length === 0 ? (
        <Empty description={t('team.empty')} />
      ) : (
        <Row gutter={[spacing.md, spacing.md]}>
          {filteredTeams.map((team) => {
            const name = team.name || '-'
            const ownerName = team.ownerNickName || team.ownerUserName || '-'
            return (
              <Col xs={24} sm={12} md={8} lg={6} xxl={4} key={String(team.teamId)}>
                <Card style={{ height: '100%', cursor: 'pointer' }} styles={{ body: { padding: spacing.md } }}>
                  <div
                    style={{ display: 'flex', flexDirection: 'column', gap: spacing.sm, height: '100%' }}
                    onClick={() => navigate(`/team/${team.teamId}`)}
                  >
                    <div style={{ display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between' }}>
                      <div style={{ display: 'flex', alignItems: 'center', gap: spacing.sm, minWidth: 0 }}>
                        <Avatar size={44} src={team.avatar || undefined} alt={name}>
                          {name.slice(0, 1).toUpperCase()}
                        </Avatar>
                        <div style={{ minWidth: 0, alignSelf: 'center' }}>
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
                          <div style={{ marginTop: 2 }}>{renderRole(team.myRole)}</div>
                        </div>
                      </div>
                    </div>
                    <Typography.Paragraph
                      type="secondary"
                      style={{ fontSize: 13, marginBottom: 0, minHeight: 38 }}
                      ellipsis={{ rows: 2 }}
                    >
                      {team.description || '-'}
                    </Typography.Paragraph>
                    <div style={{ display: 'flex', flexDirection: 'column', gap: spacing.xxs }}>
                      <span style={{ fontSize: 12, color: neutralColors.textTertiary }}>
                        {t('team.owner')}: {ownerName}
                      </span>
                      <div
                        style={{
                          display: 'flex',
                          gap: spacing.md,
                          color: neutralColors.textTertiary,
                          fontSize: 12,
                          marginTop: 'auto',
                          borderTop: `1px solid ${neutralColors.border}`,
                          paddingTop: spacing.sm,
                        }}
                      >
                        <span>{t('team.colMembers')}: {team.memberCount ?? 0}</span>
                        <span>{t('team.colCreateTime')}: {formatDateTime(team.createTime)}</span>
                      </div>
                    </div>
                  </div>
                </Card>
              </Col>
            )
          })}
        </Row>
      )}
      <Modal
        open={createOpen}
        title={t('team.createTitle')}
        onOk={() => void handleCreate()}
        onCancel={() => setCreateOpen(false)}
        okText={t('team.confirm')}
        cancelText={t('team.cancel')}
        confirmLoading={creating}
        destroyOnHidden
        maskClosable={false}
      >
        <Form form={createForm} layout="vertical">
          <Form.Item
            name="name"
            label={t('team.name')}
            rules={[
              { required: true, message: t('team.namePlaceholder') },
              { max: 50, message: `${t('team.name')} ≤ 50` },
            ]}
          >
            <Input placeholder={t('team.namePlaceholder')} maxLength={50} />
          </Form.Item>
          <Form.Item name="description" label={t('team.desc')} rules={[{ max: 255 }]}>
            <Input.TextArea placeholder={t('team.descPlaceholder')} maxLength={255} rows={3} />
          </Form.Item>
        </Form>
      </Modal>
    </Page>
  )
}
