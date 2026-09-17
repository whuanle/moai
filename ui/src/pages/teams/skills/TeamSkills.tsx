import { useCallback, useEffect, useMemo, useState } from 'react'
import {
  DeleteOutlined,
  DownloadOutlined,
  EditOutlined,
  EyeOutlined,
  PlusOutlined,
  SearchOutlined,
  SendOutlined,
  UndoOutlined,
} from '@ant-design/icons'
import { Button, Form, Input, Modal, Popconfirm, Space, Tag, Typography } from 'antd'
import type { TableColumnsType } from 'antd'
import { useTranslation } from 'react-i18next'
import { DataTable, QueryBar, feedback } from '@/design-system'
import { spacing } from '@/design-system/theme'
import { formatDateTime } from '@/utils/datetime'
import { applyPublication, withdrawPublication } from '@/api/publication'
import {
  deleteSkill,
  downloadSkillFiles,
  getSkill,
  getTeamSkills,
  type SkillDetail,
  type SkillListItem,
} from '@/api/skills'
import { SkillDetailModal } from '@/pages/skills/SkillDetailModal'
import { SkillEditModal } from '@/pages/skills/SkillEditModal'

const { Text } = Typography

interface SkillFilters extends Record<string, unknown> {
  keywords?: string
}

/** 团队技能：团队管理员创建管理，团队成员全部可见可用，支持申请上架到技能市场 */
export function TeamSkills({ teamId, canManage }: { teamId: number; canManage: boolean }) {
  const { t } = useTranslation()

  const [loading, setLoading] = useState(false)
  const [items, setItems] = useState<SkillListItem[]>([])
  const [keywords, setKeywords] = useState<string | undefined>(undefined)
  const [detail, setDetail] = useState<SkillDetail | null>(null)
  const [detailOpen, setDetailOpen] = useState(false)
  const [editOpen, setEditOpen] = useState(false)
  const [editTarget, setEditTarget] = useState<string | null>(null)
  const [applyRecord, setApplyRecord] = useState<SkillListItem | null>(null)
  const [applying, setApplying] = useState(false)
  const [applyForm] = Form.useForm<{ applyReason?: string }>()
  const [filterForm] = Form.useForm<SkillFilters>()

  const load = useCallback(async () => {
    if (!teamId) return
    setLoading(true)
    try {
      setItems(await getTeamSkills(teamId, { keywords }))
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setLoading(false)
    }
  }, [teamId, keywords])

  useEffect(() => {
    void load()
  }, [load])

  const openDetail = async (record: SkillListItem) => {
    try {
      setDetail(await getSkill(record.id ?? ''))
      setDetailOpen(true)
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }

  const handleDelete = async (record: SkillListItem) => {
    try {
      await deleteSkill(record.id ?? '')
      feedback.success(t('skills.deleteSuccess'))
      void load()
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }

  const handleDownload = async (record: SkillListItem) => {
    try {
      await downloadSkillFiles(record.id ?? '')
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }

  const handleApply = async () => {
    if (!applyRecord) return
    const values = await applyForm.validateFields()
    setApplying(true)
    try {
      await applyPublication({
        resourceType: 'skill',
        resourceId: String(applyRecord.id),
        applyReason: values.applyReason,
      })
      feedback.success(t('skills.applySuccess'))
      setApplyRecord(null)
      applyForm.resetFields()
      void load()
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setApplying(false)
    }
  }

  const handleWithdraw = async (record: SkillListItem) => {
    if (!record.pendingPublicationId) return
    try {
      await withdrawPublication(String(record.pendingPublicationId))
      feedback.success(t('skills.withdrawSuccess'))
      void load()
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }

  const renderStatus = (record: SkillListItem) => {
    if (record.isPublic) return <Tag color="success">{t('skills.statusPublic')}</Tag>
    if (record.pendingPublicationId) return <Tag color="processing">{t('skills.statusPending')}</Tag>
    return <Tag>{t('skills.statusPrivate')}</Tag>
  }

  const applyFilters = () => {
    const values = filterForm.getFieldsValue()
    setKeywords(typeof values.keywords === 'string' && values.keywords.trim() ? values.keywords.trim() : undefined)
  }

  const columns: TableColumnsType<SkillListItem> = useMemo(
    () => [
      {
        title: t('skills.colName'),
        key: 'name',
        width: 200,
        render: (_, record) => (
          <Space size={spacing.sm}>
            <span style={{ fontWeight: 500 }}>{record.name || '-'}</span>
            <Text code style={{ fontSize: 12 }}>
              {record.key}
            </Text>
          </Space>
        ),
      },
      { title: t('skills.colDescription'), dataIndex: 'description', ellipsis: true, render: (v: string | null) => v || '-' },
      { title: t('skills.colStatus'), key: 'status', width: 100, render: (_, record) => renderStatus(record) },
      { title: t('skills.colFileCount'), dataIndex: 'fileCount', width: 80, render: (v: number | null) => v ?? 0 },
      {
        title: t('skills.colUpdateTime'),
        dataIndex: 'updateTime',
        width: 160,
        render: (v: string | null) => <span style={{ whiteSpace: 'nowrap' }}>{formatDateTime(v)}</span>,
      },
      {
        title: t('skills.colActions'),
        key: 'actions',
        width: 220,
        fixed: 'right' as const,
        render: (_, record) => (
          <Space size={0}>
            <Button type="text" size="small" icon={<EyeOutlined />} aria-label={`${t('skills.detail')}-${record.key ?? ''}`} onClick={() => void openDetail(record)} />
            {canManage && (
              <>
                <Button
                  type="text"
                  size="small"
                  icon={<EditOutlined />}
                  aria-label={`${t('skills.edit')}-${record.key ?? ''}`}
                  onClick={() => {
                    setEditTarget(record.id ?? null)
                    setEditOpen(true)
                  }}
                />
                {record.isPublic ? null : record.pendingPublicationId ? (
                  <Popconfirm title={t('skills.withdrawConfirm')} onConfirm={() => void handleWithdraw(record)}>
                    <Button type="text" size="small" icon={<UndoOutlined />} aria-label={t('skills.withdraw')} />
                  </Popconfirm>
                ) : (
                  <Button
                    type="text"
                    size="small"
                    icon={<SendOutlined />}
                    aria-label={t('skills.apply')}
                    onClick={() => {
                      setApplyRecord(record)
                      applyForm.resetFields()
                    }}
                  />
                )}
                <Popconfirm title={t('skills.deleteConfirm')} onConfirm={() => void handleDelete(record)}>
                  <Button type="text" size="small" danger icon={<DeleteOutlined />} aria-label={`${t('skills.delete')}-${record.key ?? ''}`} />
                </Popconfirm>
              </>
            )}
            <Button
              type="text"
              size="small"
              icon={<DownloadOutlined />}
              aria-label={`${t('skills.download')}-${record.key ?? ''}`}
              onClick={() => void handleDownload(record)}
            />
          </Space>
        ),
      },
    ],
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [t, canManage],
  )

  return (
    <>
      <QueryBar form={filterForm} onSearch={applyFilters} onReset={applyFilters} loading={loading}>
        <Form.Item name="keywords">
          <Input
            placeholder={t('skills.searchPlaceholder')}
            prefix={<SearchOutlined style={{ color: 'inherit' }} />}
            allowClear
            maxLength={100}
            style={{ width: 280 }}
          />
        </Form.Item>
      </QueryBar>
      <DataTable<SkillListItem>
        rowKey="id"
        columns={columns}
        dataSource={items}
        loading={loading}
        sticky
        scroll={{ x: 980 }}
        toolbar={
          <Space size={12}>
            {canManage && (
              <Button
                type="primary"
                icon={<PlusOutlined />}
                onClick={() => {
                  setEditTarget(null)
                  setEditOpen(true)
                }}
              >
                {t('skills.create')}
              </Button>
            )}
            <Text type="secondary">{t('ds.table.total', { total: items.length })}</Text>
          </Space>
        }
        onRefresh={() => void load()}
        refreshLoading={loading}
      />
      <SkillDetailModal open={detailOpen} detail={detail} onClose={() => setDetailOpen(false)} />
      <SkillEditModal
        open={editOpen}
        skillId={editTarget}
        teamId={teamId}
        onSaved={() => {
          setEditOpen(false)
          setEditTarget(null)
          void load()
        }}
        onCancel={() => {
          setEditOpen(false)
          setEditTarget(null)
        }}
      />
      <Modal
        open={!!applyRecord}
        title={t('skills.applyTitle')}
        onOk={() => void handleApply()}
        onCancel={() => setApplyRecord(null)}
        okText={t('skills.applyOk')}
        cancelText={t('skills.cancel')}
        confirmLoading={applying}
        destroyOnHidden
        maskClosable={false}
      >
        <Form form={applyForm} layout="vertical">
          <Form.Item name="applyReason" label={t('skills.applyReason')} rules={[{ max: 255 }]}>
            <Input.TextArea placeholder={t('skills.applyReasonPlaceholder')} rows={3} maxLength={255} />
          </Form.Item>
        </Form>
      </Modal>
    </>
  )
}
