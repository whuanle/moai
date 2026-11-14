// 系统级接入 Demo 数据种子 + 功能验证（可重复执行：先清理全部系统级接入，再创建演示数据）
// Demo 内容：
//   子系统   智能报表（report-demo，双回调地址+全员可用+组织授权 down）
//   子系统   OA 协同（oa-demo，指定团队=演示研发团队）
//   应用接入 ERP 组织同步 / HR 用户推送（自动生成结构化 appid）
// 验证段：结构化 appid 直连自省 / 统一 token 换取 / 心跳在线（实例数）/ 密钥信封回显 /
//         多回调 redirect_uri 指定入口 / 限流 429。
// 用法：node local-dev/subsys-demo-seed.mjs [baseUrl]（默认 http://127.0.0.1:5000）
import crypto from 'node:crypto'

const BASE = process.argv[2] ?? process.env.SUBSYS_BASE ?? 'http://127.0.0.1:5000'
let PASS = 0
let FAIL = 0
const check = (name, cond, detail = '') => {
  if (cond) {
    PASS++
    console.log(`PASS | ${name}`)
  } else {
    FAIL++
    console.log(`FAIL | ${name}${detail ? ' — ' + detail : ''}`)
  }
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

// 密钥信封回显：随机 AES-256 key 经 RSA 加密上送，响应 GCM 密文本地解出原文
async function revealSecret(rootToken, id) {
  const pub = crypto.createPublicKey({ key: Buffer.from(RSA_KEY, 'base64'), format: 'der', type: 'spki' })
  const envelopeKey = crypto.randomBytes(32)
  const encryptedKey = crypto.publicEncrypt({ key: pub, padding: crypto.constants.RSA_PKCS1_PADDING }, Buffer.from(envelopeKey.toString('base64'), 'utf8')).toString('base64')
  const r = await api('POST', `/api/subsystem/${id}/reveal-secret`, { token: rootToken, body: { encryptedKey } })
  if (r.status !== 200) throw new Error(`reveal-secret 失败: ${r.status} ${r.text.slice(0, 160)}`)
  const iv = Buffer.from(r.json.iv, 'base64')
  const payload = Buffer.from(r.json.data, 'base64')
  const decipher = crypto.createDecipheriv('aes-256-gcm', envelopeKey, iv)
  decipher.setAuthTag(payload.subarray(-16))
  return Buffer.concat([decipher.update(payload.subarray(0, -16)), decipher.final()]).toString('utf8')
}

const TS = Date.now().toString().slice(-8)

const si = await (await fetch(BASE + '/api/common/serverinfo')).json()
RSA_KEY = si.rsaPublic

const rootLogin = await api('POST', '/api/auth/login', { body: { userName: 'admin', password: rsa('abcd123456') } })
if (rootLogin.status !== 200) {
  console.error('root 登录失败，终止:', rootLogin.status, rootLogin.text.slice(0, 200))
  process.exit(1)
}
const root = rootLogin.json.accessToken

console.log(`目标：${BASE}\n`)

// ---- 清理：删除全部系统级接入（软删，appid 释放可复用）----
const old = await api('GET', '/api/subsystem/list', { token: root })
let removed = 0
for (const item of old.json?.items ?? []) {
  const r = await api('DELETE', `/api/subsystem/${item.id}`, { token: root })
  if (r.status === 200) removed++
}
console.log(`清理系统级接入 ${removed} 条`)

// ---- 演示团队（root 创建即 Owner 入队，SSO 签发需要团队身份）----
const teamRd = await api('POST', '/api/team', { token: root, body: { name: `演示研发团队-${TS}`, description: 'demo seed' } })
const TID_RD = Number(teamRd.json?.value)

// ---- 1. 子系统：智能报表（双回调 + 全员 + 组织授权 down）----
const reportCbProd = 'https://report.demo.local/sso/callback'
const reportCbTest = 'https://test.report.demo.local/sso/callback'
const report = await api('POST', '/api/subsystem/create', { token: root, body: { appType: 'subsystem', key: 'report-demo', name: '智能报表（演示）', description: '双回调地址 + 全员可用 + 组织授权', teamScope: 'allTeams', callbackUrls: [reportCbProd, reportCbTest] } })
const REPORT_ID = report.json?.id
const REPORT_SECRET = await revealSecret(root, REPORT_ID)
check('demo-1 创建子系统 report-demo（双回调）', report.status === 200 && report.json?.key === 'report-demo' && report.json?.appType === 'subsystem' && Array.isArray(report.json?.callbackUrls) === false && report.json?.secretMask?.includes('****'), report.text.slice(0, 160))

const role = await api('POST', '/api/org/role/create', { token: root, body: { name: `演示组织-${TS}`, parentId: null } })
const ROLE_ID = Number(role.json?.orgRoleId)
await api('PUT', `/api/subsystem/${REPORT_ID}/org-grants`, { token: root, body: { grants: [{ orgRoleId: ROLE_ID, scope: 'down' }] } })

// ---- 2. 子系统：OA 协同（指定团队）----
const oa = await api('POST', '/api/subsystem/create', { token: root, body: { appType: 'subsystem', key: 'oa-demo', name: 'OA 协同（演示）', description: '指定团队授权', teamScope: 'specifiedTeams', teamIds: [TID_RD], callbackUrls: ['https://oa.demo.local/sso/callback'] } })
check('demo-2 创建子系统 oa-demo（指定团队）', oa.status === 200 && oa.json?.key === 'oa-demo', oa.text.slice(0, 120))
const OA_SECRET = await revealSecret(root, oa.json?.id)

// ---- 3/4. 应用接入（自动生成结构化 appid）----
const erp = await api('POST', '/api/subsystem/create', { token: root, body: { appType: 'app', name: 'ERP 组织同步（演示）', description: 'ERP 定时推送组织/用户' } })
const hr = await api('POST', '/api/subsystem/create', { token: root, body: { appType: 'app', name: 'HR 用户推送（演示）', description: 'HR 系统推送用户' } })
const ERP_ID = erp.json?.id
const ERP_KEY = erp.json?.key
const ERP_SECRET = await revealSecret(root, ERP_ID)
check('demo-3 创建应用接入 ERP（结构化 appid 35 位）', erp.status === 200 && ERP_KEY?.startsWith('moai-sa-') && ERP_KEY.length === 35, `${erp.status} len=${ERP_KEY?.length}`)
check('demo-4 创建应用接入 HR', hr.status === 200 && hr.json?.key?.startsWith('moai-sa-'), hr.text.slice(0, 120))

// ---- 功能验证 ----
const tokenOk = await api('POST', '/api/subsys/token', { body: { appId: 'report-demo', secret: REPORT_SECRET } })
check('verify-1 子系统统一 token 换取 200', tokenOk.status === 200 && !!tokenOk.json?.accessToken, `${tokenOk.status}`)

const direct = await api('GET', '/api/subsys/info', { token: ERP_KEY })
check('verify-2 应用接入结构化 appid 直连自省 200', direct.status === 200 && direct.json?.id === ERP_ID && direct.json?.appType === 'app', `${direct.status} ${direct.text.slice(0, 120)}`)

const appToken = await api('POST', '/api/subsys/token', { body: { appId: ERP_KEY, secret: ERP_SECRET } })
const hb = appToken.status === 200 ? await api('POST', '/api/subsys/heartbeat', { token: appToken.json?.accessToken, body: { instanceId: `demo-inst-${TS}`, version: 'demo' } }) : { status: 0 }
const erpInfo = hb.status === 200 ? await api('GET', '/api/subsys/info', { token: ERP_KEY }) : null
check('verify-3 心跳后列表/自省在线（1 实例）', hb.status === 200 && erpInfo?.json?.online === true && Number(erpInfo?.json?.onlineInstances) === 1, `hb=${hb.status} info=${erpInfo?.status}`)

const list = await api('GET', '/api/subsystem/list', { token: root })
const reportItem = (list.json?.items ?? []).find((x) => x.key === 'report-demo')
check('verify-4 列表回显双回调与组织授权', !!reportItem && (reportItem.callbackUrls ?? []).length === 2 && (reportItem.authorizedOrgs ?? []).length === 1, JSON.stringify(reportItem ?? null).slice(0, 220))

const ssoTest = await api('POST', '/api/subsystem/sso/code', { token: root, body: { subsystemKey: 'report-demo', redirectUri: reportCbTest } })
check('verify-5 redirect_uri 指定测试入口回跳', ssoTest.status === 200 && ssoTest.json?.callbackUrl === reportCbTest, `${ssoTest.status} ${ssoTest.text.slice(0, 160)}`)

let rlStatus = 0
for (let i = 0; i < 12; i++) {
  rlStatus = (await api('POST', '/api/subsys/token', { body: { appId: 'report-demo', secret: 'wrong' } })).status
}
check('verify-6 限流：错密钥 10 次后 429（窗口 60s 自愈）', rlStatus === 429, `last=${rlStatus}`)
const rlOk = await api('POST', '/api/subsys/token', { body: { appId: 'oa-demo', secret: OA_SECRET } })
check('verify-7 限流维度隔离：其他 appid 正常换取', rlOk.status === 200, `${rlOk.status}`)

// ---- 汇总 ----
const finalList = await api('GET', '/api/subsystem/list', { token: root })
console.log('\n===== Demo 数据清单 =====')
for (const item of finalList.json?.items ?? []) {
  const type = item.appType === 'app' ? '应用接入' : '子系统'
  const appid = item.appType === 'app' ? item.keyPrefix : item.key
  const online = item.appType === 'app' ? '-' : item.online ? `在线（${item.onlineInstances} 实例）` : '离线'
  console.log(`  [${type}] ${item.name} | appid=${appid} | 密钥掩码=${item.secretMask || '-'} | 回调=${(item.callbackUrls ?? []).length} 个 | 在线=${online} | 状态=${item.status}`)
}
console.log(`\n组织角色「演示组织-${TS}」已 down 授权给智能报表；演示研发团队-${TS} 已授权给 OA 协同`)
console.log(`结果: PASS=${PASS} FAIL=${FAIL}`)
if (FAIL > 0) process.exit(1)
