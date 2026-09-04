import { useCallback, useEffect, useState } from 'react'
import { Alert, Form, Modal, Progress, Spin, Upload } from 'antd'
import { UploadOutlined } from '@ant-design/icons'
import { useTranslation } from 'react-i18next'
import type { UploadFile } from 'antd'
import type { RcFile } from 'antd/es/upload/interface'
import type { PluginClassify } from '@/api/classify'
import type { CustomPlugin, CustomPluginDetail, CustomKeyValue } from '@/api/plugin'
import { customPluginApi } from '@/api/plugin'
import { uploadOpenApiFile } from '@/utils/pluginFile'
import { feedback } from '@/design-system'
import { BaseFormFields } from './BaseFormFields'
import { KeyValueConfig } from './KeyValueConfig'

interface KeyValueItem {
  key: string
  value: string
}

export interface OpenApiFormValues {
  name: string
  title: string
  serverUrl: string
  classifyId?: number
  description?: string
  isPublic: boolean
  header?: KeyValueItem[]
}

const EMPTY_KEY_VALUES = (): KeyValueItem[] => []

interface OpenApiModalProps {
  open: boolean
  isEdit?: boolean
  editing?: CustomPlugin | null | undefined
  classifies: PluginClassify[]
  onOk: (values: OpenApiFormValues, file: File | null, fileId?: string) => Promise<void>
  onCancel: () => void
}

/** 将详情回填为表单初始值（OpenApi 编辑用）. */
function fromDetail(detail: CustomPluginDetail | null): OpenApiFormValues {
  if (!detail) {
    return { name: '', title: '', serverUrl: '', isPublic: false, header: EMPTY_KEY_VALUES() }
  }
  const header = (detail.header ?? []) as CustomKeyValue[]
  return {
    name: detail.pluginName ?? '',
    title: detail.title ?? '',
    serverUrl: detail.server ?? '',
    classifyId: detail.classifyId ?? undefined,
    description: detail.description ?? '',
    isPublic: detail.isPublic ?? false,
    header: header.map((item) => ({ key: item.key ?? '', value: item.value ?? '' })),
  }
}

export function OpenApiModal({
  open,
  isEdit = false,
  editing,
  classifies,
  onOk,
  onCancel,
}: OpenApiModalProps) {
  const { t } = useTranslation()
  const [form] = Form.useForm<OpenApiFormValues>()
  const [selectedFile, setSelectedFile] = useState<UploadFile | null>(null)
  const [uploadedFileId, setUploadedFileId] = useState<string | undefined>(undefined)
  const [currentFileName, setCurrentFileName] = useState<string | undefined>(undefined)
  const [loading, setLoading] = useState(false)
  const [detailLoading, setDetailLoading] = useState(false)
  const [progress, setProgress] = useState(0)

  // 打开时根据插件拉取详情回填（编辑模式）
  useEffect(() => {
    if (!open) return
    setSelectedFile(null)
    setUploadedFileId(undefined)
    setCurrentFileName(undefined)
    setProgress(0)
    if (isEdit && editing?.pluginId) {
      setDetailLoading(true)
      customPluginApi
        .getCustomPluginDetail(editing.pluginId)
        .then((detail) => {
          setCurrentFileName(detail?.openapiFileName ?? undefined)
          form.setFieldsValue(fromDetail(detail))
        })
        .finally(() => setDetailLoading(false))
    } else {
      form.resetFields()
    }
  }, [open, isEdit, editing, form])

  const handleCancel = useCallback(() => {
    setSelectedFile(null)
    setUploadedFileId(undefined)
    setCurrentFileName(undefined)
    setProgress(0)
    form.resetFields()
    onCancel()
  }, [form, onCancel])

  // 选择文件后立即上传，成功后回填 fileId
  const handleSelectFile = async (file: RcFile) => {
    setSelectedFile({
      uid: crypto.randomUUID(),
      name: file.name,
      originFileObj: file,
      status: 'uploading',
    })
    setProgress(10)
    try {
      const name = await form.getFieldValue('name')
      const uploaded = await uploadOpenApiFile(file, name || '')
      setUploadedFileId(uploaded.fileId)
      setProgress(100)
      setSelectedFile((prev) => (prev ? { ...prev, status: 'done' } : prev))
    } catch {
      setSelectedFile(null)
      setProgress(0)
      setUploadedFileId(undefined)
      feedback.error(t('plugins.uploadOpenApiFailed'))
    }
  }

  const handleOk = async () => {
    const values = await form.validateFields()
    if (!uploadedFileId) {
      feedback.warning(t('plugins.formUploadOpenApiRequired'))
      return
    }
    setLoading(true)
    try {
      await onOk(values, selectedFile?.originFileObj ?? null, uploadedFileId)
      setProgress(100)
    } finally {
      setLoading(false)
    }
  }

  const beforeUpload = (file: RcFile) => {
    setSelectedFile(null)
    setUploadedFileId(undefined)
    void handleSelectFile(file)
    return Upload.LIST_IGNORE
  }

  const fileList: UploadFile[] = selectedFile ? [selectedFile] : []

  return (
    <Modal
      title={isEdit ? t('plugins.editOpenApiTitle') : t('plugins.importOpenApiTitle')}
      open={open}
      onOk={handleOk}
      onCancel={handleCancel}
      width={720}
      maskClosable={false}
      destroyOnClose
      confirmLoading={loading}
    >
      <Spin spinning={detailLoading} tip={t('plugins.refresh')}>
        <Form form={form} layout="vertical" style={{ marginTop: 16 }}>
          <BaseFormFields classifies={classifies} showServerUrl serverUrlLabel={t('plugins.formServerUrlExtra')} />
          <Form.Item label={isEdit ? t('plugins.formOpenApiUploadOptional') : t('plugins.formUploadOpenApi')} required={!isEdit}>
            {currentFileName && (
              <Alert message={t('plugins.formCurrentFile', { name: currentFileName })} type="info" showIcon style={{ marginBottom: 8 }} />
            )}
            <Upload.Dragger
              accept=".json,.yaml,.yml"
              maxCount={1}
              beforeUpload={beforeUpload}
              fileList={fileList}
              onRemove={() => {
                setSelectedFile(null)
                setUploadedFileId(undefined)
                setProgress(0)
              }}
              showUploadList={{ showRemoveIcon: true, showPreviewIcon: false }}
              disabled={loading}
            >
              <p className="ant-upload-drag-icon">
                <UploadOutlined style={{ fontSize: 36 }} />
              </p>
              <p className="ant-upload-text">{t('plugins.formUploadOpenApi')}</p>
              <p className="ant-upload-hint">{t('plugins.formUploadHint')}</p>
            </Upload.Dragger>
            {progress > 0 && progress < 100 && <Progress percent={progress} size="small" style={{ marginTop: 8 }} />}
          </Form.Item>
          <KeyValueConfig name="header" title="Header" />
        </Form>
      </Spin>
    </Modal>
  )
}
