import { useEffect, useRef, type CSSProperties } from 'react'
import Vditor from 'vditor'
import 'vditor/dist/index.css'
import { themePresets } from '@/design-system/theme'
import { useAppStore } from '@/store/app'

/**
 * Vditor Markdown 编辑器封装：默认 IR 即时渲染（Typora 式所见即所得），可切纯源码模式。
 *
 * - 运行时按需资源（lute WASM/i18n/高亮/katex）由 vite 插件复制到 public/vendor/vditor，cdn 选项指向本地，不依赖外网
 * - 主题与语言跟随全局设置（主题热切换不重建编辑器、不丢光标）；vditor 无公开模式切换 API，
 *   mode 变化走重建（值经 value 属性延续，撤销栈不保留）
 * - maxLength 启用内置计数器并在超限后阻断输入，与后端内容长度校验保持一致
 */

/** vditor 运行时资源前缀（vite 插件 copyVditorToPublic 复制到 public/vendor/vditor/dist） */
const VDITOR_CDN = '/vendor/vditor'

export type VditorEditorMode = 'ir' | 'sv'

export interface VditorMarkdownEditorProps {
  value: string
  onChange: (markdown: string) => void
  /** ir = 即时渲染所见即所得；sv = 纯源码（单栏源码，无分屏预览） */
  mode?: VditorEditorMode
  placeholder?: string
  /** 内容上限（markdown 字符数），超限阻断输入；不传则不启用计数器 */
  maxLength?: number
  style?: CSSProperties
}

export function VditorMarkdownEditor({ value, onChange, mode = 'ir', placeholder, maxLength, style }: VditorMarkdownEditorProps) {
  const containerRef = useRef<HTMLDivElement>(null)
  const vditorRef = useRef<Vditor | null>(null)
  const readyRef = useRef(false)
  /** 最近一次上报给 onChange / 应用到编辑器的内容，用于区分外部赋值与自身输入 */
  const emittedRef = useRef(value)
  const onChangeRef = useRef(onChange)
  onChangeRef.current = onChange
  /** 始终持有最新 value：vditor 异步初始化完成前到达的外部赋值在 after 回调中补应用 */
  const valueRef = useRef(value)
  valueRef.current = value

  const themeKey = useAppStore((s) => s.themeKey)
  const dark = themePresets[themeKey].mode === 'dark'

  useEffect(() => {
    const container = containerRef.current
    if (!container || vditorRef.current) return
    // 宿主元素在 effect 内创建：清理时直接 detach，StrictMode 挂载→卸载→再挂载时未完成的异步初始化无处渲染
    const host = document.createElement('div')
    host.style.height = '100%'
    container.appendChild(host)
    readyRef.current = false
    // 语言/主题只在初始化时读取一次；后续变化由下方 effect 热更新（语言切换走编辑器重建，随页面刷新即可）
    const state = useAppStore.getState()
    const isDark = themePresets[state.themeKey].mode === 'dark'
    const initial = valueRef.current
    emittedRef.current = initial
    const vditor = new Vditor(host, {
      cdn: VDITOR_CDN,
      mode,
      // 纯源码模式下关掉分屏预览，仅编辑器单栏
      preview: {
        mode: mode === 'sv' ? 'editor' : 'both',
        maxWidth: 100000,
        theme: {
          current: isDark ? 'dark' : 'light',
          path: `${VDITOR_CDN}/dist/css/content-theme`,
        },
        hljs: {
          enable: true,
          lineNumber: false,
          style: isDark ? 'github-dark' : 'github',
        },
        markdown: {
          // 多用户平台，共享/市场内容按默认开启 XSS 清洗
          sanitize: true,
        },
      },
      value: initial,
      height: '100%',
      lang: state.locale === 'zh-CN' ? 'zh_CN' : 'en_US',
      placeholder,
      cache: { enable: false },
      counter: maxLength ? { enable: true, max: maxLength } : undefined,
      theme: isDark ? 'dark' : 'classic',
      toolbar: [
        'undo',
        'redo',
        '|',
        'headings',
        'bold',
        'italic',
        'strike',
        'inline-code',
        '|',
        'list',
        'ordered-list',
        'check',
        'quote',
        'line',
        '|',
        'code',
        'table',
        'link',
        '|',
        'fullscreen',
      ],
      input: (markdown) => {
        emittedRef.current = markdown
        onChangeRef.current(markdown)
      },
      after: () => {
        readyRef.current = true
        // 初始化期间发生的外部赋值/主题切换在此补应用（vditor 内部状态此刻才就绪，此前调用 setTheme/setValue 会崩）
        const latest = valueRef.current
        if (latest !== emittedRef.current) {
          vditor.setValue(latest)
          emittedRef.current = latest
        }
        const current = useAppStore.getState()
        const darkNow = themePresets[current.themeKey].mode === 'dark'
        vditor.setTheme(darkNow ? 'dark' : 'classic', darkNow ? 'dark' : 'light', darkNow ? 'github-dark' : 'github')
      },
    })
    vditorRef.current = vditor
    return () => {
      host.remove()
      readyRef.current = false
      try {
        vditor.destroy()
      } catch {
        // 异步初始化未完成时 destroy 会抛错；宿主已移除，安全
      }
      vditorRef.current = null
    }
    // mode 变化走重建；value/placeholder/maxLength 为初始化捕获配置（value 由下方 effect 增量同步，placeholder/maxLength 变更不重建）
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [mode])

  // 外部赋值（编辑页详情回填）同步进编辑器；自身输入触发的 value 变化跳过
  useEffect(() => {
    if (!readyRef.current || value === emittedRef.current) return
    emittedRef.current = value
    void vditorRef.current?.setValue(value)
  }, [value])

  // 主题热切换：只改外观，不重建编辑器、不丢光标/撤销栈；未就绪时跳过（由 after 回调兜底）
  useEffect(() => {
    if (!readyRef.current) return
    vditorRef.current?.setTheme(dark ? 'dark' : 'classic', dark ? 'dark' : 'light', dark ? 'github-dark' : 'github')
  }, [dark])

  return <div ref={containerRef} style={style} />
}
