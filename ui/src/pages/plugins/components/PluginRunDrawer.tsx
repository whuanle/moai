import { useEffect, useMemo, useState } from 'react'
import { Button, Drawer, Select, Space } from 'antd'
import Editor, { loader } from '@monaco-editor/react'
import { useTranslation } from 'react-i18next'
import { pluginApi, type PluginToolItem } from '@/api/plugin'

// 使用本地打包的 Monaco 资源，避免从 CDN 加载导致抽屉长时间 loading。
// Vite 会把 public/monaco/vs 静态输出到 /monaco/vs。
loader.config({ paths: { vs: '/monaco/vs' } })

/** 解析并格式化 JSON 字符串为易读的缩进文本；非法 JSON 返回原字符串（如前缀含 Error: 的文本）。*/
function formatJson(text: string | null | undefined): string {
  const raw = text ?? ''
  if (!raw) return raw
  try {
    return JSON.stringify(JSON.parse(raw), null, 2)
  } catch {
    return raw
  }
}

/** 运行结果最小结构（兼容管理员/团队两套生成客户端）. */
export interface PluginRunResultLike {
  success?: boolean | null
  dataJson?: string | null
  errorEscaped?: string | null
}

export interface PluginRunDrawerProps {
  open: boolean
  onClose: () => void
  pluginKey?: string | null
  paramsExample?: string | null
  /** 工具清单（多工具插件传入后可在抽屉内选择工具，参数示例随工具切换）；单工具插件不传. */
  tools?: PluginToolItem[] | null
  /** 运行器，默认走管理员接口；团队场景注入团队接口. */
  runPlugin?: (payload: { key: string; requestJson: string; toolKey?: string | null }) => Promise<PluginRunResultLike | null>
}

export function PluginRunDrawer({ open, onClose, pluginKey, paramsExample, tools, runPlugin }: PluginRunDrawerProps) {
  const { t } = useTranslation()
  const [value, setValue] = useState<string>(() => formatJson(paramsExample))
  const [result, setResult] = useState<string>('')
  const [running, setRunning] = useState(false)
  const [toolKey, setToolKey] = useState<string>('')
  const runner = runPlugin ?? pluginApi.runPlugin

  const toolOptions = useMemo(() => (tools && tools.length > 1 ? tools : null), [tools])
  const selectedTool = useMemo(
    () => toolOptions?.find((x) => x.key === toolKey) ?? toolOptions?.[0],
    [toolOptions, toolKey],
  )

  useEffect(() => {
    if (open) {
      setToolKey(toolOptions?.[0]?.key ?? '')
      setValue(formatJson(selectedTool?.paramsExample ?? paramsExample))
      setResult('')
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, paramsExample, tools])

  const handleToolChange = (key: string) => {
    setToolKey(key)
    const tool = toolOptions?.find((x) => x.key === key)
    if (tool) {
      setValue(formatJson(tool.paramsExample))
    }
  }

  const handleRun = async () => {
    if (!pluginKey) return
    setRunning(true)
    try {
      const res = await runner({
        key: pluginKey,
        requestJson: value,
        // 单工具插件（run）不传 toolKey，保持旧行为
        toolKey: selectedTool && selectedTool.key !== 'run' ? selectedTool.key : undefined,
      })
      if (res?.success) {
        setResult(formatJson(res.dataJson))
      } else {
        setResult(`Error: ${res?.errorEscaped ?? 'unknown'}`)
      }
    } catch (e) {
      setResult(`Error: ${e instanceof Error ? e.message : String(e)}`)
    } finally {
      setRunning(false)
    }
  }

  return (
    <Drawer
      open={open}
      onClose={onClose}
      width="50%"
      maskClosable={false}
      title={t('plugins.runDrawerTitle')}
      closable={false}
      styles={{ body: { padding: 16, display: 'flex', flexDirection: 'column', height: '100%' } }}
      footer={
        <Space style={{ float: 'right' }}>
          <Button onClick={onClose}>{t('plugins.close')}</Button>
          <Button type="primary" loading={running} onClick={handleRun}>
            {t('plugins.run')}
          </Button>
        </Space>
      }
      footerStyle={{ textAlign: 'right' }}
    >
      <div style={{ flex: 1, display: 'flex', flexDirection: 'column', minHeight: 0 }}>
        {toolOptions && (
          <div style={{ marginBottom: 12 }}>
            <div style={{ marginBottom: 8, fontWeight: 600 }}>{t('plugins.toolLabel')}</div>
            <Select
              style={{ width: 320 }}
              value={selectedTool?.key}
              onChange={handleToolChange}
              options={toolOptions.map((x) => ({ value: x.key, label: x.title ? `${x.title} (${x.key})` : x.key }))}
            />
          </div>
        )}
        <div style={{ flex: 1, display: 'flex', flexDirection: 'column', minHeight: 0 }}>
          <div style={{ marginBottom: 8, fontWeight: 600 }}>{t('plugins.paramsLabel')}</div>
          <Editor
            height="100%"
            language="json"
            value={value}
            onChange={(v) => setValue(v ?? '')}
            options={{ minimap: { enabled: false }, fontSize: 14 }}
          />
        </div>
        <div style={{ flex: 1, display: 'flex', flexDirection: 'column', minHeight: 0, marginTop: 16 }}>
          <div style={{ marginBottom: 8, fontWeight: 600 }}>{t('plugins.resultLabel')}</div>
          <Editor
            height="100%"
            language="json"
            value={result}
            options={{ minimap: { enabled: false }, fontSize: 14, readOnly: true }}
          />
        </div>
      </div>
    </Drawer>
  )
}
