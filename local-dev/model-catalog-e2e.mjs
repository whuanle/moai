// 模型目录在线同步 E2E（catalog/sync 真实下载 models.opencode.ai，需外网可达；其余场景零外部依赖）
// 用法: node local-dev/model-catalog-e2e.mjs [baseUrl]   （需后端运行中，默认 http://127.0.0.1:5000）
// 覆盖：
//   - catalog/sync 门禁（未登录 401 / 普通用户 403）
//   - configs/models.json 优先于程序内置目录，写入后按文件时间戳自动重载立即生效
//   - configs 文件损坏时回退内置目录，渠道同步不崩
//   - 在线同步：真实下载 → 校验 → 覆盖 configs/models.json → 返回渠道/模型数量 → 立即生效
const BASE = process.argv[2] ?? 'http://127.0.0.1:5000'
const crypto = await import('node:crypto')
const http = await import('node:http')
const fs = await import('node:fs')
const path = await import('node:path')

let pass = 0
let fail = 0
function check(name, cond, detail = '') {
  if (cond) {
    pass++
    console.log(`PASS ${name}`)
  } else {
    fail++
    console.log(`FAIL ${name} ${detail}`)
  }
}

const ts = Date.now().toString(36)
let seq = 0

// ---------- HTTP 工具 ----------
const { rsaPublic } = await fetch(`${BASE}/api/common/serverinfo`).then((r) => r.json())

function enc(text) {
  const key = crypto.createPublicKey({ key: Buffer.from(rsaPublic, 'base64'), format: 'der', type: 'spki' })
  return crypto.publicEncrypt({ key, padding: crypto.constants.RSA_PKCS1_PADDING }, Buffer.from(text)).toString('base64')
}

async function api(token, path, method = 'GET', payload) {
  const r = await fetch(`${BASE}${path}`, {
    method,
    headers: {
      'Content-Type': 'application/json',
      Authorization: token ? `Bearer ${token}` : '',
    },
    body: payload === undefined ? undefined : JSON.stringify(payload),
  })
  let body = null
  try { body = await r.json() } catch { /* no body */ }
  return { status: r.status, body }
}

const phone = () => `15${Date.now().toString().slice(-8)}${String(seq++).padStart(2, '0')}`.slice(0, 11)

async function login(userName, password) {
  const l = await api(null, '/api/auth/login', 'POST', { userName, password: enc(password) })
  return l.body?.accessToken
}

// ---------- 目录文件定位（dev 运行布局：src/MoAI/bin/Debug/net*/configs） ----------
const repoRoot = path.resolve(path.dirname(new URL(import.meta.url).pathname.replace(/^\/([A-Za-z]:)/, '$1')), '..')
const binRoots = [path.join(repoRoot, 'src', 'MoAI', 'bin', 'Debug')]
const configsDir = (() => {
  for (const binRoot of binRoots) {
    if (!fs.existsSync(binRoot)) continue
    for (const net of fs.readdirSync(binRoot)) {
      const dir = path.join(binRoot, net, 'configs')
      if (fs.existsSync(dir)) return dir
    }
  }
  return null
})()
const configsCatalog = configsDir ? path.join(configsDir, 'models.json') : null
check('定位后端 configs 目录', Boolean(configsDir), String(configsDir))

// ---------- 桩供应商服务（OpenAI /models 形态） ----------
const STUB_PORT = 19400 + Math.floor(Math.random() * 500)
const stubServer = http.createServer((req, res) => {
  res.setHeader('Content-Type', 'application/json')
  if ((req.url ?? '').endsWith('/models')) {
    res.end(JSON.stringify({ object: 'list', data: [{ id: 'cat-stub-model-a', object: 'model' }, { id: 'cat-stub-model-b', object: 'model' }] }))
    return
  }
  res.statusCode = 404
  res.end(JSON.stringify({ error: 'not found' }))
})
await new Promise((resolve) => stubServer.listen(STUB_PORT, '127.0.0.1', resolve))
const STUB_BASE = `http://127.0.0.1:${STUB_PORT}`

// ---------- 准备 ----------
const adminToken = await login('admin', 'abcd123456')
check('admin 登录', Boolean(adminToken))

const plain = await (async () => {
  const name = `catu${ts}`
  await api(null, '/api/auth/register', 'POST', { userName: name, email: `${name}@test.local`, nickName: name, phone: phone(), password: enc('Test1234') })
  return { name, token: await login(name, 'Test1234') }
})()
check('注册普通用户', Boolean(plain.token))

const providerKey = `cat-stub-${ts}`
const createCh = await api(adminToken, '/api/ai/channel', 'POST', {
  providerKey, name: `目录同步桩渠道-${ts}`, protocolFamily: 'openAIChatCompletions',
  baseUrl: STUB_BASE, apiKey: 'sk-stub-key', enabled: true,
})
check('创建系统渠道', createCh.status === 200, JSON.stringify(createCh.body))
const channels = await api(adminToken, '/api/ai/channel')
const channel = (channels.body?.items ?? []).find((x) => x.providerKey === providerKey)
check('渠道列表回显', Boolean(channel?.id), JSON.stringify(channels.body?.items?.length))

async function syncChannel() {
  return api(adminToken, '/api/ai/model/sync', 'POST', { channelId: channel.id })
}

// ---------- 门禁 ----------
const anon = await api(null, '/api/ai/model/catalog/sync', 'POST', {})
check('MC-S1 未登录 401', anon.status === 401, `${anon.status}`)
const plainRes = await api(plain.token, '/api/ai/model/catalog/sync', 'POST', {})
check('MC-S1 普通用户 403', plainRes.status === 403, `${plainRes.status}`)

// ---------- 基线：无 configs 目录文件时走内置目录，桩模型不可识别 ----------
if (configsCatalog && fs.existsSync(configsCatalog)) fs.unlinkSync(configsCatalog)
const baseline = await syncChannel()
const baselineA = (baseline.body?.items ?? []).find((x) => x.meta?.modelId === 'cat-stub-model-a')
check('MC-S3 基线：桩模型在内置目录中不可识别', baseline.status === 200 && baselineA?.recognized === false, JSON.stringify(baselineA))

// ---------- configs/models.json 优先 + 时间戳自动重载 ----------
fs.mkdirSync(configsDir, { recursive: true })
fs.writeFileSync(configsCatalog, JSON.stringify({
  [providerKey]: {
    id: providerKey,
    name: 'Catalog Stub Provider',
    models: {
      'cat-stub-model-a': {
        id: 'cat-stub-model-a',
        name: 'Catalog Stub Model A',
        attachment: true,
        reasoning: true,
        tool_call: true,
        limit: { context: 128000, output: 4096 },
        cost: { input: 1.5, output: 2.5 },
      },
    },
  },
}, null, 2), 'utf8')
const crafted = await syncChannel()
const craftedA = (crafted.body?.items ?? []).find((x) => x.meta?.modelId === 'cat-stub-model-a')
check('MC-S3 configs 目录优先，写入后立即生效', crafted.status === 200 && craftedA?.recognized === true, JSON.stringify(craftedA))
check('MC-S3 命中自定义目录元数据', craftedA?.meta?.name === 'Catalog Stub Model A' && craftedA?.meta?.contextWindow === 128000, JSON.stringify(craftedA?.meta))

// ---------- configs 文件损坏回退内置目录 ----------
fs.writeFileSync(configsCatalog, '{ this is not json', 'utf8')
const corrupt = await syncChannel()
const corruptA = (corrupt.body?.items ?? []).find((x) => x.meta?.modelId === 'cat-stub-model-a')
check('MC-S4 configs 损坏回退内置目录不崩', corrupt.status === 200 && corruptA?.recognized === false, `${corrupt.status} ${JSON.stringify(corruptA)}`)

// ---------- 在线同步：真实下载并覆盖 configs，立即生效 ----------
const syncRes = await api(adminToken, '/api/ai/model/catalog/sync', 'POST', {})
check('MC-S2 管理员在线同步 200', syncRes.status === 200, `${syncRes.status} ${JSON.stringify(syncRes.body)}`)
check('MC-S2 返回渠道与模型数量', (syncRes.body?.providerCount ?? 0) >= 20 && (syncRes.body?.modelCount ?? 0) >= 1000, JSON.stringify(syncRes.body))
let savedOk = false
let savedHasOpenai = false
try {
  const saved = JSON.parse(fs.readFileSync(configsCatalog, 'utf8'))
  savedOk = Object.keys(saved).length === syncRes.body?.providerCount
  savedHasOpenai = Boolean(saved.openai?.models)
} catch { /* keep false */ }
check('MC-S2 configs/models.json 已被真实目录覆盖', savedOk && savedHasOpenai, `savedOk=${savedOk} savedHasOpenai=${savedHasOpenai}`)
const after = await syncChannel()
const afterA = (after.body?.items ?? []).find((x) => x.meta?.modelId === 'cat-stub-model-a')
check('MC-S2 同步后新目录立即生效（桩模型不再可识别）', after.status === 200 && afterA?.recognized === false, JSON.stringify(afterA))

// ---------- 清理 ----------
const del = await api(adminToken, `/api/ai/channel/${channel.id}`, 'DELETE')
check('清理渠道', del.status === 200, `${del.status}`)
stubServer.close()

console.log(`\n结果: ${pass} pass, ${fail} fail`)
process.exit(fail > 0 ? 1 : 0)
