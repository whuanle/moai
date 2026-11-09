import { useCallback, useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Alert, Button, Select, Space, Spin, Tag, Tooltip, Typography, Upload } from 'antd'
import { InboxOutlined, RedoOutlined, StopOutlined } from '@ant-design/icons'
import { DataTable, feedback } from '@/design-system'
import { spacing } from '@/design-system/theme'
import { formatDateTime } from '@/utils/datetime'
import { WIKI_DEFAULT_FILE_TYPES } from '@/api/settings'
import { uploadGraphFile } from '@/utils/storage'
import {
  cancelKnowledgeGraphImportJob,
  getKnowledgeGraphImportJobs,
  getKnowledgeGraphModelOptions,
  importKnowledgeGraphFile,
  retryKnowledgeGraphImportJob,
  type KnowledgeGraphImportJob,
  type KnowledgeGraphModelOption,
} from '@/api/knowledgeGraph'

const MAX_FILE_SIZE = 20 * 1024 * 1024
/** 进行中状态轮询间隔 */
const POLL_INTERVAL = 3000

/** 任务处于活跃状态（未终态） */
export function isImportJobActive(status?: string | null): boolean {
  return status === 'pending' || status === 'extracting' || status === 'parsing' || status === 'merging'
}

function statusTag(status?: string | null): { color: string; key: string } {
  switch (status) {
    case 'done': return { color: 'success', key: 'done' }
    case 'failed': return { color: 'error', key: 'failed' }
    case 'cancelled': return { color: 'default', key: 'cancelled' }
    case 'extracting': return { color: 'processing', key: 'extracting' }
    case 'parsing': return { color: 'processing', key: 'parsing' }
    case 'merging': return { color: 'processing', key: 'merging' }
    default: return { color: 'processing', key: 'pending' }
  }
}

interface KnowledgeGraphAiImportFormProps {
  teamId: number
  graphId: number
  /** 图谱默认抽取模型（设置页配置；优先作为导入模型初始值） */
  extractModelId?: string | null
  /** 导入成功后回调（刷新画布/详情） */
  onImported: () => void
}

/** AI 智能导入表单（导入页内嵌版，B2 任务化）：文档直传 → 登记异步导入任务（Hangfire）→ 任务历史轮询进度/失败重试/取消 */
export function KnowledgeGraphAiImportForm({ teamId, graphId, extractModelId, onImported }: KnowledgeGraphAiImportFormProps) {
  const { t } = useTranslation()
  const [models, setModels] = useState<KnowledgeGraphModelOption[]>([])
  const [modelsLoading, setModelsLoading] = useState(true)
  const [aiModelId, setAiModelId] = useState<string | undefined>(undefined)
  const [file, setFile] = useState<File | null>(null)
  const [submitting, setSubmitting] = useState(false)
  const [jobs, setJobs] = useState<KnowledgeGraphImportJob[]>([])
  const [jobsLoading, setJobsLoading] = useState(false)
  /** 已提示过完成的任务（避免轮询刷新反复弹提示） */
  const notifiedRef = useRef<Set<number>>(new Set())
  /** 刷新历史时若发现刚完成的任务，触发一次回调（画布刷新） */
  const pendingNotifyRef = useRef(false)

  const loadJobs = useCallback(async (silent = false) => {
    if (!silent) setJobsLoading(true)
    try {
      const res = await getKnowledgeGraphImportJobs(graphId, { pageNo: 1, pageSize: 10 })
      const items = res.items ?? []
      setJobs(items)
      // 刚从活跃转终态且成功的任务 → 提示 + 触发画布刷新
      for (const job of items) {
        const id = Number(job.jobId ?? 0)
        if (!isImportJobActive(job.status) && !notifiedRef.current.has(id)) {
          notifiedRef.current.add(id)
          if (job.status === 'done' && job.result && ((job.result.nodesCreated ?? 0) > 0 || (job.result.nodesMerged ?? 0) > 0 || (job.result.edgesCreated ?? 0) > 0)) {
            feedback.success(t('knowledgegraph.import.jobDone', {
              created: job.result.nodesCreated ?? 0,
              merged: job.result.nodesMerged ?? 0,
              edges: job.result.edgesCreated ?? 0,
            }))
            pendingNotifyRef.current = true
          }
        }
      }
      if (pendingNotifyRef.current) {
        pendingNotifyRef.current = false
        onImported()
      }
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      if (!silent) setJobsLoading(false)
    }
  }, [graphId, onImported, t])

  useEffect(() => {
    setModelsLoading(true)
    getKnowledgeGraphModelOptions(teamId)
      .then((options) => {
        const items = options.conversationModels ?? []
        setModels(items)
        const preferred = extractModelId && items.some((x) => x.id === extractModelId) ? extractModelId : items[0]?.id
        if (items.length > 0) setAiModelId(preferred ?? undefined)
      })
      .catch(() => setModels([]))
      .finally(() => setModelsLoading(false))
  }, [teamId])

  useEffect(() => {
    void loadJobs()
  }, [loadJobs])

  // 存在活跃任务时轮询（静默刷新）
  const hasActive = jobs.some((x) => isImportJobActive(x.status))
  useEffect(() => {
    if (!hasActive) return
    const timer = window.setInterval(() => void loadJobs(true), POLL_INTERVAL)
    return () => window.clearInterval(timer)
  }, [hasActive, loadJobs])

  const handleSelectFile = (selected: File) => {
    if (selected.size > MAX_FILE_SIZE) {
      feedback.error(t('knowledgegraph.import.fileTooLarge'))
      return false
    }

    setFile(selected)
    return false
  }

  const handleSubmit = async () => {
    if (!file || !aiModelId) return
    setSubmitting(true)
    try {
      const uploaded = await uploadGraphFile(file)
      await importKnowledgeGraphFile(graphId, {
        objectKey: uploaded.objectKey,
        fileName: file.name,
        aiModelId,
      })
      feedback.success(t('knowledgegraph.import.jobCreated'))
      setFile(null)
      await loadJobs(true)
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setSubmitting(false)
    }
  }

  const handleRetry = async (jobId: number) => {
    try {
      await retryKnowledgeGraphImportJob(graphId, jobId)
      feedback.success(t('knowledgegraph.import.jobRetried'))
      await loadJobs(true)
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }

  const handleCancel = async (jobId: number) => {
    try {
      await cancelKnowledgeGraphImportJob(graphId, jobId)
      feedback.success(t('knowledgegraph.import.jobCancelled'))
      await loadJobs(true)
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }

  return (
    <div>
      <Spin spinning={submitting} tip={t('knowledgegraph.import.submitting')}>
        <Typography.Paragraph type="secondary" style={{ marginBottom: spacing.md, fontSize: 13 }}>
          {t('knowledgegraph.import.hint')}
        </Typography.Paragraph>

        <Upload.Dragger
          accept={WIKI_DEFAULT_FILE_TYPES.join(',')}
          showUploadList={false}
          multiple={false}
          disabled={submitting}
          beforeUpload={handleSelectFile}
          style={{ marginBottom: spacing.md }}
        >
          <p className="ant-upload-drag-icon">
            <InboxOutlined />
          </p>
          <p className="ant-upload-text">{file ? file.name : t('knowledgegraph.import.filePlaceholder')}</p>
          <p className="ant-upload-hint">{t('knowledgegraph.import.fileHint')}</p>
        </Upload.Dragger>

        <div style={{ marginBottom: spacing.sm }}>{t('knowledgegraph.import.modelLabel')}</div>
        {modelsLoading ? (
          <Spin size="small" />
        ) : models.length === 0 ? (
          <Alert type="warning" showIcon message={t('knowledgegraph.import.noModels')} />
        ) : (
          <Select
            style={{ width: '100%' }}
            value={aiModelId}
            onChange={setAiModelId}
            placeholder={t('knowledgegraph.import.modelPlaceholder')}
            options={models.map((x) => ({ value: x.id ?? '', label: x.name ?? '' }))}
          />
        )}

        <Space style={{ marginTop: spacing.md }}>
          <Button type="primary" loading={submitting} disabled={!file || !aiModelId} onClick={() => void handleSubmit()}>
            {t('knowledgegraph.import.submit')}
          </Button>
          <Button icon={<RedoOutlined />} onClick={() => void loadJobs()}>
            {t('knowledgegraph.import.jobsRefresh')}
          </Button>
        </Space>
      </Spin>

      <Typography.Title level={5} style={{ marginTop: spacing.lg, marginBottom: spacing.sm, fontSize: 14 }}>
        {t('knowledgegraph.import.jobsTitle')}
      </Typography.Title>
      <DataTable<KnowledgeGraphImportJob>
        loading={jobsLoading}
        rowKey={(record) => String(record.jobId ?? '')}
        columns={[
          { title: t('knowledgegraph.import.jobsFile'), dataIndex: 'fileName', key: 'fileName', ellipsis: true },
          {
            title: t('knowledgegraph.import.jobsStatus'),
            dataIndex: 'status',
            key: 'status',
            width: 110,
            render: (v: string) => {
              const tag = statusTag(v)
              return <Tag color={tag.color}>{t(`knowledgegraph.import.status_${tag.key}`)}</Tag>
            },
          },
          {
            title: t('knowledgegraph.import.jobsProgress'),
            key: 'progress',
            width: 130,
            render: (_, record) => {
              if (record.status === 'done') {
                const r = record.result
                return r ? t('knowledgegraph.import.jobsResultSummary', {
                  created: r.nodesCreated ?? 0,
                  merged: r.nodesMerged ?? 0,
                  edges: r.edgesCreated ?? 0,
                }) : '-'
              }
              if (record.status === 'failed' || record.status === 'cancelled') return '-'
              return `${record.doneRows ?? 0}/${record.totalRows ?? 0}`
            },
          },
          {
            title: t('knowledgegraph.import.jobsError'),
            key: 'error',
            ellipsis: true,
            render: (_, record) => {
              if (record.status === 'failed') {
                return <Tooltip title={record.error ?? '-'}><span>{record.error ?? '-'}</span></Tooltip>
              }
              if (record.status === 'done' && record.result?.message) {
                return <Tooltip title={record.result.message}><span>{record.result.message}</span></Tooltip>
              }
              return '-'
            },
          },
          { title: t('knowledgegraph.import.jobsTime'), dataIndex: 'createTime', key: 'createTime', width: 150, render: (v: string) => formatDateTime(v) },
          {
            title: t('knowledgegraph.importPage.columnActions'),
            key: 'actions',
            width: 150,
            render: (_, record) => {
              const jobId = Number(record.jobId ?? 0)
              if (record.status === 'failed' || record.status === 'cancelled') {
                return (
                  <Space size={0}>
                    <Button type="link" size="small" icon={<RedoOutlined />} onClick={() => void handleRetry(jobId)}>
                      {t('knowledgegraph.import.jobRetry')}
                    </Button>
                  </Space>
                )
              }
              if (isImportJobActive(record.status)) {
                return (
                  <Button type="link" size="small" danger icon={<StopOutlined />} onClick={() => void handleCancel(jobId)}>
                    {t('knowledgegraph.import.jobCancel')}
                  </Button>
                )
              }
              return null
            },
          },
        ]}
        dataSource={jobs}
        pagination={false}
      />
    </div>
  )
}
