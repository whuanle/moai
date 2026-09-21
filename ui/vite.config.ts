import { defineConfig } from 'vitest/config'
import react from '@vitejs/plugin-react'
import { cpSync, createReadStream, existsSync, mkdirSync, rmSync, statSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath, URL } from 'node:url'

const __dirname = dirname(fileURLToPath(import.meta.url))

/** 把 monaco-editor/min/vs 复制到 public/monaco/vs，供 loader 本地加载（避免 CDN 卡顿）.*/
function copyMonacoToPublic(): { name: string; configureServer: () => void; writeBundle: () => void } {
  const src = join(__dirname, 'node_modules', 'monaco-editor', 'min', 'vs')
  const dest = join(__dirname, 'public', 'monaco', 'vs')
  const run = () => {
    if (!existsSync(src)) return
    rmSync(dest, { recursive: true, force: true })
    mkdirSync(dest, { recursive: true })
    cpSync(src, dest, { recursive: true })
  }
  return { name: 'copy-monaco-to-public', configureServer: run, writeBundle: run }
}

/**
 * 开发期把后端 wwwroot 的悬浮对话组件挂到本站 /embed/moai-widget.js（嵌入代码的 script src 指向站点自身源）.
 * 唯一产物源仍是 `npm run build:embed` 输出的 src/MoAI/wwwroot/embed/moai-widget.js，此处只读托管避免双份产物.
 */
function serveEmbedWidget() {
  return {
    name: 'serve-embed-widget',
    configureServer(server: { middlewares: { use: (fn: (req: import('node:http').IncomingMessage, res: import('node:http').ServerResponse, next: () => void) => void) => void } }) {
      server.middlewares.use((req, res, next) => {
        const path = (req.url ?? '').split('?')[0]
        if (path !== '/embed/moai-widget.js') {
          next()
          return
        }
        const file = join(__dirname, '..', 'src', 'MoAI', 'wwwroot', 'embed', 'moai-widget.js')
        if (!existsSync(file) || !statSync(file).isFile()) {
          res.statusCode = 404
          res.end('moai-widget.js not found; run `npm run build:embed` first')
          return
        }
        res.setHeader('Content-Type', 'text/javascript; charset=utf-8')
        createReadStream(file).pipe(res)
      })
    },
  }
}

export default defineConfig({
  plugins: [react(), copyMonacoToPublic(), serveEmbedWidget()],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
    // FlowGram 的 IoC 容器是模块级单例，必须去重避免双实例（Ambiguous match: FlowRendererRegistry）
    dedupe: [
      '@flowgram.ai/core',
      '@flowgram.ai/editor',
      '@flowgram.ai/form',
      '@flowgram.ai/form-core',
      '@flowgram.ai/node',
      '@flowgram.ai/free-layout-editor',
      '@flowgram.ai/free-layout-core',
      '@flowgram.ai/free-snap-plugin',
      '@flowgram.ai/minimap-plugin',
      '@flowgram.ai/variable-core',
      '@flowgram.ai/document',
      '@flowgram.ai/renderer',
      '@flowgram.ai/playground-react',
      '@flowgram.ai/utils',
    ],
  },
  server: {
    port: 4000,
    host: true,
    // 必须关闭：Vite 内置 cors 只放行 localhost 系 origin，会抢答 /api 预检且对
    // Origin: null（file:// 宿主页，悬浮组件的典型嵌入场景）不带 ACAO，把请求挡在代理之前。
    // 关闭后预检穿透到后端，由后端 CORS 策略统一回答（AllowAnyOrigin → *）。
    cors: false,
    proxy: {
      '/openapi': {
        target: 'http://127.0.0.1:5000',
        changeOrigin: true,
      },
      // 悬浮对话组件以 script 源（本站）调用后端 API，开发期代理到后端（生产为同源部署，无需代理）
      '/api': {
        target: 'http://127.0.0.1:5000',
        changeOrigin: true,
        // 后端不可达（重启中/未启动）时代理默认 500 且无 CORS 头，浏览器会误报成跨域错误；
        // 显式回 502 + ACAO，让宿主页能看到真实错误
        configure: (proxy) => {
          proxy.on('error', (err, _req, res) => {
            if (!('writeHead' in res) || res.headersSent) return
            res.writeHead(502, {
              'Content-Type': 'application/json; charset=utf-8',
              'Access-Control-Allow-Origin': '*',
            })
            res.end(JSON.stringify({ error: { message: `MoAI 后端不可达：${err.message}`, code: 'backend_unreachable' } }))
          })
        },
      },
    },
  },
  build: {
    outDir: 'dist',
    sourcemap: true,
  },
  optimizeDeps: {
    include: [
      'react',
      'react-dom',
      '@flowgram.ai/core',
      '@flowgram.ai/editor',
      '@flowgram.ai/form',
      '@flowgram.ai/form-core',
      '@flowgram.ai/node',
      '@flowgram.ai/free-layout-editor',
      '@flowgram.ai/free-snap-plugin',
      '@flowgram.ai/minimap-plugin',
      '@flowgram.ai/variable-core',
    ],
  },
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: ['./src/test/setup.ts'],
    css: false,
  },
})

