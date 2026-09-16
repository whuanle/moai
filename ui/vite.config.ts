import { defineConfig } from 'vitest/config'
import react from '@vitejs/plugin-react'
import { cpSync, existsSync, mkdirSync, rmSync } from 'node:fs'
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

export default defineConfig({
  plugins: [react(), copyMonacoToPublic()],
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
    proxy: {
      '/openapi': {
        target: 'http://127.0.0.1:5000',
        changeOrigin: true,
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
