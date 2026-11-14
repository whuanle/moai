import { useCallback, useEffect, useMemo, useState } from 'react'
import { useNavigate } from 'react-router'
import { useTranslation } from 'react-i18next'
import { Avatar, Button, Col, Empty, Form, Input, Modal, Popconfirm, Radio, Row, Select, Space, Spin, Tag, Tooltip, Typography } from 'antd'
import { ClusterOutlined, DeleteOutlined, EditOutlined, PlusOutlined, SearchOutlined } from '@ant-design/icons'
import { AvatarUpload, Card, feedback, PageToolbar, useNeutralColors } from '@/design-system'
import { spacing } from '@/design-system/theme'
import { formatDateTime } from '@/utils/datetime'
import { resolveStorageUrl } from '@/utils/storage'
import { NAME_PATTERN } from '@/utils/validation'
import {
  createKnowledgeGraph,
  deleteKnowledgeGraph,
  getKnowledgeGraphTemplates,
  getKnowledgeGraphs,
  updateKnowledgeGraph,
  uploadKnowledgeGraphAvatar,
  type KnowledgeGraphItem,
  type KnowledgeGraphTemplateItem,
} from '@/api/knowledgeGraph'

const ROLE_MEMBER = 0

const { Paragraph } = Typography

interface FormValues {
  name: string
  description?: string
  mode?: 'managed' | 'connected'
  templateKey?: string
  database?: string
}

export function TeamKnowledgeGraphs({ teamId }: { teamId: number }) {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const neutral = useNeutralColors()
  const [items, setItems] = useState<KnowledgeGraphItem[]>([])
  const [loading, setLoading] = useState(true)
  const [myRole, setMyRole] = useState<number | null>(null)
  const [enabled, setEnabled] = useState(true)
  /** 搜索输入框内容（查询按钮/回车触发后才生效） */
  const [keyword, setKeyword] = useState('')
  const [appliedKeyword, setAppliedKeyword] = useState('')
  const [templates, setTemplates] = useState<KnowledgeGraphTemplateItem[]>([])
  const [open, setOpen] = useState(false)
  const [editing, setEditing] = useState<KnowledgeGraphItem | null>(null)
  const [saving, setSaving] = useState(false)
  const [avatarFile, setAvatarFile] = useState<File | null>(null)
  const [avatarPreview, setAvatarPreview] = useState<string | undefined>(undefined)
  const [form] = Form.useForm<FormValues>()
  const isAdminPlus = myRole !== null && myRole !== ROLE_MEMBER

  const load = useCallback(async () => {
    setLoading(true)
    try {
      const res = await getKnowledgeGraphs(teamId)
      setItems(res.items ?? [])
      setMyRole(res.myRole ?? null)
      setEnabled(res.enabled ?? false)
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setLoading(false)
    }
  }, [teamId])

  useEffect(() => { void load() }, [load])

  /** 应用关键字筛选（查询按钮/回车触发） */
  const applySearch = useCallback(() => setAppliedKeyword(keyword.trim()), [keyword])

  /** 客户端过滤：关键字匹配名称/描述 */
  const filteredItems = useMemo(() => {
    const kw = appliedKeyword.toLowerCase()
    if (!kw) return items
    return items.filter((item) => `${item.name ?? ''}\n${item.description ?? ''}`.toLowerCase().includes(kw))
  }, [items, appliedKeyword])

  useEffect(() => {
    if (enabled) void getKnowledgeGraphTemplates().then(setTemplates).catch(() => setTemplates([]))
  }, [enabled])

  /** 弹窗关闭时清理待上传头像与预览地址（仅 blob 预览需要回收） */
  const resetAvatar = () => {
    setAvatarFile(null)
    setAvatarPreview((prev) => {
      if (prev?.startsWith('blob:')) URL.revokeObjectURL(prev)
      return undefined
    })
  }

  const openCreate = () => {
    setEditing(null)
    form.resetFields()
    resetAvatar()
    setOpen(true)
  }

  const openEdit = (record: KnowledgeGraphItem) => {
    setEditing(record)
    form.setFieldsValue({ name: record.name ?? '', description: record.description ?? undefined })
    setAvatarFile(null)
    setAvatarPreview(record.avatarPath?.trim() ? resolveStorageUrl(record.avatarPath) : undefined)
    setOpen(true)
  }

  /** 创建弹窗内先本地校验头像，创建成功后再直传登记 */
  const handleAvatarSelect = (file: File) => {
    if (!file.type.startsWith('image/')) {
      feedback.error(t('knowledgegraph.avatarTypeError'))
      return
    }
    if (file.size > 5 * 1024 * 1024) {
      feedback.error(t('knowledgegraph.avatarSizeError'))
      return
    }
    resetAvatar()
    setAvatarFile(file)
    setAvatarPreview(URL.createObjectURL(file))
  }

  const handleSubmit = async () => {
    let values: FormValues
    try {
      values = await form.validateFields()
    } catch {
      // 校验失败，antd 已在表单项下展示错误
      return
    }
    setSaving(true)
    try {
      if (editing) {
        await updateKnowledgeGraph(Number(editing.kgId), { name: values.name, description: values.description })
        if (avatarFile) {
          try {
            await uploadKnowledgeGraphAvatar(Number(editing.kgId), avatarFile)
          } catch {
            // 头像登记失败已由全局请求中间件提示，图谱更新不受影响，可在设置页补传
          }
        }
        feedback.success(t('knowledgegraph.saveSuccess'))
      } else {
        const mode = values.mode ?? 'managed'
        const kgId = await createKnowledgeGraph({
          teamId,
          name: values.name,
          description: values.description,
          mode,
          templateKey: mode === 'managed' ? values.templateKey : undefined,
          database: mode === 'connected' ? values.database : undefined,
        })
        if (avatarFile && kgId > 0) {
          try {
            await uploadKnowledgeGraphAvatar(kgId, avatarFile)
          } catch {
            // 头像登记失败已由全局请求中间件提示，图谱创建不受影响，可在设置页补传
          }
        }
        feedback.success(t('knowledgegraph.createSuccess'))
      }
      setOpen(false)
      resetAvatar()
      void load()
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setSaving(false)
    }
  }

  const handleDelete = async (record: KnowledgeGraphItem) => {
    try {
      await deleteKnowledgeGraph(Number(record.kgId))
      feedback.success(t('knowledgegraph.deleteSuccess'))
      void load()
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }

  return (
    <>
      <PageToolbar
        filters={
          <Input
            value={keyword}
            onChange={(e) => setKeyword(e.target.value)}
            onPressEnter={applySearch}
            allowClear
            maxLength={100}
            prefix={<SearchOutlined style={{ color: 'inherit' }} />}
            placeholder={t('knowledgegraph.listSearchPlaceholder')}
            style={{ width: 240 }}
          />
        }
        queries={<Button onClick={applySearch}>{t('ds.query.search')}</Button>}
        actions={
          isAdminPlus && (
            <Button type="primary" icon={<PlusOutlined />} disabled={!enabled} onClick={openCreate}>
              {t('knowledgegraph.create')}
            </Button>
          )
        }
      />
      {!enabled && <div style={{ marginBottom: spacing.md }}>{t('knowledgegraph.disabled')}</div>}
      <Spin spinning={loading}>
        {items.length === 0 ? (
          <Empty description={t('knowledgegraph.empty')} />
        ) : filteredItems.length === 0 ? (
          <Empty description={t('knowledgegraph.filterEmpty')} />
        ) : (
          <Row gutter={[spacing.md, spacing.md]}>
          {filteredItems.map((item) => (
          <Col key={String(item.kgId)} xs={24} sm={12} md={8} lg={6} xxl={4}>
            <Card
              hoverable
              style={{ height: '100%', cursor: 'pointer' }}
              styles={{ body: { padding: spacing.md } }}
              onClick={() => navigate(`/team/${teamId}/kg/${item.kgId}/canvas`)}
            >
              <div style={{ display: 'flex', flexDirection: 'column', gap: spacing.sm, height: '100%' }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: spacing.sm, minWidth: 0 }}>
                  <Avatar
                    shape="square"
                    size={44}
                    icon={<ClusterOutlined />}
                    src={item.avatarPath?.trim() ? resolveStorageUrl(item.avatarPath) : undefined}
                  />
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
                      {item.name || '-'}
                    </div>
                    <div style={{ display: 'flex', alignItems: 'center', gap: spacing.xs, marginTop: 2, minWidth: 0 }}>
                      {item.mode === 'connected' && (
                        <Tag color="blue" style={{ marginRight: 0, flexShrink: 0 }}>
                          {t('knowledgegraph.connectedBadge')}
                        </Tag>
                      )}
                    </div>
                  </div>
                </div>
                <Paragraph
                  type="secondary"
                  style={{ fontSize: 13, marginBottom: 0, minHeight: 38, flex: 1 }}
                  ellipsis={{ rows: 2 }}
                >
                  {item.description || '-'}
                </Paragraph>
                <div
                  style={{
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'space-between',
                    gap: spacing.sm,
                    fontSize: 12,
                    color: neutral.textTertiary,
                    marginTop: 'auto',
                    borderTop: `1px solid ${neutral.border}`,
                    paddingTop: spacing.sm,
                  }}
                >
                  <span style={{ minWidth: 0, whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>
                    {t('knowledgegraph.colCreateTime')}: {formatDateTime(item.createTime)}
                  </span>
                  {isAdminPlus && (
                    <Space size={0} onClick={(e) => e.stopPropagation()}>
                      <Tooltip title={t('knowledgegraph.edit')}>
                        <Button
                          type="text"
                          size="small"
                          icon={<EditOutlined />}
                          aria-label={t('knowledgegraph.edit')}
                          onClick={() => openEdit(item)}
                        />
                      </Tooltip>
                      <Popconfirm
                        title={item.mode === 'connected' ? t('knowledgegraph.deleteConnectedConfirm') : t('knowledgegraph.deleteConfirm')}
                        onConfirm={() => void handleDelete(item)}
                      >
                        <Tooltip title={t('knowledgegraph.delete')}>
                          <Button type="text" size="small" danger icon={<DeleteOutlined />} aria-label={t('knowledgegraph.delete')} />
                        </Tooltip>
                      </Popconfirm>
                    </Space>
                  )}
                </div>
              </div>
            </Card>
          </Col>
        ))}
          </Row>
        )}
      </Spin>

      <Modal
        open={open}
        title={editing ? t('knowledgegraph.editTitle') : t('knowledgegraph.createTitle')}
        onOk={() => void handleSubmit()}
        onCancel={() => { setOpen(false); resetAvatar() }}
        confirmLoading={saving}
        destroyOnHidden
        maskClosable={false}
      >
        <Form form={form} layout="vertical">
          <Form.Item label={t('knowledgegraph.avatar')}>
            <Space size={spacing.md} align="center">
              <AvatarUpload
                src={avatarPreview}
                fallback={<ClusterOutlined />}
                shape="square"
                size={64}
                onSelect={handleAvatarSelect}
              />
              <span style={{ opacity: 0.65 }}>{t('knowledgegraph.avatarHint')}</span>
            </Space>
          </Form.Item>
          <Form.Item
            name="name"
            label={t('knowledgegraph.name')}
            rules={[
              { required: true, message: t('knowledgegraph.namePlaceholder') },
              { pattern: NAME_PATTERN, message: t('validation.nameChars') },
            ]}
          >
            <Input maxLength={50} placeholder={t('knowledgegraph.namePlaceholder')} />
          </Form.Item>
          <Form.Item name="description" label={t('knowledgegraph.desc')}>
            <Input.TextArea maxLength={255} rows={2} />
          </Form.Item>
          {!editing && (
            <>
              <Form.Item name="mode" label={t('knowledgegraph.mode')} initialValue="managed">
                <Radio.Group>
                  <Radio value="managed">{t('knowledgegraph.modeManaged')}</Radio>
                  <Radio value="connected">{t('knowledgegraph.modeConnected')}</Radio>
                </Radio.Group>
              </Form.Item>
              <Form.Item noStyle shouldUpdate={(p, c) => p.mode !== c.mode}>
                {({ getFieldValue }) => getFieldValue('mode') === 'connected' ? (
                  <Form.Item
                    name="database"
                    label={t('knowledgegraph.database')}
                    rules={[{ required: true, message: t('knowledgegraph.databasePlaceholder') }]}
                  >
                    <Input maxLength={100} placeholder={t('knowledgegraph.databasePlaceholder')} />
                  </Form.Item>
                ) : (
                  <Form.Item name="templateKey" label={t('knowledgegraph.template')} initialValue="blank">
                    <Select
                      options={templates.map((x) => ({
                        value: x.key ?? '',
                        label: `${x.name}${x.entityTypes?.length ? ` (${x.entityTypes.join(' / ')})` : ''}`,
                      }))}
                    />
                  </Form.Item>
                )}
              </Form.Item>
            </>
          )}
        </Form>
      </Modal>
    </>
  )
}
