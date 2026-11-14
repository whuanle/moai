import { rmSync } from 'node:fs'
import { join } from 'node:path'
import { spawnSync } from 'node:child_process'

const description = process.argv[2] ?? 'http://127.0.0.1:5000/openapi/v1.json'
const clientDir = join(process.cwd(), 'src', 'api', 'client')
const aiPluginClientDir = join(process.cwd(), 'src', 'api', 'aiplugin-client')

function generate(name, namespace, outputDir, includePaths) {
  const args = [
    'generate',
    '--additional-data',
    'false',
    '-l',
    'typescript',
    '-d',
    description,
    '-c',
    name,
    '-n',
    namespace,
    '-o',
    outputDir,
  ]
  for (const includePath of includePaths) {
    args.push('--include-path', includePath)
  }

  const result = spawnSync('kiota', args, { stdio: 'inherit', shell: true })
  if ((result.status ?? 1) !== 0) {
    process.exit(result.status ?? 1)
  }
}

rmSync(clientDir, { recursive: true, force: true })
console.log(`Cleaned generated API client: ${clientDir}`)
generate('MoAIClient', 'ApiSdk', './src/api/client', [])

rmSync(aiPluginClientDir, { recursive: true, force: true })
console.log(`Cleaned generated AI plugin client: ${aiPluginClientDir}`)
// 插件引擎独立客户端：仅收录 /api/ai/plugin 子树（客户端名/命名空间沿用既有生成物 kiota-lock.json；
// include 需同时带目录本身与 ** 通配，否则丢掉挂在该节点上的端点如 GET /api/ai/plugin）
generate('AiPluginClient', 'ApiTypes', './src/api/aiplugin-client', ['/api/ai/plugin', '/api/ai/plugin/**'])
