// 子系统协作面 .NET 客户端再生成脚本（对齐 ui/scripts/sync-api.mjs 的用法）：
//   node clients/sync-api.mjs [openapi-url]
// 默认取本机后端 /openapi/subsys.json（子系统协作面，仅含 /api/subsys/** 路径）。
// 只清空并重写 MoAI.Subsystem.Client/Generated/，项目根的手写包装
// （DI / 配置 / token 管理）不参与生成、永不被覆盖。
import { rmSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { spawnSync } from 'node:child_process'

const here = dirname(fileURLToPath(import.meta.url))
const description = process.argv[2] ?? 'http://127.0.0.1:5000/openapi/subsys.json'
const outputDir = join(here, 'MoAI.Subsystem.Client', 'Generated')

rmSync(outputDir, { recursive: true, force: true })
console.log(`Cleaned generated subsystem client: ${outputDir}`)

const result = spawnSync(
  'kiota',
  [
    'generate',
    '--additional-data',
    'false',
    '-l',
    'CSharp',
    '-d',
    description,
    '-c',
    'SubsysClient',
    '-n',
    'MoAI.Subsystem.Client.ApiSdk',
    '-o',
    outputDir,
  ],
  { stdio: 'inherit', shell: true },
)

if (result.status !== 0) {
  console.error('kiota generate failed: check backend is running (src/MoAI) and kiota CLI is on PATH.')
}
process.exit(result.status ?? 1)
