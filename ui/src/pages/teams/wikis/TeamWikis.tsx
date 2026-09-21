import { useCallback, useEffect, useState } from 'react'
import { BookOutlined, DeleteOutlined, EditOutlined } from '@ant-design/icons'
import { Avatar, Button, Col, Empty, Form, Input, Modal, Popconfirm, Row, Space, Tooltip, Typography } from 'antd'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router'
import { Card, feedback } from '@/design-system'
import { neutralColors, spacing } from '@/design-system/theme'
import { formatDateTime } from '@/utils/datetime'
import { resolveStorageUrl } from '@/utils/storage'
import {
  createWiki,
  deleteWiki,
  getWikis,
  updateWiki,
  type WikiItem,
} from '@/api/wiki'

const { Paragraph, Text } = Typography

/** 角色：0=Member 1=Admin 2=Owner（对齐后端 TeamRole 枚举） */
const ROLE_MEMBER = 0

interface WikiFormValues {
  name: string
  description?: string
}

interface TeamWikisProps {
  teamId: number
}

export function TeamWikis({ teamId }: TeamWikisProps) {
  const { t } = useTranslation()
  const navigate = useNavigate()

  const [loading, setLoading] = useState(true)
  const [items, setItems] = useState<WikiItem[]>([])
  const [myRole, setMyRole] = useState<number | null>(null)
  const [formOpen, setFormOpen] = useState(false)
  const [editing, setEditing] = useState<WikiItem | null>(null)
  const [saving, setSaving] = useState(false)
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

  const openCreate = () => {
    setEditing(null)
    form.resetFields()
    setFormOpen(true)
  }

  const openEdit = (record: WikiItem) => {
    setEditing(record)
    form.setFieldsValue({
      name: record.name ?? '',
      description: record.description ?? undefined,
    })
    setFormOpen(true)
  }

  const handleSubmit = async () => {
    const values = await form.validateFields()
    setSaving(true)
    try {
      if (editing) {
        await updateWiki(Number(editing.wikiId), { name: values.name, description: values.description })
      } else {
        await createWiki({ teamId, name: values.name, description: values.description })
      }
      feedback.success(t(editing ? 'wiki.saveSuccess' : 'wiki.createSuccess'))
      setFormOpen(false)
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
        {isAdminPlus && (
          <Button type="primary" icon={<BookOutlined />} onClick={openCreate}>
            {t('wiki.create')}
          </Button>
        )}
        <Text type="secondary">{t('ds.table.total', { total: items.length })}</Text>
      </div>

      {loading ? null : items.length === 0 ? (
        <Empty description={t('wiki.empty')} />
      ) : (
        <Row gutter={[spacing.md, spacing.md]}>
          {items.map((card) => {
            const name = card.name || '-'
            const avatarSrc = renderAvatarSource(card)
            return (
              <Col xs={24} sm={12} md={8} lg={6} xxl={4} key={String(card.wikiId)}>
                <Card style={{ height: '100%', cursor: 'pointer' }} styles={{ body: { padding: spacing.md } }}>
                  <div
                    style={{ display: 'flex', flexDirection: 'column', gap: spacing.sm, height: '100%' }}
                    onClick={() => navigate(`/team/${teamId}/wiki/${card.wikiId}`)}
                  >
                    <div style={{ display: 'flex', alignItems: 'center', gap: spacing.sm, minWidth: 0 }}>
                      <Avatar shape="square" size={44} icon={<BookOutlined />} src={avatarSrc}>
                        {name.slice(0, 1).toUpperCase()}
                      </Avatar>
                      <div
                        style={{
                          minWidth: 0,
                          alignSelf: 'center',
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
                      {card.description || '-'}
                    </Paragraph>
                    <div style={{ display: 'flex', alignItems: 'flex-end', gap: spacing.md }}>
                      <div>
                        <div style={{ fontSize: 14, fontWeight: 600, lineHeight: 1.3 }}>{card.documentCount ?? 0}</div>
                        <div style={{ fontSize: 11, color: neutralColors.textTertiary, lineHeight: 1.4 }}>{t('wiki.statDocuments')}</div>
                      </div>
                      <div>
                        <div style={{ fontSize: 14, fontWeight: 600, lineHeight: 1.3 }}>{card.chunkCount ?? 0}</div>
                        <div style={{ fontSize: 11, color: neutralColors.textTertiary, lineHeight: 1.4 }}>{t('wiki.statChunks')}</div>
                      </div>
                      <div style={{ minWidth: 0 }}>
                        <div style={{ fontSize: 14, fontWeight: 600, lineHeight: 1.3, whiteSpace: 'nowrap' }}>
                          {card.lastDocumentUpdateTime ? formatDateTime(card.lastDocumentUpdateTime).slice(5) : '-'}
                        </div>
                        <div style={{ fontSize: 11, color: neutralColors.textTertiary, lineHeight: 1.4 }}>{t('wiki.statLastUpdate')}</div>
                      </div>
                    </div>
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
                      <span>{t('wiki.colCreateTime')}: {formatDateTime(card.createTime)}</span>
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
          <Form.Item
            name="name"
            label={t('wiki.name')}
            rules={[
              { required: true, message: t('wiki.namePlaceholder') },
              { max: 50, message: `${t('wiki.name')} ≤ 50` },
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
