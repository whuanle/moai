/**
 * http 节点表单：请求配置（方法/地址/超时/cURL 导入）、请求参数（Params/Body/Headers）、
 * 鉴权配置、输出（报错捕获 + 输出字段提取 + 固定输出）.
 * 配置存 data.settings，保存时经 utils.sanitizeSettings 映射为引擎 config；
 * 地址/参数值/请求体支持 {引用} 插值（引擎 Interpolation 语义），输入处提供上游变量提示.
 */

import { useState } from 'react'
import { App, Button, Input, InputNumber, Modal, Popconfirm, Select, Switch, Tabs } from 'antd'
import { DeleteOutlined, PlusOutlined } from '@ant-design/icons'
import { useTranslation } from 'react-i18next'

import { useTemplateVariableOptions } from './node-form-shared'
import { SectionTitle, TemplateValueInput, TypeSelect } from './node-form-widgets'
import { parseCurlCommand } from './curl'
import type { HttpAuthSetting, HttpKvItem, NodeSettings, OutputField } from './types'

const HTTP_METHODS = ['GET', 'POST', 'PUT', 'DELETE', 'PATCH', 'HEAD']
const BODY_TYPES = ['none', 'json', 'form', 'text'] as const
const AUTH_TYPES = ['none', 'bearer', 'basic', 'apiKey'] as const
const TIMEOUT_MAX = 300

export interface HttpNodeFormData {
  outputs?: OutputField[]
  settings?: NodeSettings
}

const KV_NAME_PLACEHOLDER = 'name'

/** 键值对列表编辑（查询参数/请求头/表单字段）：值为插值模板输入，下拉可追加上游变量 */
function KvListEditor({
  list,
  namePlaceholder,
  valuePlaceholder,
  options,
  onChange,
}: {
  list: HttpKvItem[]
  namePlaceholder: string
  valuePlaceholder: string
  options: { value: string; label: string }[]
  onChange: (next: HttpKvItem[]) => void
}) {
  const { t } = useTranslation()
  return (
    <>
      {list.map((item, index) => (
        <div key={`${item.name}-${index}`} className="wf-b-row">
          <div className="wf-b-row-top">
            <Input
              size="small"
              value={item.name}
              onChange={(e) => onChange(list.map((it, i) => (i === index ? { ...it, name: e.target.value } : it)))}
              placeholder={namePlaceholder}
              className="wf-field-name"
            />
            <Popconfirm title={t('workflowDesigner.deleteFieldConfirm')} onConfirm={() => onChange(list.filter((_, i) => i !== index))}>
              <Button size="small" type="text" danger icon={<DeleteOutlined />} />
            </Popconfirm>
          </div>
          <TemplateValueInput
            value={item.value}
            options={options}
            placeholder={valuePlaceholder}
            onChange={(v) => onChange(list.map((it, i) => (i === index ? { ...it, value: v } : it)))}
          />
        </div>
      ))}
      <Button
        size="small"
        type="dashed"
        block
        icon={<PlusOutlined />}
        onClick={() => onChange([...list, { name: '', value: '' }])}
      >
        {t('workflowDesigner.httpAddKv')}
      </Button>
    </>
  )
}

export function HttpNodeForm({
  data,
  nodeId,
  onUpdateData,
}: {
  data: HttpNodeFormData
  nodeId: string
  onUpdateData: (patch: Partial<HttpNodeFormData>) => void
}) {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const variableOptions = useTemplateVariableOptions(nodeId)
  const settings = data.settings ?? {}

  const patchSettings = (patch: Partial<NodeSettings>) => {
    onUpdateData({ settings: { ...settings, ...patch } })
  }

  const [curlOpen, setCurlOpen] = useState(false)
  const [curlText, setCurlText] = useState('')

  const params = settings.params ?? []
  const headers = settings.headers ?? []
  const formEntries = settings.formEntries ?? []
  const extract = settings.extract ?? []
  const auth: HttpAuthSetting = settings.auth ?? { type: 'none' }
  const bodyType = settings.bodyType ?? 'none'

  const kvOnChange = (key: 'params' | 'headers' | 'formEntries') => (next: HttpKvItem[]) =>
    patchSettings({ [key]: next } as Partial<NodeSettings>)

  const handleCurlImport = () => {
    const parsed = parseCurlCommand(curlText)
    if (!parsed) {
      message.warning(t('workflowDesigner.httpCurlInvalid'))
      return
    }

    patchSettings({
      method: parsed.method,
      url: parsed.url,
      headers: parsed.headers,
      bodyType: parsed.bodyType,
      body: parsed.body,
      ...(parsed.formEntries ? { formEntries: parsed.formEntries } : {}),
      ...(parsed.auth ? { auth: parsed.auth } : {}),
    })
    setCurlOpen(false)
    setCurlText('')
    message.success(t('workflowDesigner.httpCurlApplied'))
  }

  const requestSection = (
    <div className="wf-node-sec">
      <SectionTitle
        text={t('workflowDesigner.httpRequestConfig')}
        extra={
          <Button type="link" size="small" className="wf-sec-extra-link" onClick={() => setCurlOpen(true)}>
            {t('workflowDesigner.httpCurlImport')}
          </Button>
        }
      />
      <div className="wf-b-row-value">
        <Select
          size="small"
          className="wf-field-expr-type"
          popupMatchSelectWidth={false}
          value={settings.method ?? 'GET'}
          onChange={(v) => patchSettings({ method: v })}
          options={HTTP_METHODS.map((m) => ({ value: m, label: m }))}
        />
        <TemplateValueInput
          value={settings.url ?? ''}
          options={variableOptions}
          placeholder={t('workflowDesigner.httpUrlPlaceholder')}
          onChange={(v) => patchSettings({ url: v })}
        />
      </div>
      <div className="wf-b-row" style={{ marginTop: 8 }}>
        <span className="wf-out-name" style={{ whiteSpace: 'nowrap' }}>{t('workflowDesigner.httpTimeout')}</span>
        <InputNumber
          size="small"
          min={1}
          max={TIMEOUT_MAX}
          precision={0}
          style={{ width: 88 }}
          value={typeof settings.timeoutSeconds === 'number' && settings.timeoutSeconds >= 1 ? settings.timeoutSeconds : 30}
          onChange={(v) => {
            if (v == null) return
            patchSettings({ timeoutSeconds: Math.min(Math.max(Number(v), 1), TIMEOUT_MAX) })
          }}
          addonAfter="s"
        />
      </div>
      <div className="wf-config-hint">{t('workflowDesigner.httpUrlHint')}</div>
    </div>
  )

  const paramsTab = <KvListEditor list={params} namePlaceholder={KV_NAME_PLACEHOLDER} valuePlaceholder={t('workflowDesigner.httpKvValuePlaceholder')} options={variableOptions} onChange={kvOnChange('params')} />
  const headersTab = <KvListEditor list={headers} namePlaceholder={KV_NAME_PLACEHOLDER} valuePlaceholder={t('workflowDesigner.httpKvValuePlaceholder')} options={variableOptions} onChange={kvOnChange('headers')} />

  const bodyTab = (
    <>
      <div className="wf-b-row">
        <Select
          size="small"
          style={{ width: '100%' }}
          popupMatchSelectWidth={false}
          value={bodyType}
          onChange={(v) => patchSettings({ bodyType: v })}
          options={BODY_TYPES.map((bt) => ({ value: bt, label: t(`workflowDesigner.httpBodyType_${bt}`) }))}
        />
      </div>
      {bodyType === 'json' && (
        <>
          <Input.TextArea
            size="small"
            rows={4}
            value={settings.body ?? ''}
            placeholder={t('workflowDesigner.httpBodyPlaceholder')}
            onChange={(e) => patchSettings({ body: e.target.value })}
            className="wf-config-code"
          />
          <div className="wf-config-hint">{t('workflowDesigner.httpBodyHint')}</div>
        </>
      )}
      {bodyType === 'text' && (
        <Input.TextArea
          size="small"
          rows={4}
          value={settings.body ?? ''}
          placeholder={t('workflowDesigner.httpBodyPlaceholder')}
          onChange={(e) => patchSettings({ body: e.target.value })}
        />
      )}
      {bodyType === 'form' && (
        <KvListEditor list={formEntries} namePlaceholder={KV_NAME_PLACEHOLDER} valuePlaceholder={t('workflowDesigner.httpKvValuePlaceholder')} options={variableOptions} onChange={kvOnChange('formEntries')} />
      )}
    </>
  )

  const authSection = (
    <div className="wf-node-sec">
      <SectionTitle text={t('workflowDesigner.httpAuth')} />
      <Select
        size="small"
        style={{ width: '100%' }}
        popupMatchSelectWidth={false}
        value={auth.type}
        onChange={(v) => patchSettings({ auth: { ...auth, type: v } })}
        options={AUTH_TYPES.map((at) => ({ value: at, label: t(`workflowDesigner.httpAuthType_${at}`) }))}
      />
      {auth.type === 'bearer' && (
        <div className="wf-b-row" style={{ marginTop: 8 }}>
          <TemplateValueInput value={auth.token ?? ''} options={variableOptions} placeholder={t('workflowDesigner.httpAuthToken')} onChange={(v) => patchSettings({ auth: { ...auth, token: v } })} />
        </div>
      )}
      {auth.type === 'basic' && (
        <>
          <div className="wf-b-row" style={{ marginTop: 8 }}>
            <TemplateValueInput value={auth.username ?? ''} options={variableOptions} placeholder={t('workflowDesigner.httpAuthUsername')} onChange={(v) => patchSettings({ auth: { ...auth, username: v } })} />
          </div>
          <div className="wf-b-row">
            <TemplateValueInput value={auth.password ?? ''} options={variableOptions} placeholder={t('workflowDesigner.httpAuthPassword')} onChange={(v) => patchSettings({ auth: { ...auth, password: v } })} />
          </div>
        </>
      )}
      {auth.type === 'apiKey' && (
        <>
          <div className="wf-b-row" style={{ marginTop: 8 }}>
            <Input size="small" value={auth.headerName ?? ''} placeholder={t('workflowDesigner.httpAuthHeaderName')} onChange={(e) => patchSettings({ auth: { ...auth, headerName: e.target.value } })} />
          </div>
          <div className="wf-b-row">
            <TemplateValueInput value={auth.headerValue ?? ''} options={variableOptions} placeholder={t('workflowDesigner.httpAuthHeaderValue')} onChange={(v) => patchSettings({ auth: { ...auth, headerValue: v } })} />
          </div>
        </>
      )}
    </div>
  )

  const outputSection = (
    <>
      <div className="wf-node-sec">
        <SectionTitle text={t('workflowDesigner.outputFields')} />
        <div className="wf-branch-row">
          <span className="wf-branch-label">{t('workflowDesigner.httpErrorCapture')}</span>
          <Switch
            size="small"
            checked={settings.errorCapture === true}
            onChange={(v) => patchSettings({ errorCapture: v })}
          />
        </div>
        <div className="wf-config-hint">{t('workflowDesigner.httpErrorCaptureHint')}</div>
        <div className="wf-outs" style={{ marginTop: 8 }}>
          {(data.outputs ?? []).map((o, i) => (
            <div key={`${o.name}-${i}`} className="wf-out-row">
              <span className="wf-out-name">{o.name}</span>
              <span className="wf-out-type">{o.fieldType}</span>
              {o.description && <span className="wf-out-desc" title={o.description}>{o.description}</span>}
            </div>
          ))}
        </div>
      </div>
      <div className="wf-node-sec">
        <SectionTitle text={t('workflowDesigner.httpExtract')} />
        {extract.map((field, index) => (
          <div key={`${field.name}-${index}`} className="wf-b-row">
            <div className="wf-b-row-top">
              <Input
                size="small"
                value={field.name}
                onChange={(e) => patchSettings({ extract: extract.map((f, i) => (i === index ? { ...f, name: e.target.value.replace(/[^a-zA-Z0-9_]/g, '') } : f)) })}
                placeholder={t('workflowDesigner.httpExtractName')}
                className="wf-field-name"
              />
              <TypeSelect value={field.fieldType} onChange={(v) => patchSettings({ extract: extract.map((f, i) => (i === index ? { ...f, fieldType: v } : f)) })} />
              <Popconfirm title={t('workflowDesigner.deleteFieldConfirm')} onConfirm={() => patchSettings({ extract: extract.filter((_, i) => i !== index) })}>
                <Button size="small" type="text" danger icon={<DeleteOutlined />} />
              </Popconfirm>
            </div>
            <Input
              size="small"
              value={field.path}
              onChange={(e) => patchSettings({ extract: extract.map((f, i) => (i === index ? { ...f, path: e.target.value } : f)) })}
              placeholder="$.data.title"
            />
          </div>
        ))}
        <Button
          size="small"
          type="dashed"
          block
          icon={<PlusOutlined />}
          onClick={() => patchSettings({ extract: [...extract, { name: '', path: '', fieldType: 'string' }] })}
        >
          {t('workflowDesigner.httpAddExtract')}
        </Button>
        <div className="wf-config-hint">{t('workflowDesigner.httpExtractHint')}</div>
      </div>
    </>
  )

  return (
    <div className="wf-node-form">
      {requestSection}
      <div className="wf-node-sec">
        <SectionTitle text={t('workflowDesigner.httpParams')} />
        <Tabs
          size="small"
          className="wf-http-tabs"
          items={[
            { key: 'params', label: 'Params', children: paramsTab },
            { key: 'body', label: 'Body', children: bodyTab },
            { key: 'headers', label: 'Headers', children: headersTab },
          ]}
        />
      </div>
      {authSection}
      {outputSection}
      <Modal
        title={t('workflowDesigner.httpCurlTitle')}
        open={curlOpen}
        maskClosable={false}
        onCancel={() => setCurlOpen(false)}
        onOk={handleCurlImport}
        okText={t('workflowDesigner.httpCurlApply')}
        width={520}
      >
        <Input.TextArea
          rows={8}
          value={curlText}
          placeholder={t('workflowDesigner.httpCurlPlaceholder')}
          onChange={(e) => setCurlText(e.target.value)}
          className="wf-config-code"
        />
      </Modal>
    </div>
  )
}
