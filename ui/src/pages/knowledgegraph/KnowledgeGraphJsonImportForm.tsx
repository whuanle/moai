import { useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Alert, Button, Checkbox, Input, Select, Space, Tag, Typography, Upload } from 'antd'
import type { TableColumnsType } from 'antd'
import { DownloadOutlined, InboxOutlined, SafetyCertificateOutlined } from '@ant-design/icons'
import { DataTable, feedback } from '@/design-system'
import { spacing } from '@/design-system/theme'
import {
  importKnowledgeGraphJson,
  type KnowledgeGraphJsonImportResult,
  type KnowledgeGraphJsonImportItemResult,
  type KnowledgeGraphDuplicateSuspect,
} from '@/api/knowledgeGraph'

const JSON_PLACEHOLDER = `{
  "mode": "upsert",
  "autoCreateTypes": true,
  "nodes": [
    { "key": "p-1", "entityTypeName": "人员", "name": "张三", "description": "工程师", "properties": { "城市": "深圳" } },
    { "key": "c-1", "entityTypeName": "公司", "name": "Acme" }
  ],
  "edges": [
    { "relationTypeName": "任职", "source": { "key": "p-1" }, "target": { "key": "c-1" } }
  ]
}`

/** 示例 JSON：覆盖节点 key/类型名引用/属性 与 边的 key、名称+类型消歧两种端点引用（可直接导入） */
const SAMPLE_JSON = JSON.stringify(
  {
    mode: 'upsert',
    autoCreateTypes: true,
    nodes: [
      { key: 'person-1', entityTypeName: '人员', name: '张三', description: '后端工程师', properties: { 城市: '深圳', 级别: 'P7' } },
      { key: 'company-1', entityTypeName: '公司', name: 'Acme', description: '示例公司', properties: { 行业: '互联网' } },
      { key: 'person-2', entityTypeName: '人员', name: '李四' },
    ],
    edges: [
      { relationTypeName: '任职', source: { key: 'person-1' }, target: { key: 'company-1' } },
      { relationTypeName: '任职', source: { key: 'person-2' }, target: { name: 'Acme', entityTypeName: '公司' } },
    ],
  },
  null,
  2,
)

function downloadSample() {
  const blob = new Blob([SAMPLE_JSON], { type: 'application/json;charset=utf-8' })
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = 'knowledge-graph-import-sample.json'
  a.click()
  URL.revokeObjectURL(url)
}

interface KnowledgeGraphJsonImportFormProps {
  graphId: number
  /** 导入落库成功后回调（刷新画布/详情） */
  onImported: () => void
}

/** JSON 结构化导入表单：粘贴或上传 JSON → 可选预检 → 反序列化落库（与外部 /import 同一管线，逐条返回结果） */
export function KnowledgeGraphJsonImportForm({ graphId, onImported }: KnowledgeGraphJsonImportFormProps) {
  const { t } = useTranslation()
  const [content, setContent] = useState('')
  const [mode, setMode] = useState<'upsert' | 'create'>('upsert')
  const [autoCreateTypes, setAutoCreateTypes] = useState(true)
  const [detectDuplicates, setDetectDuplicates] = useState(true)
  const [submitting, setSubmitting] = useState(false)
  const [previewing, setPreviewing] = useState(false)
  const [result, setResult] = useState<KnowledgeGraphJsonImportResult | null>(null)
  const [wasPreview, setWasPreview] = useState(false)

  const itemColumns: TableColumnsType<KnowledgeGraphJsonImportItemResult> = [
    {
      title: t('knowledgegraph.importPage.columnKind'),
      dataIndex: 'kind',
      key: 'kind',
      width: 80,
      render: (v: string) => <Tag>{v === 'edge' ? t('knowledgegraph.importPage.kindEdge') : t('knowledgegraph.importPage.kindNode')}</Tag>,
    },
    { title: t('knowledgegraph.importPage.columnIndex'), dataIndex: 'index', key: 'index', width: 70 },
    {
      title: t('knowledgegraph.importPage.columnAction'),
      dataIndex: 'action',
      key: 'action',
      width: 90,
      render: (v: string | null | undefined, record: KnowledgeGraphJsonImportItemResult) =>
        record.ok === false ? <Tag color="error">{t('knowledgegraph.importPage.actionFailed')}</Tag> : <Tag color={v === 'created' ? 'success' : v === 'updated' ? 'processing' : 'default'}>{t(`knowledgegraph.importPage.action${(v ?? '').charAt(0).toUpperCase()}${(v ?? '').slice(1)}`) || v}</Tag>,
    },
    { title: 'ID', dataIndex: 'id', key: 'id', width: 150, ellipsis: true, render: (v: string | null | undefined) => v || '-' },
    { title: t('knowledgegraph.importPage.columnMessage'), dataIndex: 'message', key: 'message', ellipsis: true, render: (v: string | null | undefined) => v || '-' },
  ]

  const summary = useMemo(() => {
    if (!result) return null
    const failed = (result.nodeFailedCount ?? 0) + (result.edgeFailedCount ?? 0)
    const touched = (result.nodeCreatedCount ?? 0) + (result.nodeUpdatedCount ?? 0) + (result.edgeCreatedCount ?? 0)
    return { failed, touched }
  }, [result])

  const run = async (validateOnly: boolean) => {
    setSubmitting(validateOnly ? submitting : true)
    setPreviewing(validateOnly)
    try {
      const res = await importKnowledgeGraphJson(graphId, { content, validateOnly, mode, autoCreateTypes, detectDuplicates })
      setResult(res)
      setWasPreview(validateOnly)
      if (!validateOnly && summaryTouched(res)) {
        feedback.success(t('knowledgegraph.importPage.success'))
        onImported()
      }
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setSubmitting(false)
      setPreviewing(false)
    }
  }

  const summaryTouched = (res: KnowledgeGraphJsonImportResult) =>
    (res.nodeCreatedCount ?? 0) + (res.nodeUpdatedCount ?? 0) + (res.edgeCreatedCount ?? 0) > 0

  const handleReadFile = (file: File) => {
    if (file.size > 4 * 1024 * 1024) {
      feedback.error(t('knowledgegraph.importPage.fileTooLarge'))
      return false
    }

    void file.text().then((text) => {
      setContent(text)
      setResult(null)
    })
    return false
  }

  return (
    <Space direction="vertical" size={spacing.md} style={{ width: '100%' }}>
      <Typography.Paragraph type="secondary" style={{ marginBottom: 0, fontSize: 13 }}>
        {t('knowledgegraph.importPage.jsonHint')}
      </Typography.Paragraph>

      <Upload.Dragger accept=".json,application/json" showUploadList={false} multiple={false} beforeUpload={handleReadFile}>
        <p className="ant-upload-drag-icon">
          <InboxOutlined />
        </p>
        <p className="ant-upload-text">{t('knowledgegraph.importPage.jsonUpload')}</p>
        <p className="ant-upload-hint">{t('knowledgegraph.importPage.fileHint')}</p>
      </Upload.Dragger>

      <Input.TextArea
        value={content}
        onChange={(e) => {
          setContent(e.target.value)
          setResult(null)
        }}
        placeholder={JSON_PLACEHOLDER}
        rows={12}
        style={{ fontFamily: 'monospace', fontSize: 12 }}
      />

      <Space wrap>
        <span>{t('knowledgegraph.importPage.mode')}</span>
        <Select
          style={{ width: 150 }}
          value={mode}
          onChange={(v) => setMode(v)}
          options={[
            { value: 'upsert', label: t('knowledgegraph.importPage.modeUpsert') },
            { value: 'create', label: t('knowledgegraph.importPage.modeCreate') },
          ]}
        />
        <Checkbox checked={autoCreateTypes} onChange={(e) => setAutoCreateTypes(e.target.checked)}>
          {t('knowledgegraph.importPage.autoCreateTypes')}
        </Checkbox>
        <Checkbox checked={detectDuplicates} onChange={(e) => setDetectDuplicates(e.target.checked)}>
          {t('knowledgegraph.importPage.detectDuplicates')}
        </Checkbox>
        <Button type="link" size="small" icon={<DownloadOutlined />} onClick={downloadSample}>
          {t('knowledgegraph.importPage.downloadSample')}
        </Button>
      </Space>

      <Space wrap>
        <Button icon={<SafetyCertificateOutlined />} loading={previewing} disabled={submitting || !content.trim()} onClick={() => void run(true)}>
          {t('knowledgegraph.importPage.preview')}
        </Button>
        <Button type="primary" loading={submitting} disabled={previewing || !content.trim()} onClick={() => void run(false)}>
          {t('knowledgegraph.importPage.submit')}
        </Button>
      </Space>

      {result && (
        <>
          <Alert
            type={summary?.failed ? (summary.touched ? 'warning' : 'error') : 'success'}
            showIcon
            message={wasPreview ? t('knowledgegraph.importPage.previewResultTitle') : t('knowledgegraph.importPage.resultTitle')}
            description={t('knowledgegraph.importPage.resultSummary', {
              nodeCreated: result.nodeCreatedCount ?? 0,
              nodeUpdated: result.nodeUpdatedCount ?? 0,
              nodeFailed: result.nodeFailedCount ?? 0,
              edgeCreated: result.edgeCreatedCount ?? 0,
              edgeSkipped: result.edgeSkippedCount ?? 0,
              edgeFailed: result.edgeFailedCount ?? 0,
            })}
          />
          {(result.duplicateSuspects?.length ?? 0) > 0 && (
            <Alert
              type="warning"
              showIcon
              message={t('knowledgegraph.importPage.duplicateTitle', { count: result.duplicateSuspects?.length ?? 0 })}
              description={t('knowledgegraph.importPage.duplicateHint')}
            />
          )}
          {(result.duplicateSuspects?.length ?? 0) > 0 && (
            <DataTable<KnowledgeGraphDuplicateSuspect>
              sticky
              rowKey={(record) => `${record.kind}-${record.index}-${record.matchIndex ?? record.matchNodeId ?? ''}`}
              columns={[
                { title: t('knowledgegraph.importPage.duplicateNewRow'), dataIndex: 'name', key: 'name', render: (_: unknown, record: KnowledgeGraphDuplicateSuspect) => `#${record.index} ${record.name}` },
                { title: t('knowledgegraph.importPage.duplicateMatched'), key: 'match', render: (_: unknown, record: KnowledgeGraphDuplicateSuspect) => record.kind === 'inbatch' ? `#${record.matchIndex} ${record.matchName}` : `${record.matchName}` },
                { title: t('knowledgegraph.importPage.duplicateSource'), dataIndex: 'kind', key: 'kind', width: 110, render: (v: string) => v === 'inbatch' ? t('knowledgegraph.importPage.duplicateInBatch') : t('knowledgegraph.importPage.duplicateExisting') },
                { title: t('knowledgegraph.importPage.duplicateScore'), dataIndex: 'score', key: 'score', width: 90, render: (v: number) => (v * 100).toFixed(1) + '%' },
              ]}
              dataSource={result.duplicateSuspects ?? []}
              pagination={false}
            />
          )}
          {(result.createdEntityTypeNames?.length ?? 0) > 0 && (
            <div>
              <span style={{ fontSize: 12, marginRight: spacing.sm }}>{t('knowledgegraph.importPage.createdTypes')}</span>
              {(result.createdEntityTypeNames ?? []).map((name) => (
                <Tag key={`e-${name}`} color="blue">{name}</Tag>
              ))}
              {(result.createdRelationTypeNames ?? []).map((name) => (
                <Tag key={`r-${name}`} color="purple">{name}</Tag>
              ))}
            </div>
          )}
          <DataTable<KnowledgeGraphJsonImportItemResult>
            sticky
            rowKey={(record) => `${record.kind}-${record.index}`}
            columns={itemColumns}
            dataSource={result.results ?? []}
            pagination={false}
          />
        </>
      )}
    </Space>
  )
}
