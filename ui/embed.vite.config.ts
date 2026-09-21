import { resolve } from 'node:path'
import { defineConfig } from 'vite'

/**
 * 悬浮对话组件构建配置：IIFE 单文件，输出到后端 wwwroot 由 UseStaticFiles 托管.
 * outDir 内还有 logo.svg/models.json/monaco 等静态资源，必须 emptyOutDir:false 只覆盖自身产物.
 * 用法：npm run build:embed
 */
export default defineConfig({
  build: {
    lib: {
      entry: resolve(__dirname, 'src/embed/main.ts'),
      name: 'MoaiWidget',
      formats: ['iife'],
      fileName: () => 'moai-widget.js',
    },
    outDir: resolve(__dirname, '../src/MoAI/wwwroot/embed'),
    emptyOutDir: false,
    sourcemap: false,
    reportCompressedSize: false,
  },
})
