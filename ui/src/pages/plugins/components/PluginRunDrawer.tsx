import { useEffect, useState } from 'react'
import { Button, Drawer, Space } from 'antd'
import Editor, { loader } from '@monaco-editor/react'
import { useTranslation } from 'react-i18next'
import { pluginApi } from '@/api/plugin'

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

export interface PluginRunDrawerProps {
  open: boolean
  onClose: () => void
  pluginKey?: string | null
  paramsExample?: string | null
}

export function PluginRunDrawer({ open, onClose, pluginKey, paramsExample }: PluginRunDrawerProps) {
  const { t } = useTranslation()
  const [value, setValue] = useState<string>(() => formatJson(paramsExample))
  const [result, setResult] = useState<string>('')
  const [running, setRunning] = useState(false)

  useEffect(() => {
    if (open) {
      setValue(formatJson(paramsExample))
      setResult('')
    }
  }, [open, paramsExample])

  const handleRun = async () => {
    if (!pluginKey) return
    setRunning(true)
    try {
      const res = await pluginApi.runPlugin({ key: pluginKey, requestJson: value })
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
