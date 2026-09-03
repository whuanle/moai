import { useEffect, useState } from 'react'
import { Col, Form, Modal, Row, Select, Spin, Switch } from 'antd'
import { useTranslation } from 'react-i18next'
import type { PluginClassify } from '@/api/classify'
import type { CustomPlugin, CustomKeyValue } from '@/api/plugin'
import { customPluginApi } from '@/api/plugin'
import { BaseFormFields } from './BaseFormFields'
import { KeyValueConfig } from './KeyValueConfig'

const HTTP_TRANSPORT_MODE_OPTIONS = [
  { value: 'AutoDetect', label: '自动检测 (AutoDetect)' },
  { value: 'StreamableHttp', label: '仅 Streamable HTTP (StreamableHttp)' },
  { value: 'Sse', label: '仅 HTTP with SSE (Sse)' },
]

interface KeyValueItem {
  key: string
  value: string
}

export interface McpFormValues {
  name: string
  title: string
  serverUrl: string
  classifyId?: number
  description?: string
  isPublic: boolean
  httpTransportMode?: string
  header?: KeyValueItem[]
  query?: KeyValueItem[]
}

const EMPTY_KEY_VALUES = (): KeyValueItem[] => []

interface McpPluginModalProps {
  open: boolean
  isEdit?: boolean
  editing?: CustomPlugin | null | undefined
  classifies: PluginClassify[]
  onOk: (values: McpFormValues) => Promise<void>
  onCancel: () => void
}

/** 将详情对象转为表单初始值（含 transportMode 回填）. */
function toFormValues(
  detail: Awaited<ReturnType<typeof customPluginApi.getCustomPluginDetail>>,
): McpFormValues {
  if (!detail) {
    return { name: '', title: '', serverUrl: '', isPublic: false, header: EMPTY_KEY_VALUES(), query: EMPTY_KEY_VALUES() }
  }  const header = (detail.header ?? []) as CustomKeyValue[]
  const query = (detail.query ?? []) as CustomKeyValue[]
  const transportMode = header.find((item) => item.key === '.HttpTransportMode')?.value
  return {
    name: detail.pluginName ?? '',
    title: detail.title ?? '',
    serverUrl: detail.server ?? '',
    classifyId: detail.classifyId ?? undefined,
    description: detail.description ?? '',
    isPublic: detail.isPublic ?? false,
    httpTransportMode: transportMode ?? undefined,
    header: header
      .filter((item) => item.key !== '.HttpTransportMode')
      .map((item) => ({ key: item.key ?? '', value: item.value ?? '' })),
    query: query.map((item) => ({ key: item.key ?? '', value: item.value ?? '' })),
  }
}

export function McpPluginModal({
  open,
  isEdit = false,
  editing,
  classifies,
  onOk,
  onCancel,
}: McpPluginModalProps) {
  const { t } = useTranslation()
  const [form] = Form.useForm<McpFormValues>()
  const [detailLoading, setDetailLoading] = useState(false)

  // 打开时根据插件拉取详情回填（编辑模式）
  useEffect(() => {
    if (!open) return
    form.resetFields()
    if (isEdit && editing?.pluginId) {
      setDetailLoading(true)
      customPluginApi
        .getCustomPluginDetail(editing.pluginId)
        .then((detail) => form.setFieldsValue(toFormValues(detail)))
        .finally(() => setDetailLoading(false))
    }
  }, [open, isEdit, editing, form])

  const handleOk = async () => {
    const values = await form.validateFields()
    await onOk(values)
  }

  return (
    <Modal
      title={isEdit ? t('plugins.editMcpTitle') : t('plugins.importMcpTitle')}
      open={open}
      onOk={handleOk}
      onCancel={onCancel}
      width={720}
      maskClosable={false}
      destroyOnClose
    >
      <Spin spinning={detailLoading} tip={t('plugins.refresh')}>
        <Form form={form} layout="vertical" style={{ marginTop: 16 }}>
          <BaseFormFields classifies={classifies} showServerUrl serverUrlLabel={t('plugins.formServerUrl')} showIsPublic={false} />
          <Row gutter={16}>
            <Col span={12}>
              <Form.Item name="httpTransportMode" label={t('plugins.formHttpTransportMode')}>
                <Select placeholder={t('plugins.formHttpTransportModePlaceholder')} allowClear options={HTTP_TRANSPORT_MODE_OPTIONS} />
              </Form.Item>
            </Col>
            <Col span={12}>
              <Form.Item name="isPublic" label={t('plugins.formIsPublic')} valuePropName="checked" initialValue={false}>
                <Switch checkedChildren={t('plugins.isPublic')} unCheckedChildren={t('plugins.notPublic')} />
              </Form.Item>
            </Col>
          </Row>
          <KeyValueConfig name="header" title="Header" />
          <KeyValueConfig name="query" title="Query" />
        </Form>
      </Spin>
    </Modal>
  )
}
