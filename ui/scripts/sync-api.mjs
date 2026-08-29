import { rmSync } from 'node:fs'
import { join } from 'node:path'
import { spawnSync } from 'node:child_process'

const description = process.argv[2] ?? 'http://127.0.0.1:5000/openapi/v1.json'
const clientDir = join(process.cwd(), 'src', 'api', 'client')

rmSync(clientDir, { recursive: true, force: true })
console.log(`Cleaned generated API client: ${clientDir}`)

const result = spawnSync(
  'kiota',
  [
    'generate',
    '--additional-data',
    'false',
    '-l',
    'typescript',
    '-d',
    description,
    '-c',
    'MoAIClient',
    '-n',
    'ApiSdk',
    '-o',
    './src/api/client',
  ],
  { stdio: 'inherit', shell: true },
)

process.exit(result.status ?? 1)
