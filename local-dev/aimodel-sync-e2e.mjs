// AM 14 模型同步挑选导入（后端 127.0.0.1:5000，内置 /models 桩服务，零真实依赖）
// 覆盖：sync 只探测不落库 + 候选项 recognized/exists/modelKind + 未识别模型兜底元数据
//       + import 所选模型落库且默认停用（enabled=false）+ 再 sync exists=true + 渠道清理
import crypto from 'node:crypto'
import http from 'node:http'

const BASE = 'http://127.0.0.1:5000'
let PASS = 0, FAIL = 0
const check = (name, cond, detail = '') => {
  if (cond) { PASS++; console.log(`PASS | ${name}`) }
  else { FAIL++; console.log(`FAIL | ${name} ${detail ? '— ' + detail : ''}`) }
}

let RSA_KEY = ''
const rsa = (plain) => {
  const key = crypto.createPublicKey({ key: Buffer.from(RSA_KEY, 'base64'), format: 'der', type: 'spki' })
  return crypto.publicEncrypt({ key, padding: crypto.constants.RSA_PKCS1_PADDING }, Buffer.from(plain, 'utf8')).toString('base64')
}
async function api(method, path, { token, body } = {}) {
  const headers = {}
  if (body !== undefined) headers['Content-Type'] = 'application/json'
  if (token) headers['Authorization'] = `Bearer ${token}`
  const res = await fetch(BASE + path, { method, headers, body: body === undefined ? undefined : JSON.stringify(body) })
  const text = await res.text()
  let json = null
  try { json = JSON.parse(text) } catch { /* 非 JSON */ }
  return { status: res.status, json, text }
}

function startStubServer() {
  const stub = http.createServer((req, res) => {
    res.writeHead(200, { 'Content-Type': 'application/json' })
    res.end(JSON.stringify({
      object: 'list',
      data: [{ id: 'gpt-4o' }, { id: 'text-embedding-3-small' }, { id: 'fake-ultra-model' }],
    }))
  })
  return new Promise((resolve) => stub.listen(0, '127.0.0.1', () => resolve(stub)))
}

async function main() {
  const stub = await startStubServer()
  const stubPort = stub.address().port
  try {
    const si = await api('GET', '/api/common/serverinfo')
    RSA_KEY = si.json.rsaPublic
    const l = await api('POST', '/api/auth/login', { body: { userName: 'admin', password: rsa('abcd123456') } })
    const token = l.json.token ?? l.json.accessToken
    check('登录', Boolean(token))

    const chName = `syncmodal-verify-${Date.now().toString().slice(-8)}`
    const ch = await api('POST', '/api/ai/channel', {
      token,
      body: { providerKey: 'openai', name: chName, protocolFamily: 'openaiChatCompletions', baseUrl: `http://127.0.0.1:${stubPort}/v1`, apiKey: 'stub-key', enabled: true },
    })
    check('创建渠道', ch.status === 200 || ch.status === 204, `status=${ch.status} body=${ch.text.slice(0, 200)}`)

    const channels = await api('GET', '/api/ai/channel', { token })
    const created = (channels.json.items ?? []).find((x) => x.name === chName)
    check('渠道已存在', Boolean(created?.id))

    // sync：只探测不落库
    const sync = await api('POST', '/api/ai/model/sync', { token, body: { channelId: created.id } })
    check('sync 200', sync.status === 200, `status=${sync.status} body=${sync.text.slice(0, 200)}`)
    const items = sync.json?.items ?? []
    check('sync 返回 3 个候选', sync.json?.total === 3 && items.length === 3, JSON.stringify(sync.json)?.slice(0, 300))
    const gpt = items.find((x) => x.meta?.modelId === 'gpt-4o')
    const emb = items.find((x) => x.meta?.modelId === 'text-embedding-3-small')
    const fake = items.find((x) => x.meta?.modelId === 'fake-ultra-model')
    check('gpt-4o 已识别+conversation', gpt?.recognized === true && gpt?.modelKind === 'conversation', JSON.stringify(gpt))
    check('embedding 已识别+embedding 类型', emb?.recognized === true && emb?.modelKind === 'embedding', JSON.stringify(emb))
    check('未知模型未识别+兜底元数据', fake?.recognized === false && fake?.meta?.name === 'fake-ultra-model' && fake?.exists === false, JSON.stringify(fake))

    // 库里应该还是 0 个模型
    const models0 = await api('GET', `/api/ai/model?channelId=${created.id}`, { token })
    check('sync 未落库（0 模型）', (models0.json?.items ?? []).length === 0, JSON.stringify(models0.json)?.slice(0, 200))

    // import：只导 2 个（gpt-4o + fake）
    const imp = await api('POST', '/api/ai/model/import', {
      token,
      body: { channelId: created.id, items: [gpt.meta, fake.meta] },
    })
    check('import 200', imp.status === 200 || imp.status === 204, `status=${imp.status} body=${imp.text.slice(0, 200)}`)

    const models1 = await api('GET', `/api/ai/model?channelId=${created.id}`, { token })
    const list = models1.json?.items ?? []
    check('导入 2 个模型', list.length === 2, `count=${list.length}`)
    const gptRow = list.find((x) => x.modelId === 'gpt-4o')
    const fakeRow = list.find((x) => x.modelId === 'fake-ultra-model')
    check('导入模型默认停用', gptRow?.enabled === false && fakeRow?.enabled === false, JSON.stringify({ gptRow, fakeRow }))

    // 再 sync：已导入的标记 exists=true
    const sync2 = await api('POST', '/api/ai/model/sync', { token, body: { channelId: created.id } })
    const gpt2 = (sync2.json?.items ?? []).find((x) => x.meta?.modelId === 'gpt-4o')
    check('再 sync exists=true', gpt2?.exists === true, JSON.stringify(gpt2))

    // 清理
    await api('DELETE', `/api/ai/channel/${created.id}`, { token })
    const channels2 = await api('GET', '/api/ai/channel', { token })
    check('清理渠道', !(channels2.json.items ?? []).some((x) => x.name === chName))
  } finally {
    stub.close()
  }
  console.log(`\n${PASS} passed, ${FAIL} failed`)
  process.exit(FAIL > 0 ? 1 : 0)
}

main().catch((e) => { console.error(e); process.exit(1) })
