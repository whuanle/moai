/**
 * 调试运行面板：编辑启动参数（开始节点的 run 输入）并查看节点级执行结果.
 */

import { useState } from 'react'
import { Alert, Button, Input, Select, Table, Typography } from 'antd'
import { CaretRightOutlined } from '@ant-design/icons'
import { useTranslation } from 'react-i18next'
import type { WorkflowDebugRunResult, WorkflowNodeExecution } from '@/api/workflow'
import { formatDateTime } from '@/utils/datetime'
import { getNodeTemplate } from './constants'
import { useWorkflowDesignerStore } from './store'
import type { GlobalVariableDef } from './types'

const { Text } = Typography

export interface RunPanelProps {
  running: boolean
  result: WorkflowDebugRunResult | null
  onRun: (inputJson: string, systemJson: string) => void
}

/** 全局变量赋值：按声明的字段类型把字符串输入转为 JSON 值 */
function buildSystemJson(variables: GlobalVariableDef[], values: Record<string, string>): string {
  const json: Record<string, unknown> = {}
  for (const variable of variables) {
    if (!variable.name) continue
    const raw = values[variable.name] ?? variable.defaultValue ?? ''
    if (variable.fieldType === 'number') {
      const parsed = Number.parseFloat(raw)
      json[variable.name] = Number.isNaN(parsed) ? 0 : parsed
    } else if (variable.fieldType === 'boolean') {
      json[variable.name] = raw === 'true'
    } else {
      json[variable.name] = raw
    }
  }
  return JSON.stringify(json)
}

/** 启动参数模板：开始节点固定唯一启动参数 question（契约见 constants start 模板），不从画布推导以免疫旧草稿残留 */
function buildInputTemplate(): string {
  const template: Record<string, unknown> = {}
  for (const [name, binding] of Object.entries(getNodeTemplate('start')?.inputs ?? {})) {
    if (binding.required !== true || !name) continue
    template[name] = binding.fieldType === 'number' ? 0 : binding.fieldType === 'boolean' ? false : ''
  }
  return JSON.stringify(template, null, 2)
}

const STATE_COLOR: Record<string, string> = {
  completed: '#52c41a',
  failed: '#ff4d4f',
  running: '#1677ff',
  skipped: '#8c8c8c',
  pending: '#d9d9d9',
}

export function RunPanel({ running, result, onRun }: RunPanelProps) {
  const { t } = useTranslation()
  const variables = useWorkflowDesignerStore((s) => s.variables)
  const [inputOverride, setInputOverride] = useState<string>('')
  const [systemValues, setSystemValues] = useState<Record<string, string>>({})

  const inputJson = inputOverride || buildInputTemplate()

  const setSystemValue = (name: string, value: string) =>
    setSystemValues((prev) => ({ ...prev, [name]: value }))

  const columns = [
    {
      title: t('workflowDesigner.colNode'),
      dataIndex: 'nodeName',
      width: 110,
      render: (v: string, record: WorkflowNodeExecution) => (
        <span>
          <span className="wf-run-dot" style={{ background: STATE_COLOR[record.state ?? ''] ?? '#d9d9d9' }} />
          {v || record.nodeKey}
        </span>
      ),
    },
    { title: t('workflowDesigner.colState'), dataIndex: 'state', width: 80 },
    {
      title: t('workflowDesigner.colOutput'),
      dataIndex: 'output',
      ellipsis: true,
      render: (v: string | null) => (
        <Text style={{ fontSize: 12 }}>{v ? (v.length > 80 ? `${v.slice(0, 80)}…` : v) : '-'}</Text>
      ),
    },
    {
      title: t('workflowDesigner.colError'),
      dataIndex: 'errorMessage',
      ellipsis: true,
      render: (v: string | null) => (v ? <Text type="danger" style={{ fontSize: 12 }}>{v}</Text> : '-'),
    },
  ]

  return (
    <div className="wf-run-panel">
      <div className="wf-run-section">
        <div className="wf-fields-title">{t('workflowDesigner.runInput')}</div>
        <Input.TextArea
          rows={6}
          value={inputJson}
          onChange={(e) => setInputOverride(e.target.value)}
          className="wf-config-code"
        />
        {variables.length > 0 && (
          <div style={{ marginTop: 8 }}>
            <div className="wf-fields-title">{t('workflowDesigner.systemVars')}</div>
            {variables.map((variable) => (
              <div key={variable.name} className="wf-system-var-row">
                <span className="wf-system-var-name">{variable.name}</span>
                {variable.fieldType === 'boolean' ? (
                  <Select
                    size="small"
                    value={systemValues[variable.name] ?? variable.defaultValue ?? 'false'}
                    onChange={(v) => setSystemValue(variable.name, v)}
                    className="wf-system-var-input"
                    options={[
                      { value: 'true', label: 'true' },
                      { value: 'false', label: 'false' },
                    ]}
                  />
                ) : (
                  <Input
                    size="small"
                    value={systemValues[variable.name] ?? variable.defaultValue ?? ''}
                    onChange={(e) => setSystemValue(variable.name, e.target.value)}
                    placeholder={variable.description}
                    className="wf-system-var-input"
                  />
                )}
              </div>
            ))}
          </div>
        )}
        <Button
          type="primary"
          icon={<CaretRightOutlined />}
          loading={running}
          onClick={() => onRun(inputJson || '{}', buildSystemJson(variables, systemValues))}
          block
          style={{ marginTop: 8 }}
        >
          {t('workflowDesigner.run')}
        </Button>
      </div>

      {result && (
        <div className="wf-run-section">
          {result.status !== 'completed' ? (
            <Alert
              type={result.status === 'suspended' ? 'warning' : 'error'}
              message={t(`workflowDesigner.runState.${result.status}`, { defaultValue: result.status ?? '' })}
              description={result.errorMessage}
              showIcon
              style={{ marginBottom: 12 }}
            />
          ) : (
            <Alert
              type="success"
              message={t('workflowDesigner.runCompleted')}
              description={result.output}
              showIcon
              style={{ marginBottom: 12 }}
            />
          )}
          <div className="wf-fields-title">{t('workflowDesigner.runNodes')}</div>
          <Table
            size="small"
            rowKey={(r) => r.nodeKey ?? r.nodeName ?? 'node'}
            columns={columns}
            dataSource={result.nodes ?? []}
            pagination={false}
            expandable={{
              expandedRowRender: (record) => (
                <div className="wf-run-detail">
                  <div>IN: {record.input ?? '-'}</div>
                  <div>OUT: {record.output ?? '-'}</div>
                  <div>
                    {record.startedAt
                      ? `${formatDateTime(record.startedAt)} → ${record.endedAt ? formatDateTime(record.endedAt) : '-'}`
                      : ''}
                    {record.attempts ? ` · ${t('workflowDesigner.attempts', { count: record.attempts })}` : ''}
                  </div>
                </div>
              ),
            }}
          />
        </div>
      )}
    </div>
  )
}
