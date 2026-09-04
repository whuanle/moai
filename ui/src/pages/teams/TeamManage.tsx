import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react'
import { Avatar, Button, Descriptions, Empty, Form, Input, Layout, Menu, Popconfirm, Select, Space, Tag, Tooltip, Typography, Upload } from 'antd'
import type { MenuProps, TableColumnsType } from 'antd'
import type { UploadProps } from 'antd'
import {
  AppstoreAddOutlined,
  BookOutlined,
  KeyOutlined,
  SettingOutlined,
  StopOutlined,
  TeamOutlined,
  UploadOutlined,
  UserSwitchOutlined,
} from '@ant-design/icons'
import { useTranslation } from 'react-i18next'
import { Link, useParams } from 'react-router'
import { Card as DSCard, DataTable, feedback, Page } from '@/design-system'
import { spacing } from '@/design-system/theme'
import { useAppStore } from '@/store/app'
import { formatDateTime } from '@/utils/datetime'
import { Variables } from '@/pages/variables/Variables'
import {
  addTeamUser,
  getTeamCandidates,
  getTeamDetail,
  getTeamUsers,
  removeTeamUser,
  transferTeamOwner,
  updateTeam,
  updateTeamUserRole,
  uploadTeamAvatar,
  type TeamCandidateItem,
  type TeamUserItem,
} from '@/api/team'

const { Text } = Typography
const { Sider, Content } = Layout

/** 角色：0=Member 1=Admin 2=Owner（对齐后端 TeamRole 枚举） */
const ROLE_OWNER = 2
const ROLE_ADMIN = 1
const ROLE_MEMBER = 0

type SectionKey = 'info' | 'members' | 'knowledge' | 'plugins' | 'variables' | 'settings'

interface MemberFormValues {
  userId: number
  role: number
}

interface TeamDetail {
  teamId?: string | number | null
  name?: string | null
  description?: string | null
  avatar?: string | null
  isDisable?: boolean | null
  myRole?: number | null
  memberCount?: number | null
  createTime?: string | null
  ownerUserId?: string | number | null
  ownerUserName?: string | null
  ownerNickName?: string | null
  ownerAvatar?: string | null
}

export function TeamManage() {
  const { t } = useTranslation()
  const params = useParams<{ id: string }>()
  const teamId = Number(params.id)
  const currentUserId = useAppStore((state) => state.userInfo?.userId)

  const [section, setSection] = useState<SectionKey>('info')
  const [detail, setDetail] = useState<TeamDetail | null>(null)
  const [loading, setLoading] = useState(true)
  const [members, setMembers] = useState<TeamUserItem[]>([])
  const [membersLoading, setMembersLoading] = useState(false)
  const [addForm] = Form.useForm<MemberFormValues>()
  const [settingsForm] = Form.useForm<{ name: string; description?: string }>()
  const [savingInfo, setSavingInfo] = useState(false)
  const [uploadingAvatar, setUploadingAvatar] = useState(false)
  const [candidates, setCandidates] = useState<TeamCandidateItem[]>([])
  const [searching, setSearching] = useState(false)

  const loadDetail = useCallback(async () => {
    if (!Number.isFinite(teamId) || teamId <= 0) return
    setLoading(true)
    try {
      setDetail((await getTeamDetail(teamId)) as unknown as TeamDetail)
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setLoading(false)
    }
  }, [teamId])

  const reloadMembers = useCallback(async () => {
    if (!Number.isFinite(teamId) || teamId <= 0) return
    setMembersLoading(true)
    try {
      setMembers(await getTeamUsers(teamId))
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setMembersLoading(false)
    }
  }, [teamId])

  useEffect(() => {
    void loadDetail()
  }, [loadDetail])

  useEffect(() => {
    void reloadMembers()
  }, [reloadMembers])

  useEffect(() => {
    settingsForm.setFieldsValue({
      name: detail?.name ?? '',
      description: detail?.description ?? undefined,
    })
  }, [detail, settingsForm])

  const isOwner = detail?.myRole === ROLE_OWNER

  /** 成员行的移除按钮是否可用（镜像后端矩阵） */
  const canRemoveMember = (member: TeamUserItem): boolean => {
    const role = member.role ?? ROLE_MEMBER
    const myRole = detail?.myRole ?? ROLE_MEMBER
    if (role === ROLE_OWNER) return false
    if (myRole === ROLE_MEMBER) return true
    if (myRole === ROLE_ADMIN) return role === ROLE_MEMBER
    return true
  }

  const handleAddMember = async () => {
    const values = await addForm.validateFields()
    try {
      await addTeamUser(teamId, { userId: values.userId, role: values.role })
      feedback.success(t('team.addSuccess'))
      addForm.resetFields()
      setCandidates([])
      await reloadMembers()
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }

  const handleSearchCandidates = useCallback(async (keyword: string) => {
    setSearching(true)
    try {
      setCandidates(await getTeamCandidates(teamId, keyword))
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setSearching(false)
    }
  }, [teamId])

  const handleChangeRole = async (member: TeamUserItem, role: number) => {
    try {
      await updateTeamUserRole(teamId, Number(member.userId), role)
      feedback.success(t('team.roleSuccess'))
      await reloadMembers()
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }

  const handleRemoveMember = async (member: TeamUserItem) => {
    try {
      await removeTeamUser(teamId, Number(member.userId))
      feedback.success(t('team.removeSuccess'))
      await reloadMembers()
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }

  const handleTransfer = async (member: TeamUserItem) => {
    try {
      await transferTeamOwner(teamId, Number(member.userId))
      feedback.success(t('team.transferSuccess'))
      await Promise.all([reloadMembers(), loadDetail()])
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }

  const handleSaveInfo = async () => {
    const values = await settingsForm.validateFields()
    setSavingInfo(true)
    try {
      await updateTeam(teamId, { name: values.name, description: values.description })
      feedback.success(t('team.saveSuccess'))
      await loadDetail()
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setSavingInfo(false)
    }
  }

  const avatarBeforeUpload: UploadProps['beforeUpload'] = (file) => {
    if (!file.type.startsWith('image/')) {
      feedback.error(t('team.avatarTypeError'))
      return Upload.LIST_IGNORE
    }
    if (file.size > 5 * 1024 * 1024) {
      feedback.error(t('team.avatarSizeError'))
      return Upload.LIST_IGNORE
    }
    setUploadingAvatar(true)
    uploadTeamAvatar(teamId, file)
      .then(() => feedback.success(t('team.avatarSuccess')))
      .then(() => loadDetail())
      .catch(() => undefined)
      .finally(() => setUploadingAvatar(false))
    return Upload.LIST_IGNORE
  }

  const renderRole = (role: number | null | undefined) => {
    if (role === ROLE_OWNER) return <Tag color="gold">{t('team.roleOwner')}</Tag>
    if (role === ROLE_ADMIN) return <Tag color="blue">{t('team.roleAdmin')}</Tag>
    return <Tag>{t('team.roleMember')}</Tag>
  }

  const memberColumns: TableColumnsType<TeamUserItem> = useMemo(
    () => [
      { title: t('team.colNickName'), dataIndex: 'nickName', width: 120 },
      { title: t('team.colUserName'), dataIndex: 'userName', width: 140, ellipsis: true },
      {
        title: t('team.colRole'),
        key: 'role',
        width: 180,
        render: (_, record) => {
          const role = record.role ?? ROLE_MEMBER
          if (role === ROLE_OWNER || !isOwner) return renderRole(role)
          return (
            <Select
              size="small"
              value={role === ROLE_ADMIN ? ROLE_ADMIN : ROLE_MEMBER}
              style={{ width: 120 }}
              onChange={(v) => void handleChangeRole(record, v)}
              options={[
                { value: ROLE_ADMIN, label: t('team.roleAdminOption') },
                { value: ROLE_MEMBER, label: t('team.roleMemberOption') },
              ]}
            />
          )
        },
      },
      {
        title: t('team.colJoinTime'),
        dataIndex: 'joinTime',
        width: 160,
        render: (v: string | null) => (v ? formatDateTime(v) : '-'),
      },
      {
        title: t('team.colActions'),
        key: 'actions',
        width: 110,
        fixed: 'right' as const,
        render: (_, record) => {
          const role = record.role ?? ROLE_MEMBER
          const isSelf = String(record.userId) === String(currentUserId)
          const actions: ReactNode[] = []
          if (isOwner && role !== ROLE_OWNER) {
            actions.push(
              <Popconfirm key="transfer" title={t('team.transferConfirm')} onConfirm={() => void handleTransfer(record)}>
                <Tooltip title={t('team.transferOwner')}>
                  <Button type="text" size="small" icon={<UserSwitchOutlined />} aria-label={t('team.transferOwner')} />
                </Tooltip>
              </Popconfirm>,
            )
          }
          if (canRemoveMember(record)) {
            const label = t(isSelf ? 'team.leave' : 'team.remove')
            actions.push(
              <Popconfirm
                key="remove"
                title={t(isSelf ? 'team.leaveConfirm' : 'team.removeConfirm')}
                onConfirm={() => void handleRemoveMember(record)}
              >
                <Tooltip title={label}>
                  <Button type="text" size="small" danger icon={<StopOutlined />} aria-label={label} />
                </Tooltip>
              </Popconfirm>,
            )
          }
          if (actions.length === 0) return <Text type="secondary">-</Text>
          return <Space size={0}>{actions}</Space>
        },
      },
    ],
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [t, isOwner, detail, currentUserId],
  )

  const menuItems: Required<MenuProps>['items'] = [
    { key: 'info', icon: <TeamOutlined />, label: t('team.info') },
    { key: 'members', icon: <TeamOutlined />, label: t('team.membersTitle') },
    { key: 'knowledge', icon: <BookOutlined />, label: t('team.knowledge') },
    { key: 'plugins', icon: <AppstoreAddOutlined />, label: t('team.managePlugins') },
    { key: 'variables', icon: <KeyOutlined />, label: t('team.manageVariables') },
    { key: 'settings', icon: <SettingOutlined />, label: t('team.settings') },
  ]

  const ownerName = detail?.ownerNickName || detail?.ownerUserName || '-'

  return (
    <Page
      breadcrumb={[
        { title: <Link to="/team">{t('team.title')}</Link> },
        { title: detail?.name ?? '' },
      ]}
    >
      <Layout style={{ background: 'transparent', gap: spacing.md }}>
        <Sider width={200} style={{ background: 'transparent' }}>
          <Menu
            mode="inline"
            items={menuItems}
            selectedKeys={[section]}
            onClick={({ key }) => setSection(key as SectionKey)}
            style={{ borderRadius: spacing.sm }}
          />
        </Sider>
        <Content>
          {loading ? null : section === 'info' ? (
<DSCard styles={{ body: { padding: spacing.lg } }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: spacing.md, marginBottom: spacing.lg }}>
                <Avatar size={72} src={detail?.avatar || undefined} alt={detail?.name ?? ''}>
                  {(detail?.name ?? '?').slice(0, 1).toUpperCase()}
                </Avatar>
                <div>
                  <div style={{ fontSize: 18, fontWeight: 600 }}>{detail?.name ?? '-'}</div>
                  <div style={{ marginTop: 4 }}>{renderRole(detail?.myRole)}</div>
                </div>
              </div>
              <Descriptions column={2} size="small" bordered
                items={[
                  { key: 'owner', label: t('team.owner'), children: ownerName },
                  { key: 'members', label: t('team.colMembers'), children: members.length },
                  { key: 'createTime', label: t('team.colCreateTime'), children: formatDateTime(detail?.createTime ?? '') },
                  { key: 'desc', label: t('team.desc'), children: detail?.description || '-' },
                ]}
              />
            </DSCard>
          ) : section === 'members' ? (
<DSCard styles={{ body: { padding: spacing.lg } }}>
              {isOwner && (
                <Form form={addForm} layout="inline" style={{ marginBottom: spacing.md }} initialValues={{ role: ROLE_MEMBER }}>
                  <Form.Item name="userId" rules={[{ required: true, message: t('team.addMemberPlaceholder') }]}>
                    <Select
                      showSearch
                      placeholder={t('team.addMemberPlaceholder')}
                      style={{ width: 240 }}
                      filterOption={false}
                      onSearch={(v) => void handleSearchCandidates(v)}
                      onFocus={() => void handleSearchCandidates('')}
                      notFoundContent={searching ? null : t('team.addMemberNoResult')}
                      loading={searching}
                      options={candidates.map((c) => ({
                        value: Number(c.userId),
                        label: c.userName ? `${c.userName}${c.nickName ? ` (${c.nickName})` : ''}` : String(c.nickName ?? ''),
                      }))}
                    />
                  </Form.Item>
                  <Form.Item name="role">
                    <Select
                      style={{ width: 140 }}
                      options={[
                        { value: ROLE_MEMBER, label: t('team.roleMember') },
                        { value: ROLE_ADMIN, label: t('team.roleAdmin') },
                      ]}
                    />
                  </Form.Item>
                  <Button type="primary" onClick={() => void handleAddMember()}>
                    {t('team.addMember')}
                  </Button>
                </Form>
              )}
              <DataTable<TeamUserItem>
                rowKey="userId"
                columns={memberColumns}
                dataSource={members}
                loading={membersLoading}
                onRefresh={() => void reloadMembers()}
                refreshLoading={membersLoading}
              />
            </DSCard>
          ) : section === 'knowledge' ? (
<DSCard styles={{ body: { padding: spacing.lg } }}>
              <Empty description={t('team.knowledge')} />
            </DSCard>
          ) : section === 'plugins' ? (
<DSCard styles={{ body: { padding: spacing.lg } }}>
              <Empty description={t('team.managePlugins')} />
            </DSCard>
          ) : section === 'variables' ? (
<DSCard styles={{ body: { padding: spacing.lg } }}>
              <Variables teamId={teamId} />
            </DSCard>
          ) : (
<DSCard styles={{ body: { padding: spacing.lg } }}>
              <Form form={settingsForm} layout="vertical">
                <Form.Item label={t('team.avatar')}>
                  <Space align="center">
                    <Avatar size={64} src={detail?.avatar || undefined}>
                      {(detail?.name ?? '?').slice(0, 1)}
                    </Avatar>
                    <Upload beforeUpload={avatarBeforeUpload} showUploadList={false} accept="image/*">
                      <Button icon={<UploadOutlined />} loading={uploadingAvatar}>
                        {t('team.avatar')}
                      </Button>
                    </Upload>
                  </Space>
                  <div>
                    <Text type="secondary" style={{ fontSize: 12 }}>{t('team.avatarHint')}</Text>
                  </div>
                </Form.Item>
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
                <Button type="primary" loading={savingInfo} onClick={() => void handleSaveInfo()}>
                  {t('team.saveInfo')}
                </Button>
              </Form>
            </DSCard>
          )}
        </Content>
      </Layout>
    </Page>
  )
}
