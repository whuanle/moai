import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react'
import { Avatar, Button, Form, Input, InputNumber, Modal, Popconfirm, Select, Space, Tag, Typography, Upload } from 'antd'
import type { TableColumnsType } from 'antd'
import type { UploadProps } from 'antd'
import { UploadOutlined } from '@ant-design/icons'
import { useTranslation } from 'react-i18next'
import { Page, DataTable, feedback } from '@/design-system'
import { useAppStore } from '@/store/app'
import {
  addTeamUser,
  createTeam,
  dissolveTeam,
  getMyTeams,
  getTeamUsers,
  removeTeamUser,
  transferTeamOwner,
  updateTeam,
  updateTeamUserRole,
  uploadTeamAvatar,
  type TeamItem,
  type TeamUserItem,
} from '@/api/team'

const { Text } = Typography

/** 角色：0=Owner 1=Admin 2=Member */
const ROLE_OWNER = 0
const ROLE_ADMIN = 1
const ROLE_MEMBER = 2

interface CreateFormValues {
  name: string
  description?: string
}

interface AddMemberFormValues {
  userId: number
  role: number
}

interface SettingsFormValues {
  name: string
  description?: string
}

export function Teams() {
  const { t } = useTranslation()
  const currentUserId = useAppStore((state) => state.userInfo?.userId)

  const [loading, setLoading] = useState(true)
  const [teams, setTeams] = useState<TeamItem[]>([])
  const [createOpen, setCreateOpen] = useState(false)
  const [creating, setCreating] = useState(false)
  const [membersOpen, setMembersOpen] = useState(false)
  const [membersLoading, setMembersLoading] = useState(false)
  const [current, setCurrent] = useState<TeamItem | null>(null)
  const [members, setMembers] = useState<TeamUserItem[]>([])
  const [addForm] = Form.useForm<AddMemberFormValues>()
  const [createForm] = Form.useForm<CreateFormValues>()
  const [settingsForm] = Form.useForm<SettingsFormValues>()
  const [settingsOpen, setSettingsOpen] = useState(false)
  const [savingInfo, setSavingInfo] = useState(false)
  const [uploadingAvatar, setUploadingAvatar] = useState(false)

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

  const openMembers = async (team: TeamItem) => {
    setCurrent(team)
    setMembersOpen(true)
    setMembersLoading(true)
    try {
      setMembers(await getTeamUsers(Number(team.teamId)))
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setMembersLoading(false)
    }
  }

  const reloadMembers = async () => {
    if (!current) return
    try {
      setMembers(await getTeamUsers(Number(current.teamId)))
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }

  const handleCreate = async () => {
    const values = await createForm.validateFields()
    setCreating(true)
    try {
      await createTeam({ name: values.name, description: values.description })
      feedback.success(t('team.createSuccess'))
      setCreateOpen(false)
      createForm.resetFields()
      void load()
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setCreating(false)
    }
  }

  const handleDissolve = async (team: TeamItem) => {
    try {
      await dissolveTeam(Number(team.teamId))
      feedback.success(t('team.dissolveSuccess'))
      void load()
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }

  const handleAddMember = async () => {
    const values = await addForm.validateFields()
    if (!current) return
    try {
      await addTeamUser(Number(current.teamId), { userId: values.userId, role: values.role })
      feedback.success(t('team.addSuccess'))
      addForm.resetFields()
      await reloadMembers()
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }

  const handleChangeRole = async (member: TeamUserItem, role: number) => {
    if (!current) return
    try {
      await updateTeamUserRole(Number(current.teamId), Number(member.userId), role)
      feedback.success(t('team.roleSuccess'))
      await reloadMembers()
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }

  const handleRemoveMember = async (member: TeamUserItem) => {
    if (!current) return
    try {
      await removeTeamUser(Number(current.teamId), Number(member.userId))
      feedback.success(t('team.removeSuccess'))
      await reloadMembers()
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }

  const openSettings = (team: TeamItem) => {
    setCurrent(team)
    settingsForm.setFieldsValue({ name: team.name ?? '', description: team.description ?? undefined })
    setSettingsOpen(true)
  }

  const handleSaveInfo = async () => {
    const values = await settingsForm.validateFields()
    if (!current) return
    setSavingInfo(true)
    try {
      await updateTeam(Number(current.teamId), { name: values.name, description: values.description })
      feedback.success(t('team.saveSuccess'))
      setSettingsOpen(false)
      void load()
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
    if (current) {
      setUploadingAvatar(true)
      uploadTeamAvatar(Number(current.teamId), file)
        .then(() => feedback.success(t('team.avatarSuccess')))
        .then(() => load())
        .catch(() => undefined)
        .finally(() => setUploadingAvatar(false))
    }
    return Upload.LIST_IGNORE
  }

  const handleTransfer = async (member: TeamUserItem) => {
    if (!current) return
    try {
      await transferTeamOwner(Number(current.teamId), Number(member.userId))
      feedback.success(t('team.transferSuccess'))
      await Promise.all([reloadMembers(), load()])
      setCurrent((prev) => (prev ? { ...prev, myRole: ROLE_ADMIN } : prev))
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }

  const renderRole = (role: number | null | undefined) => {
    if (role === ROLE_OWNER) return <Tag color="gold">{t('team.roleOwner')}</Tag>
    if (role === ROLE_ADMIN) return <Tag color="blue">{t('team.roleAdmin')}</Tag>
    return <Tag>{t('team.roleMember')}</Tag>
  }

  const isOwnerOfCurrent = current?.myRole === ROLE_OWNER

  /** 成员行的移除按钮是否可用（镜像后端矩阵：Owner 行不可移除；Admin 不能移 Admin；Member 仅自己可退） */
  const canRemoveMember = (member: TeamUserItem): boolean => {
    const role = member.role ?? ROLE_MEMBER
    const myRole = current?.myRole ?? ROLE_MEMBER
    if (role === ROLE_OWNER) return false
    if (myRole === ROLE_MEMBER) return true
    if (myRole === ROLE_ADMIN) return role === ROLE_MEMBER
    return true
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
          if (role === ROLE_OWNER || !isOwnerOfCurrent) return renderRole(role)
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
        render: (v: string | null) => (v ? new Date(v).toLocaleString() : '-'),
      },
      {
        title: t('team.colActions'),
        key: 'actions',
        width: 170,
        render: (_, record) => {
          const role = record.role ?? ROLE_MEMBER
          const isSelf = String(record.userId) === String(currentUserId)
          const actions: ReactNode[] = []
          if (isOwnerOfCurrent && role !== ROLE_OWNER) {
            actions.push(
              <Popconfirm
                key="transfer"
                title={t('team.transferConfirm')}
                onConfirm={() => void handleTransfer(record)}
              >
                <Button type="link" size="small">
                  {t('team.transferOwner')}
                </Button>
              </Popconfirm>,
            )
          }
          if (canRemoveMember(record)) {
            actions.push(
              <Popconfirm
                key="remove"
                title={t(isSelf ? 'team.leaveConfirm' : 'team.removeConfirm')}
                onConfirm={() => void handleRemoveMember(record)}
              >
                <Button type="link" size="small" danger>
                  {t(isSelf ? 'team.leave' : 'team.remove')}
                </Button>
              </Popconfirm>,
            )
          }
          if (actions.length === 0) return <Text type="secondary">-</Text>
          return <Space size={0} wrap>{actions}</Space>
        },
      },
    ],
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [t, isOwnerOfCurrent, current],
  )

  const columns: TableColumnsType<TeamItem> = useMemo(
    () => [
      { title: t('team.colName'), dataIndex: 'name', width: 160 },
      { title: t('team.colDesc'), dataIndex: 'description', ellipsis: true },
      {
        title: t('team.colRole'),
        key: 'myRole',
        width: 100,
        render: (_, record) => renderRole(record.myRole),
      },
      { title: t('team.colMembers'), dataIndex: 'memberCount', width: 90 },
      {
        title: t('team.colCreateTime'),
        dataIndex: 'createTime',
        width: 170,
        render: (v: string | null) => (v ? new Date(v).toLocaleString() : '-'),
      },
      {
        title: t('team.colActions'),
        key: 'actions',
        width: 160,
        render: (_, record) => (
          <Space size={0} wrap>
            <Button type="link" size="small" onClick={() => void openMembers(record)}>
              {t('team.members')}
            </Button>
            {(record.myRole === ROLE_OWNER || record.myRole === ROLE_ADMIN) && (
              <Button type="link" size="small" onClick={() => openSettings(record)}>
                {t('team.settings')}
              </Button>
            )}
            {record.myRole === ROLE_OWNER && (
              <Popconfirm title={t('team.dissolveConfirm')} onConfirm={() => void handleDissolve(record)}>
                <Button type="link" size="small" danger>
                  {t('team.dissolve')}
                </Button>
              </Popconfirm>
            )}
          </Space>
        ),
      },
    ],
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [t],
  )

  return (
    <Page
      title={t('team.title')}
      extra={
        <Button type="primary" onClick={() => setCreateOpen(true)}>
          {t('team.create')}
        </Button>
      }
    >
      <DataTable<TeamItem>
        rowKey="teamId"
        columns={columns}
        dataSource={teams}
        loading={loading}
        onRefresh={() => void load()}
        refreshLoading={loading}
      />
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
      <Modal
        open={membersOpen}
        title={t('team.membersTitle')}
        footer={null}
        onCancel={() => setMembersOpen(false)}
        width={680}
        destroyOnHidden
      >
        {isOwnerOfCurrent && (
          <Form form={addForm} layout="inline" style={{ marginBottom: 16 }} initialValues={{ role: ROLE_MEMBER }}>
            <Form.Item name="userId" rules={[{ required: true, message: t('team.userIdPlaceholder') }]}>
              <InputNumber placeholder={t('team.userIdPlaceholder')} min={1} style={{ width: 160 }} />
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
        <div aria-label="team-member-table">
          <DataTable<TeamUserItem>
            rowKey="userId"
            columns={memberColumns}
            dataSource={members}
            loading={membersLoading}
            onRefresh={() => void reloadMembers()}
            refreshLoading={membersLoading}
          />
        </div>
      </Modal>
      <Modal
        open={settingsOpen}
        title={t('team.settingsTitle')}
        onOk={() => void handleSaveInfo()}
        onCancel={() => setSettingsOpen(false)}
        okText={t('team.saveInfo')}
        cancelText={t('team.cancel')}
        confirmLoading={savingInfo}
        destroyOnHidden
        maskClosable={false}
      >
        <Form form={settingsForm} layout="vertical">
          <Form.Item label={t('team.avatar')}>
            <Space align="center">
              <Avatar size={64} src={current?.avatar || undefined}>
                {(current?.name ?? '?').slice(0, 1)}
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
        </Form>
      </Modal>
    </Page>
  )
}
