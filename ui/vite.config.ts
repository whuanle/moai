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
    include: ['react', 'react-dom'],
  },
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: ['./src/test/setup.ts'],
    css: false,
  },
})
