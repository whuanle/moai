import { useCallback, useEffect, useMemo, useState } from 'react'
import {
  DeleteOutlined,
  DownloadOutlined,
  EditOutlined,
  SearchOutlined,
  UploadOutlined,
  FileTextOutlined,
  ThunderboltOutlined,
} from '@ant-design/icons'
import { Button, Input, List, Popconfirm, Progress, Radio, Space, Tag, Tooltip, Typography, Upload, Modal } from 'antd'
import type { TableColumnsType } from 'antd'
import type { UploadProps } from 'antd'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router'
import { DataTable, feedback } from '@/design-system'
import { spacing } from '@/design-system/theme'
import { formatFileSize } from '@/utils/format'
import { formatDateTime } from '@/utils/datetime'
import {
  getWikiDocuments,
  preUploadWikiDocument,
  completeWikiDocument,
  deleteWikiDocuments,
  downloadWikiDocument,
  renameWikiDocument,
  type WikiDocumentItem,
} from '@/api/wiki'

const { Text } = Typography

/** 上传状态 */
interface UploadStatus {
  file: File
  status: 'waiting' | 'uploading' | 'success' | 'error'
  progress: number
  message?: string
}

/** 支持的文件扩展名 */
const SUPPORTED_EXTENSIONS = ['.pdf', '.doc', '.docx', '.txt', '.md', '.xls', '.xlsx', '.ppt', '.pptx', '.csv', '.json', '.xml', '.html', '.htm']

const isSupported = (file: File): boolean => {
  const ext = file.name.slice(file.name.lastIndexOf('.')).toLowerCase()
  return SUPPORTED_EXTENSIONS.includes(ext)
}

const getErrorMessage = (error: unknown, defaultMessage: string): string => {
  if (error instanceof Error) return error.message || defaultMessage
  return defaultMessage
}

export function WikiDocuments({ wikiId, teamId }: { wikiId: number; teamId?: number }) {
  const { t } = useTranslation()
  const navigate = useNavigate()

  const [loading, setLoading] = useState(false)
  const [items, setItems] = useState<WikiDocumentItem[]>([])
  const [total, setTotal] = useState(0)
  const [pageNo, setPageNo] = useState(1)
  const [pageSize, setPageSize] = useState(20)
  const [searchText, setSearchText] = useState('')
  const [embeddingFilter, setEmbeddingFilter] = useState<boolean | null>(null)

  // 重命名状态
  const [renamingId, setRenamingId] = useState<number | null>(null)
  const [renameValue, setRenameValue] = useState('')

  // 多选
  const [selectedRowKeys, setSelectedRowKeys] = useState<number[]>([])

  // 上传
  const [uploadOpen, setUploadOpen] = useState(false)
  const [selectedFiles, setSelectedFiles] = useState<File[]>([])
  const [uploadStatuses, setUploadStatuses] = useState<UploadStatus[]>([])
  const [uploading, setUploading] = useState(false)

  const load = useCallback(
    async (page = pageNo, size = pageSize, keyword = searchText, embedding = embeddingFilter) => {
      if (!Number.isFinite(wikiId) || wikiId <= 0) return
      setLoading(true)
      try {
        const res = await getWikiDocuments(wikiId, {
          pageNo: page,
          pageSize: size,
          query: keyword || undefined,
          isEmbedding: embedding,
        })
        setItems(res.items ?? [])
        setTotal(res.total ?? 0)
      } catch {
        // 错误已由全局请求中间件统一提示
      } finally {
        setLoading(false)
      }
    },
    [wikiId, pageNo, pageSize, searchText, embeddingFilter],
  )

  useEffect(() => {
    void load(1, pageSize, '')
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [wikiId])

  const handleSearch = () => {
    setPageNo(1)
    void load(1, pageSize, searchText, embeddingFilter)
  }

  const handleRefresh = () => {
    void load(pageNo, pageSize, searchText, embeddingFilter)
  }

  const handleEmbeddingFilter = (value: boolean | null) => {
    setEmbeddingFilter(value)
    setPageNo(1)
    void load(1, pageSize, searchText, value)
  }

  const handleDelete = async (documentIds: number[]) => {
    if (documentIds.length === 0) return
    try {
      await deleteWikiDocuments(wikiId, documentIds)
      feedback.success(t('wiki.doc.deleteSuccess', { count: documentIds.length }))
      setSelectedRowKeys([])
      void load(pageNo, pageSize, searchText)
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }

  const handleDownload = async (record: WikiDocumentItem) => {
    try {
      const url = await downloadWikiDocument(wikiId, Number(record.documentId))
      if (!url) {
        feedback.error(t('wiki.doc.downloadFailed'))
        return
      }
      // 通过 blob 强制下载，避免浏览器直接预览 PDF/图片
      const resp = await fetch(url)
      if (!resp.ok) {
        feedback.error(t('wiki.doc.downloadFailed'))
        return
      }
      const blob = await resp.blob()
      const objectUrl = URL.createObjectURL(blob)
      const link = document.createElement('a')
      link.href = objectUrl
      link.download = record.fileName || 'download'
      document.body.appendChild(link)
      link.click()
      document.body.removeChild(link)
      URL.revokeObjectURL(objectUrl)
      feedback.success(t('wiki.doc.downloadStarted'))
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }

  const handleRename = async (record: WikiDocumentItem) => {
    const name = renameValue.trim()
    if (!name) {
      setRenamingId(null)
      return
    }
    try {
      await renameWikiDocument(wikiId, Number(record.documentId), name)
      feedback.success(t('wiki.doc.renameSuccess'))
      setRenamingId(null)
      setRenameValue('')
      void load(pageNo, pageSize, searchText)
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }

  const handleVectorize = (record: WikiDocumentItem) => {
    const team = teamId ?? 0
    navigate(`/team/${team}/wiki/${wikiId}/document/${Number(record.documentId)}/embedding`)
  }

  const handleSelectFiles: UploadProps['beforeUpload'] = (file) => {
    if (!isSupported(file)) {
      feedback.error(t('wiki.doc.unsupportedType'))
      return Upload.LIST_IGNORE
    }
    setSelectedFiles((prev) => [...prev, file])
    return false
  }

  const handleUpload = async () => {
    if (selectedFiles.length === 0) return
    setUploading(true)
    const statuses: UploadStatus[] = selectedFiles.map((file) => ({ file, status: 'waiting', progress: 0 }))
    setUploadStatuses(statuses)

    let successCount = 0
    for (let i = 0; i < selectedFiles.length; i++) {
      const file = selectedFiles[i]
      setUploadStatuses((prev) => prev.map((s, idx) => (idx === i ? { ...s, status: 'uploading', progress: 50 } : s)))
      try {
        const pre = await preUploadWikiDocument(wikiId, file)
        if (!pre) throw new Error(t('wiki.doc.uploadFailed'))
        const fileId = Number(pre.fileId)
        if (!fileId) throw new Error(t('wiki.doc.uploadFailed'))
        if (!pre.isExist && pre.uploadUrl) {
          await putToUrl(pre.uploadUrl, file)
        }
        await completeWikiDocument(wikiId, { fileId, fileName: file.name, isSuccess: true })
        setUploadStatuses((prev) => prev.map((s, idx) => (idx === i ? { ...s, status: 'success', progress: 100 } : s)))
        successCount++
      } catch (error) {
        setUploadStatuses((prev) =>
          prev.map((s, idx) => (idx === i ? { ...s, status: 'error', progress: 0, message: getErrorMessage(error, t('wiki.doc.uploadFailed')) } : s)),
        )
      }
    }

    setUploading(false)
    if (successCount > 0) {
      feedback.success(t('wiki.doc.uploadSuccess', { count: successCount }))
      void load(pageNo, pageSize, searchText)
    }
    // 全部成功则自动关闭上传窗体
    if (successCount === selectedFiles.length) {
      closeUploadModal()
      return
    }
    // 清理成功的文件
    setSelectedFiles((prev) => prev.filter((_, i) => i >= selectedFiles.length || uploadStatuses[i]?.status !== 'success'))
  }

  const closeUploadModal = () => {
    setUploadOpen(false)
    setSelectedFiles([])
    setUploadStatuses([])
  }

  const columns: TableColumnsType<WikiDocumentItem> = useMemo(
    () => [
      {
        title: t('wiki.doc.colFileName'),
        dataIndex: 'fileName',
        key: 'fileName',
        width: 260,
        render: (text: string, record) => {
          if (renamingId === Number(record.documentId)) {
            return (
              <Input
                value={renameValue}
                onChange={(e) => setRenameValue(e.target.value)}
                onPressEnter={() => void handleRename(record)}
                onBlur={() => setRenamingId(null)}
                autoFocus
                suffix={
                  <Space size={0}>
                    <Button type="text" size="small" icon={<EditOutlined />} onClick={() => void handleRename(record)} aria-label={t('wiki.doc.confirmRename')} />
                  </Space>
                }
              />
            )
          }
          return (
            <Space>
              <FileTextOutlined style={{ color: 'var(--color-primary)' }} />
              <Tooltip title={t('wiki.doc.doubleClickRename')}>
                <span
                  style={{ cursor: 'pointer' }}
                  onDoubleClick={() => {
                    setRenamingId(Number(record.documentId))
                    setRenameValue(text || '')
                  }}
                >
                  {text || '-'}
                </span>
              </Tooltip>
            </Space>
          )
        },
      },
      {
        title: t('wiki.doc.colFileSize'),
        dataIndex: 'fileSize',
        key: 'fileSize',
        width: 110,
        render: (v: number | null | undefined) => formatFileSize(v ?? 0),
      },
      {
        title: t('wiki.doc.colType'),
        dataIndex: 'contentType',
        key: 'contentType',
        width: 130,
        render: (v: string | null | undefined) => <Tag>{v || '-'}</Tag>,
      },
      {
        title: t('wiki.doc.colCreateTime'),
        dataIndex: 'createTime',
        key: 'createTime',
        width: 160,
        render: (v: string | null | undefined) => <span style={{ whiteSpace: 'nowrap' }}>{formatDateTime(v)}</span>,
      },
      {
        title: t('wiki.doc.colCreateUser'),
        dataIndex: 'createUserName',
        key: 'createUserName',
        width: 120,
        render: (v: string | null | undefined) => v || '-',
      },
      {
        title: t('wiki.doc.colChunkCount'),
        dataIndex: 'chunkCount',
        key: 'chunkCount',
        width: 90,
        align: 'center',
        render: (v: number | null | undefined) => v ?? 0,
      },
      {
        title: t('wiki.doc.colEmbedding'),
        dataIndex: 'isEmbedding',
        key: 'isEmbedding',
        width: 100,
        align: 'center',
        render: (v: boolean | null | undefined) =>
          v ? (
            <Tag color="green">{t('wiki.doc.embeddingDone')}</Tag>
          ) : (
            <Tag color="orange">{t('wiki.doc.embeddingNone')}</Tag>
          ),
      },
      {
        title: t('wiki.doc.colActions'),
        key: 'actions',
        width: 170,
        fixed: 'right',
        render: (_, record) => (
          <Space size={0}>
            <Tooltip title={t('wiki.doc.vectorize')}>
              <Button
                type="text"
                size="small"
                icon={<ThunderboltOutlined />}
                aria-label={t('wiki.doc.vectorize')}
                onClick={() => handleVectorize(record)}
              />
            </Tooltip>
            <Tooltip title={t('wiki.doc.download')}>
              <Button
                type="text"
                size="small"
                icon={<DownloadOutlined />}
                aria-label={t('wiki.doc.download')}
                onClick={() => void handleDownload(record)}
              />
            </Tooltip>
            <Popconfirm title={t('wiki.doc.deleteConfirm')} okButtonProps={{ danger: true }} onConfirm={() => void handleDelete([Number(record.documentId)])}>
              <Tooltip title={t('wiki.doc.delete')}>
                <Button type="text" size="small" danger icon={<DeleteOutlined />} aria-label={t('wiki.doc.delete')} />
              </Tooltip>
            </Popconfirm>
          </Space>
        ),
      },
    ],
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [t, renamingId, renameValue],
  )

  return (
    <>
      <DataTable<WikiDocumentItem>
        rowKey={(record) => String(record.documentId)}
        columns={columns}
        dataSource={items}
        loading={loading}
        sticky
        scroll={{ x: 1050 }}
        toolbar={
          <Space wrap>
            <Input
              placeholder={t('wiki.doc.searchPlaceholder')}
              allowClear
              prefix={<SearchOutlined style={{ color: 'inherit' }} />}
              value={searchText}
              onChange={(e) => setSearchText(e.target.value)}
              onPressEnter={handleSearch}
              style={{ width: 240 }}
            />
            <Radio.Group
              value={embeddingFilter === null ? 'all' : embeddingFilter ? 'done' : 'none'}
              onChange={(e) => {
                const v = e.target.value
                handleEmbeddingFilter(v === 'all' ? null : v === 'done' ? true : false)
              }}
              buttonStyle="solid"
              size="middle"
              options={[
                { label: t('wiki.doc.embeddingAll'), value: 'all' },
                { label: t('wiki.doc.embeddingDone'), value: 'done' },
                { label: t('wiki.doc.embeddingNone'), value: 'none' },
              ]}
            />
            <Button type="primary" icon={<UploadOutlined />} onClick={() => setUploadOpen(true)}>
              {t('wiki.doc.upload')}
            </Button>
            <Button icon={<DeleteOutlined />} danger disabled={selectedRowKeys.length === 0} onClick={() => void handleDelete(selectedRowKeys)}>
              {t('wiki.doc.batchDelete')}
            </Button>
          </Space>
        }
        onRefresh={handleRefresh}
        refreshLoading={loading}
        pagination={{
          current: pageNo,
          pageSize,
          total,
          showSizeChanger: true,
          onChange: (page, size) => {
            setPageNo(page)
            setPageSize(size)
            void load(page, size, searchText)
          },
        }}
        rowSelection={{ selectedRowKeys, onChange: (keys) => setSelectedRowKeys(keys as number[]) }}
      />

      <Modal
        title={t('wiki.doc.uploadTitle')}
        open={uploadOpen}
        onCancel={closeUploadModal}
        maskClosable={false}
        footer={[
          <Button key="cancel" onClick={closeUploadModal}>
            {t('wiki.cancel')}
          </Button>,
          <Button key="upload" type="primary" icon={<UploadOutlined />} loading={uploading} disabled={selectedFiles.length === 0} onClick={() => void handleUpload()}>
            {t('wiki.doc.startUpload', { count: selectedFiles.length })}
          </Button>,
        ]}
      >
        <Upload.Dragger multiple showUploadList={false} beforeUpload={handleSelectFiles} accept={SUPPORTED_EXTENSIONS.join(',')}>
          <p className="ant-upload-drag-icon">
            <UploadOutlined />
          </p>
          <p className="ant-upload-text">{t('wiki.doc.uploadDragText')}</p>
          <p className="ant-upload-hint">{t('wiki.doc.uploadHint')}</p>
        </Upload.Dragger>
        {selectedFiles.length > 0 && (
          <List
            size="small"
            style={{ marginTop: spacing.md, maxHeight: 200, overflow: 'auto' }}
            dataSource={selectedFiles}
            renderItem={(file, index) => {
              const status = uploadStatuses[index]
              return (
                <List.Item
                  actions={[
                    <Button
                      key="del"
                      type="text"
                      size="small"
                      danger
                      icon={<DeleteOutlined />}
                      onClick={() => setSelectedFiles((prev) => prev.filter((_, i) => i !== index))}
                    />,
                  ]}
                >
                  <List.Item.Meta
                    title={file.name}
                    description={
                      status?.status === 'uploading' ? (
                        <Progress percent={status.progress} size="small" />
                      ) : status?.status === 'error' ? (
                        <Text type="danger">{status.message}</Text>
                      ) : status?.status === 'success' ? (
                        <Text type="success">{t('wiki.doc.uploaded')}</Text>
                      ) : (
                        formatFileSize(file.size)
                      )
                    }
                  />
                </List.Item>
              )
            }}
          />
        )}
      </Modal>
    </>
  )
}

async function putToUrl(uploadUrl: string, file: File): Promise<void> {
  const res = await fetch(uploadUrl, {
    method: 'PUT',
    headers: { 'Content-Type': file.type || 'application/octet-stream' },
    body: file,
  })
  if (!res.ok) throw new Error(`上传失败(${res.status})`)
}
