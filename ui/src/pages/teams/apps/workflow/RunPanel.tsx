/**
 * 调试运行面板：编辑启动参数（开始节点的 run 输入）并查看节点级执行结果.
 */

import { useState } from 'react'
import { Alert, Button, Input, Table, Typography } from 'antd'
import { CaretRightOutlined } from '@ant-design/icons'
import { useTranslation } from 'react-i18next'
import type { WorkflowDebugRunResult, WorkflowNodeExecution } from '@/api/workflow'
import { formatDateTime } from '@/utils/datetime'
import { useWorkflowDesignerStore } from './store'
import type { EditorWorkflowJSON } from './types'

const { Text } = Typography

export interface RunPanelProps {
  running: boolean
  result: WorkflowDebugRunResult | null
  onRun: (inputJson: string) => void
}

/** 从画布提取开始节点的必需启动参数，生成输入模板 */
function buildInputTemplate(editorJSON: EditorWorkflowJSON | null): string {
  const startNode = editorJSON?.nodes?.find((n) => n.type === 'start')
  const template: Record<string, unknown> = {}
  for (const output of startNode?.data?.outputs ?? []) {
    if (output.isRequired !== true || !output.name) continue
    template[output.name] = output.fieldType === 'number' ? 0 : output.fieldType === 'boolean' ? false : ''
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
  const canvasJSON = useWorkflowDesignerStore((s) => s.editorJSON ?? s.initialData)
  const [inputOverride, setInputOverride] = useState<string>('')

  const inputJson = inputOverride || buildInputTemplate(canvasJSON)

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
        <Button
          type="primary"
          icon={<CaretRightOutlined />}
          loading={running}
          onClick={() => onRun(inputJson || '{}')}
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
