// 沙箱资源上限 E2E（场景 @SB-Sn / @SET-S20+）
// 覆盖：管理员在系统设置配置每个应用沙箱最大存活时间 / CPU / 内存上限；团队保存应用配置（agent-config）时不得超出。
// 用法：node local-dev/sandbox-limits-e2e.mjs [baseUrl]（默认 http://127.0.0.1:5000）
// 前置：后端运行中；种子账号 admin / abcd123456（root）。脚本结束时把三项上限还原为默认 86400 / 4 / 8Gi。
import crypto from 'node:crypto'

const BASE = process.env.APP_BASE ?? process.argv[2] ?? 'http://127.0.0.1:5000'
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

const TS = Date.now().toString().slice(-8)
let seq = 0
const uname = (p) => `${p}${TS}${String(seq++).padStart(2, '0')}`
const phone = () => `15${Date.now().toString().slice(-8)}${String(seq++).padStart(2, '0')}`.slice(0, 11)

async function mkuser(p) {
  const name = uname(p)
  const r = await api('POST', '/api/auth/register', { body: { userName: name, email: `${name}@test.local`, nickName: name, phone: phone(), password: rsa('Test1234') } })
  if (r.status !== 200) throw new Error(`注册 ${name} 失败: ${r.status} ${r.text.slice(0, 120)}`)
  const l = await api('POST', '/api/auth/login', { body: { userName: name, password: rsa('Test1234') } })
  return { name, userId: Number(l.json.userId), token: l.json.accessToken }
}

async function rootLogin() {
  const l = await api('POST', '/api/auth/login', { body: { userName: 'admin', password: rsa('abcd123456') } })
  return l.json?.accessToken ?? null
}

const saveSetting = (token, key, value) => api('PUT', '/api/settings', { token, body: { key, value } })
const putSandbox = (token, appId, sandbox) =>
  api('PUT', `/api/app/${appId}/agent-config`, { token, body: { prompt: '', wikiIds: [], plugins: [], executionSettings: { sandbox } } })

async function main() {
  const si = await api('GET', '/api/common/serverinfo')
  RSA_KEY = si.json.rsaPublic

  const root = await rootLogin()
  check('SB-00 root 登录', Boolean(root))

  // ============ 设置项读写 ============
  // SB-01 沙箱上限查询端点：登录用户可访问（应用配置页的取值范围来源）
  const unauth = await api('GET', '/api/app/sandbox-limits')
  check('SB-01a 未登录查沙箱上限 401', unauth.status === 401)
  {
    const r = await api('GET', '/api/app/sandbox-limits', { token: root })
    check('SB-01b 登录用户查沙箱上限 200 且含三项',
      r.status === 200 && Number.isFinite(r.json?.maxTtlSeconds) && typeof r.json?.maxCpu === 'string' && typeof r.json?.maxMemory === 'string',
      `${r.status} ${r.text.slice(0, 140)}`)
  }

  // SB-02 设置保存校验：格式非法直接 400
  check('SB-02a TTL 低于下限 60 保存 400', (await saveSetting(root, 'SANDBOX_MAX_TTL_SECONDS', '30')).status === 400)
  check('SB-02b TTL 高于上限 604800 保存 400', (await saveSetting(root, 'SANDBOX_MAX_TTL_SECONDS', '700000')).status === 400)
  check('SB-02c CPU 格式非法保存 400', (await saveSetting(root, 'SANDBOX_MAX_CPU', 'fast')).status === 400)
  check('SB-02d 内存格式非法保存 400', (await saveSetting(root, 'SANDBOX_MAX_MEMORY', '8gi')).status === 400)

  // SB-03 root 收紧上限并回读生效
  check('SB-03a root 保存 TTL=600 200', (await saveSetting(root, 'SANDBOX_MAX_TTL_SECONDS', '600')).status === 200)
  check('SB-03b root 保存 CPU=2000m 200', (await saveSetting(root, 'SANDBOX_MAX_CPU', '2000m')).status === 200)
  check('SB-03c root 保存 MEMORY=1Gi 200', (await saveSetting(root, 'SANDBOX_MAX_MEMORY', '1Gi')).status === 200)
  {
    const r = await api('GET', '/api/app/sandbox-limits', { token: root })
    check('SB-03d 沙箱上限查询反映新值', r.status === 200 && r.json?.maxTtlSeconds === 600 && r.json?.maxCpu === '2000m' && r.json?.maxMemory === '1Gi',
      `${r.status} ${r.text.slice(0, 140)}`)
  }

  // ============ 应用配置强校验 ============
  const owner = await mkuser('so')
  const member = await mkuser('sm')
  const TID = Number((await api('POST', '/api/team', { token: owner.token, body: { name: 'sb-team-' + TS } })).json.value)
  await api('POST', `/api/team/${TID}/users`, { token: owner.token, body: { userId: member.userId, role: 0 } })
  const c = await api('POST', '/api/app', { token: owner.token, body: { teamId: TID, name: '沙箱应用', appType: 'agent' } })
  const APP_ID = String(c.json?.value ?? '')
  check('SB-04 创建 Agent 应用', c.status === 200 && /^[0-9a-f-]{36}$/i.test(APP_ID), `${c.status} ${c.text.slice(0, 120)}`)

  // SB-05 启用沙箱且超限 → 400
  {
    const r = await putSandbox(owner.token, APP_ID, { enabled: true, timeoutSeconds: 3600 })
    check('SB-05a 沙箱存活时间超限 400', r.status === 400 && (r.text.includes('600') || r.json?.detail?.includes('600')), `${r.status} ${r.text.slice(0, 140)}`)
  }
  {
    const r = await putSandbox(owner.token, APP_ID, { enabled: true, resource: { cpu: '2500m' } })
    check('SB-05b 沙箱 CPU 超限 400', r.status === 400 && (r.text.includes('2000m') || r.json?.detail?.includes('2000m')), `${r.status} ${r.text.slice(0, 140)}`)
  }
  {
    const r = await putSandbox(owner.token, APP_ID, { enabled: true, resource: { memory: '2Gi' } })
    check('SB-05c 沙箱内存超限 400', r.status === 400 && (r.text.includes('1Gi') || r.json?.detail?.includes('1Gi')), `${r.status} ${r.text.slice(0, 140)}`)
  }
  {
    const r = await putSandbox(owner.token, APP_ID, { enabled: true, resource: { cpu: 'fast' } })
    check('SB-05d 沙箱 CPU 格式非法 400', r.status === 400, `${r.status} ${r.text.slice(0, 140)}`)
  }

  // SB-06 未启用沙箱时超限值不拦截（收紧上限不阻断其他字段的保存）
  {
    const r = await putSandbox(owner.token, APP_ID, { enabled: false, timeoutSeconds: 999999, resource: { cpu: '99', memory: '999Gi' } })
    check('SB-06 未启用沙箱时超限值可暂存 200', r.status === 200, `${r.status} ${r.text.slice(0, 140)}`)
  }

  // SB-07 合法值保存并回读一致
  {
    const r = await putSandbox(owner.token, APP_ID, { enabled: true, timeoutSeconds: 600, renewOnAccess: true, resource: { cpu: '2000m', memory: '1Gi' } })
    check('SB-07a 等于上限的合法配置保存 200', r.status === 200, `${r.status} ${r.text.slice(0, 140)}`)
    const q = await api('GET', `/api/app/${APP_ID}/agent-config`, { token: owner.token })
    const sb = q.json?.executionSettings?.sandbox
    check('SB-07b 回读沙箱配置一致',
      q.status === 200 && sb?.enabled === true && sb?.timeoutSeconds === 600 && sb?.resource?.cpu === '2000m' && sb?.resource?.memory === '1Gi',
      `${q.status} ${q.text.slice(0, 160)}`)
  }

  // SB-08 既有门禁回归：Member 保存配置 403
  check('SB-08 Member 保存配置 403', (await putSandbox(member.token, APP_ID, { enabled: true, timeoutSeconds: 300 })).status === 403)

  // ============ 还原默认上限 ============
  check('SB-09 还原默认上限',
    (await saveSetting(root, 'SANDBOX_MAX_TTL_SECONDS', '86400')).status === 200
    && (await saveSetting(root, 'SANDBOX_MAX_CPU', '4')).status === 200
    && (await saveSetting(root, 'SANDBOX_MAX_MEMORY', '8Gi')).status === 200)

  console.log(`\n总计: PASS ${PASS} / FAIL ${FAIL}`)
  if (FAIL > 0) process.exit(1)
}

main().catch((e) => { console.error('E2E 执行异常:', e); process.exit(1) })
