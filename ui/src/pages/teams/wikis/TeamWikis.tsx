import { useCallback, useEffect, useMemo, useState } from 'react'
import { BookOutlined, DeleteOutlined, EditOutlined, FileTextOutlined, SearchOutlined } from '@ant-design/icons'
import { Avatar, Button, Col, Empty, Form, Input, List, Modal, Popconfirm, Row, Segmented, Space, Tooltip, Typography } from 'antd'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router'
import { AvatarUpload, Card, feedback, PageToolbar, useNeutralColors } from '@/design-system'
import { spacing } from '@/design-system/theme'
import { formatDateTime } from '@/utils/datetime'
import { resolveStorageUrl, uploadImageWithKey } from '@/utils/storage'
import { NAME_PATTERN } from '@/utils/validation'
import {
  createWiki,
  deleteWiki,
  getWikis,
  searchWikiTeamFiles,
  setWikiAvatar,
  updateWiki,
  type WikiItem,
  type WikiTeamFileItem,
} from '@/api/wiki'

const { Paragraph, Text } = Typography

/** 角色：0=Member 1=Admin 2=Owner（对齐后端 TeamRole 枚举） */
const ROLE_MEMBER = 0

const AVATAR_MAX_SIZE = 5 * 1024 * 1024

/** 跨库文件名搜索返回条数上限（与后端默认一致） */
const FILE_SEARCH_LIMIT = 50

interface WikiFormValues {
  name: string
  description?: string
}

/** 新建时已上传的头像：先直传拿 objectKey，等知识库创建成功后立即登记 */
interface PendingAvatar {
  objectKey: string
  url: string
}

interface TeamWikisProps {
  teamId: number
}

export function TeamWikis({ teamId }: TeamWikisProps) {
  const { t } = useTranslation()
  const neutral = useNeutralColors()
  const navigate = useNavigate()

  const [loading, setLoading] = useState(true)
  const [items, setItems] = useState<WikiItem[]>([])
  const [myRole, setMyRole] = useState<number | null>(null)
  /** 搜索输入框内容（查询按钮/回车触发后才生效） */
  const [keyword, setKeyword] = useState('')
  const [appliedKeyword, setAppliedKeyword] = useState('')
  /** 跨库文件名搜索结果；null=未搜索（关键字为空） */
  const [fileResult, setFileResult] = useState<{ items: WikiTeamFileItem[]; total: number } | null>(null)
  const [fileSearching, setFileSearching] = useState(false)
  /** 搜索结果视图：知识库卡片 / 命中文件列表 */
  const [view, setView] = useState<'wikis' | 'files'>('wikis')
  const [formOpen, setFormOpen] = useState(false)
  const [editing, setEditing] = useState<WikiItem | null>(null)
  const [saving, setSaving] = useState(false)
  const [uploadingAvatar, setUploadingAvatar] = useState(false)
  /** 新建模式下的待登记头像；编辑模式直传后立即登记 */
  const [pendingAvatar, setPendingAvatar] = useState<PendingAvatar | null>(null)
  const [form] = Form.useForm<WikiFormValues>()

  const isAdminPlus = myRole !== null && myRole !== ROLE_MEMBER

  const load = useCallback(async () => {
    if (!Number.isFinite(teamId) || teamId <= 0) return
    setLoading(true)
    try {
      const res = await getWikis(teamId)
      setItems(res.items ?? [])
      setMyRole(res.myRole ?? null)
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setLoading(false)
    }
  }, [teamId])

  useEffect(() => {
    void load()
  }, [load])

  /** 跨库文件名搜索 + 视图自动切换：知识库有命中落知识库 Tab，否则文件有命中自动落文件 Tab */
  const runFileSearch = useCallback(
    async (kw: string) => {
      setFileSearching(true)
      try {
        const res = await searchWikiTeamFiles(teamId, kw, FILE_SEARCH_LIMIT)
        const fileItems = res.items ?? []
        setFileResult({ items: fileItems, total: res.total ?? 0 })
        const kwLower = kw.toLowerCase()
        const kbCount = items.filter((item) =>
          `${item.name ?? ''}\n${item.description ?? ''}`.toLowerCase().includes(kwLower),
        ).length
        setView(kbCount === 0 && fileItems.length > 0 ? 'files' : 'wikis')
      } catch {
        // 错误已由全局请求中间件统一提示
      } finally {
        setFileSearching(false)
      }
    },
    [teamId, items],
  )

  /** 应用关键字筛选（查询按钮/回车触发）：知识库客户端过滤 + 文件名跨库服务端搜索 */
  const applySearch = useCallback(() => {
    const kw = keyword.trim()
    setAppliedKeyword(kw)
    if (!kw) {
      setFileResult(null)
      setView('wikis')
      return
    }
    void runFileSearch(kw)
  }, [keyword, runFileSearch])

  /** 客户端过滤：关键字匹配名称/描述 */
  const filteredItems = useMemo(() => {
    const kw = appliedKeyword.toLowerCase()
    if (!kw) return items
    return items.filter((item) => `${item.name ?? ''}\n${item.description ?? ''}`.toLowerCase().includes(kw))
  }, [items, appliedKeyword])

  const openCreate = () => {
    setEditing(null)
    form.resetFields()
    setPendingAvatar(null)
    setFormOpen(true)
  }

  const openEdit = (record: WikiItem) => {
    setEditing(record)
    form.setFieldsValue({
      name: record.name ?? '',
      description: record.description ?? undefined,
    })
    setPendingAvatar(null)
    setFormOpen(true)
  }

  /** 编辑模式：直传完成立即登记到已存在的知识库；新建模式：暂存 objectKey 等创建后登记 */
  const handleAvatarFile = async (file: File) => {
    if (!file.type.startsWith('image/')) {
      feedback.error(t('wiki.avatarTypeError'))
      return
    }
    if (file.size > AVATAR_MAX_SIZE) {
      feedback.error(t('wiki.avatarSizeError'))
      return
    }
    setUploadingAvatar(true)
    try {
      const { objectKey, url } = await uploadImageWithKey(file)
      if (editing) {
        await setWikiAvatar(Number(editing.wikiId), objectKey)
        feedback.success(t('wiki.avatarSuccess'))
        void load()
      } else {
        setPendingAvatar({ objectKey, url })
      }
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setUploadingAvatar(false)
    }
  }

  const handleSubmit = async () => {
    const values = await form.validateFields()
    setSaving(true)
    try {
      if (editing) {
        await updateWiki(Number(editing.wikiId), { name: values.name, description: values.description })
      } else {
        const wikiId = await createWiki({ teamId, name: values.name, description: values.description })
        // 创建成功后立即登记暂存的头像（头像接口需要已存在的知识库 id）
        if (pendingAvatar && wikiId > 0) {
          await setWikiAvatar(wikiId, pendingAvatar.objectKey)
        }
      }
      feedback.success(t(editing ? 'wiki.saveSuccess' : 'wiki.createSuccess'))
      setFormOpen(false)
      setPendingAvatar(null)
      void load()
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setSaving(false)
    }
  }

  const handleDelete = async (record: WikiItem) => {
    try {
      await deleteWiki(Number(record.wikiId))
      feedback.success(t('wiki.deleteSuccess'))
      void load()
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }

  const renderAvatarSource = (record: WikiItem): string | undefined => {
    const raw = record.avatarPath?.trim()
    return raw ? resolveStorageUrl(raw) : undefined
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
            placeholder={t('wiki.searchPlaceholder')}
            style={{ width: 240 }}
          />
        }
        queries={<Button onClick={applySearch}>{t('ds.query.search')}</Button>}
        actions={
          isAdminPlus && (
            <Button type="primary" icon={<BookOutlined />} onClick={openCreate}>
              {t('wiki.create')}
            </Button>
          )
        }
      />

      {appliedKeyword && (
        <Segmented
          style={{ marginBottom: spacing.md }}
          value={view}
          onChange={(v) => setView(v as 'wikis' | 'files')}
          options={[
            { label: `${t('wiki.tabWikis')} ${filteredItems.length}`, value: 'wikis' },
            { label: `${t('wiki.tabFiles')} ${fileResult?.items.length ?? 0}`, value: 'files' },
          ]}
        />
      )}

      {view === 'files' ? (
        <>
          <List
            size="small"
            bordered
            loading={fileSearching}
            dataSource={fileResult?.items ?? []}
            locale={{ emptyText: t('wiki.fileMatchEmpty') }}
            renderItem={(item) => (
              <List.Item
                style={{ cursor: 'pointer' }}
                onClick={() => navigate(`/team/${teamId}/wiki/${item.wikiId}/document/${item.documentId}/embedding`)}
              >
                <Space size={spacing.md} wrap style={{ minWidth: 0 }}>
                  <FileTextOutlined style={{ color: neutral.textSecondary }} />
                  <Text strong ellipsis style={{ maxWidth: 320 }}>
                    {item.fileName}
                  </Text>
                  <Text type="secondary">{item.wikiName}</Text>
                  <Text type="secondary" style={{ fontSize: 12 }}>
                    {formatDateTime(item.updateTime)}
                  </Text>
                </Space>
              </List.Item>
            )}
          />
          {fileResult && fileResult.total > fileResult.items.length && (
            <Text type="secondary" style={{ fontSize: 12, marginTop: spacing.sm, display: 'block' }}>
              {t('wiki.fileMatchMore', { top: FILE_SEARCH_LIMIT })}
            </Text>
          )}
        </>
      ) : loading ? null : items.length === 0 ? (
        <Empty description={t('wiki.empty')} />
      ) : filteredItems.length === 0 ? (
        <Empty description={t('wiki.filterEmpty')} />
      ) : (
        <Row gutter={[spacing.md, spacing.md]}>
          {filteredItems.map((card) => {
            const name = card.name || '-'
            const avatarSrc = renderAvatarSource(card)
            return (
              <Col xs={24} sm={12} md={8} lg={6} xxl={4} key={String(card.wikiId)}>
                <Card hoverable style={{ height: '100%', cursor: 'pointer' }} styles={{ body: { padding: spacing.md } }}>
                  <div
                    style={{ display: 'flex', flexDirection: 'column', gap: spacing.sm, height: '100%' }}
                    onClick={() => navigate(`/team/${teamId}/wiki/${card.wikiId}`)}
                  >
                    <div style={{ display: 'flex', alignItems: 'center', gap: spacing.sm, minWidth: 0 }}>
                      <Avatar shape="square" size={44} icon={<BookOutlined />} src={avatarSrc}>
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
                        <div
                          style={{
                            marginTop: 2,
                            fontSize: 12,
                            lineHeight: 1.5,
                            color: neutral.textTertiary,
                            whiteSpace: 'nowrap',
                            overflow: 'hidden',
                            textOverflow: 'ellipsis',
                          }}
                        >
                          {`${card.documentCount ?? 0} ${t('wiki.statDocuments')} · ${card.chunkCount ?? 0} ${t('wiki.statChunks')}`}
                        </div>
                      </div>
                    </div>
                    <Paragraph
                      type="secondary"
                      style={{ fontSize: 13, marginBottom: 0, minHeight: 38, flex: 1 }}
                      ellipsis={{ rows: 2 }}
                    >
                      {card.description || '-'}
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
                        {card.lastDocumentUpdateTime ? (
                          <Tooltip title={`${t('wiki.colCreateTime')}: ${formatDateTime(card.createTime)}`}>
                            <span>{`${t('wiki.statLastUpdate')} ${formatDateTime(card.lastDocumentUpdateTime).slice(5)}`}</span>
                          </Tooltip>
                        ) : (
                          `${t('wiki.colCreateTime')}: ${formatDateTime(card.createTime)}`
                        )}
                      </span>
                      {isAdminPlus && (
                        <Space size={0} onClick={(e) => e.stopPropagation()}>
                          <Tooltip title={t('wiki.edit')}>
                            <Button
                              type="text"
                              size="small"
                              icon={<EditOutlined />}
                              aria-label={t('wiki.edit')}
                              onClick={() => openEdit(card)}
                            />
                          </Tooltip>
                          <Popconfirm title={t('wiki.deleteConfirm')} onConfirm={() => void handleDelete(card)}>
                            <Tooltip title={t('wiki.delete')}>
                              <Button type="text" size="small" danger icon={<DeleteOutlined />} aria-label={t('wiki.delete')} />
                            </Tooltip>
                          </Popconfirm>
                        </Space>
                      )}
                    </div>
                  </div>
                </Card>
              </Col>
            )
          })}
        </Row>
      )}

      <Modal
        open={formOpen}
        title={editing ? t('wiki.editTitle') : t('wiki.createTitle')}
        onOk={() => void handleSubmit()}
        onCancel={() => setFormOpen(false)}
        okText={editing ? t('wiki.save') : t('wiki.confirm')}
        cancelText={t('wiki.cancel')}
        confirmLoading={saving}
        destroyOnHidden
        maskClosable={false}
      >
        <Form form={form} layout="vertical">
          <Form.Item label={t('wiki.avatar')}>
            <Space align="start">
              <AvatarUpload
                src={
                  pendingAvatar?.url
                  ?? (editing?.avatarPath ? resolveStorageUrl(editing.avatarPath) : undefined)
                }
                fallback={((form.getFieldValue('name') as string | undefined) ?? '?').slice(0, 1).toUpperCase()}
                shape="square"
                size={64}
                uploading={uploadingAvatar}
                onSelect={(file) => void handleAvatarFile(file)}
              />
              <Text type="secondary" style={{ fontSize: 12 }}>
                {t('wiki.avatarHint')}
              </Text>
            </Space>
          </Form.Item>
          <Form.Item
            name="name"
            label={t('wiki.name')}
            rules={[
              { required: true, message: t('wiki.namePlaceholder') },
              { max: 50, message: `${t('wiki.name')} ≤ 50` },
              { pattern: NAME_PATTERN, message: t('validation.nameChars') },
            ]}
          >
            <Input placeholder={t('wiki.namePlaceholder')} maxLength={50} />
          </Form.Item>
          <Form.Item name="description" label={t('wiki.desc')} rules={[{ max: 255 }]}>
            <Input.TextArea placeholder={t('wiki.descPlaceholder')} maxLength={255} rows={3} />
          </Form.Item>
        </Form>
      </Modal>
    </>
  )
}
