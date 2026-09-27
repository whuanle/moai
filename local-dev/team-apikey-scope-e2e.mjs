// 接入 key 功能范围（access_app.scopes）E2E
// 场景编号 TA-*（GW/WX 之外独立系列；TA-01~21/23/24/32e/f/35/37 为已下线的团队接入 key（moai-，team_api_key）
// 场景，编号永久退役不复用；接入统一收敛到应用接入 key（moai-ac-））
// 用法: node local-dev/team-apikey-scope-e2e.mjs [baseUrl]   （需后端运行中，默认 http://127.0.0.1:5210）
// 覆盖：应用接入 scopes 管理端回显、越维/未知 scope 400、wiki 读/写范围校验、
//   旧 accessAppKey 路径兼容（全量知识库范围）、修改范围后按接入当前勾选签发、refresh 吊销链路、
//   应用接入 key 直连（免换 token）：知识库按范围放行、勾选 model 直连模型网关；
//   应用对话范围（app_chat）：换用户 token 门禁与刷新重验；改范围/删除后直连下一请求即生效（Redis 缓存失效）。
// 说明：写入正向用例走 preupload→PUT 直传→complete 真实三段式（依赖 MinIO 可达，同 wiki-external-e2e 口径）。
const BASE = process.argv[2] ?? 'http://127.0.0.1:5210'
const crypto = await import('node:crypto')

let pass = 0
let fail = 0

function check(name, cond, detail = '') {
  if (cond) {
    pass++
    console.log(`PASS | ${name}`)
  } else {
    fail++
    console.log(`FAIL | ${name} ${detail ? '— ' + detail : ''}`)
  }
}

let RSA_KEY = ''
const rsa = (plain) => {
  const key = crypto.createPublicKey({ key: Buffer.from(RSA_KEY, 'base64'), format: 'der', type: 'spki' })
  return crypto.publicEncrypt({ key, padding: crypto.constants.RSA_PKCS1_PADDING }, Buffer.from(plain, 'utf8')).toString('base64')
}
const sha256Hex = (content) => crypto.createHash('sha256').update(content).digest('hex')

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
async function retryApi(method, path, body, token, tries = 4) {
  let last = null
  for (let i = 0; i < tries; i++) {
    const r = await api(method, path, { token, body })
    if (r.status !== 500) return r
    last = r
    await new Promise((resolve) => setTimeout(resolve, 1500 * (i + 1)))
  }
  return last
}
// 共享开发库偶发 500，建团队/知识库做有限重试（同 wiki-external-e2e 口径）

const TS = Date.now().toString().slice(-8)
const xw = (wikiId, suffix = '') => `/api/external/wiki/${wikiId}${suffix}`

async function main() {
  const si = await api('GET', '/api/common/serverinfo')
  RSA_KEY = si.json.rsaPublic
  const login = await api('POST', '/api/auth/login', { body: { userName: 'admin', password: rsa('abcd123456') } })
  if (login.status !== 200 || !login.json?.accessToken) throw new Error(`admin 登录失败: ${login.status}`)
  const token = login.json.accessToken

  // ===== 准备：团队 + 知识库 + 应用接入 =====
  const T = Number((await retryApi('POST', '/api/team', { name: 'ta-team-' + TS }, token)).json?.value)
  if (!Number.isFinite(T)) throw new Error('创建团队失败')
  const w = await retryApi('POST', '/api/wiki', { teamId: T, name: 'ta-wiki-' + TS, description: 'team apikey scope e2e' }, token)
  if (w.status !== 200) throw new Error(`建知识库失败: ${w.status} ${w.text.slice(0, 120)}`)
  const W = Number(w.json?.value)
  const acc = await retryApi('POST', '/api/access-app', { teamId: T, name: 'ta接入-' + TS, description: 'team apikey scope e2e' }, token)
  if (acc.status !== 200) throw new Error(`创建应用接入失败: ${acc.status}`)
  const ACCESS_KEY = acc.json?.key
  const gwBase = `${BASE}/api/aigateway/${T}/v1`
  const kdHeaders = (key) => ({ Authorization: `Bearer ${key}`, 'Content-Type': 'application/json' })
  const mint = (accessAppKey, extra = {}) => api('POST', '/api/external/token', { body: { accessAppKey, ...extra } })

  // ===== TA-25~28 应用接入（access_app）自身功能范围 =====
  {
    const accRead = await retryApi('POST', '/api/access-app', { teamId: T, name: 'ta接入只读-' + TS, description: 'scope e2e', scopes: ['wiki_read'] }, token)
    const KEY_READ = accRead.json?.key
    const ID_READ = accRead.json?.accessAppId
    const listAcc = await api('GET', `/api/access-app/list?teamId=${T}`, { token })
    const itemRead = (listAcc.json?.items ?? []).find((x) => x.accessAppId === ID_READ)
    check('TA-25a 创建只读接入 200 且列表回显 scopes', accRead.status === 200 && Array.isArray(itemRead?.scopes) && itemRead.scopes.includes('wiki_read') && !itemRead.scopes.includes('wiki_write'), `${accRead.status} ${JSON.stringify(itemRead)}`)

    const badAcc = await retryApi('POST', '/api/access-app', { teamId: T, name: 'ta接入非法-' + TS, description: 'scope e2e', scopes: ['hacker'] }, token)
    check('TA-25b 非法/越维代码创建接入 400', badAcc.status === 400, `${badAcc.status} ${badAcc.text.slice(0, 120)}`)

    const tokAR = await api('POST', '/api/external/token', { body: { accessAppKey: KEY_READ } })
    const AT_AR = tokAR.json?.accessToken
    const readOk = await api('POST', xw(W, '/documents/list'), { token: AT_AR, body: { pageNo: 1, pageSize: 10 } })
    const writeDenied = await api('POST', xw(W, '/documents/preupload'), { token: AT_AR, body: { fileName: `ta-ar-${TS}.md`, contentType: 'text/markdown', fileSize: 10, sha256: sha256Hex(`ta-ar-${TS}`) } })
    check('TA-26 只读接入 token 读放行写 403', tokAR.status === 200 && readOk.status === 200 && writeDenied.status === 403, `tok=${tokAR.status} read=${readOk.status} write=${writeDenied.status}`)

    const updAcc = await api('PUT', `/api/access-app/${ID_READ}`, { token, body: { name: 'ta接入只读-' + TS, description: 'scope e2e', scopes: ['wiki_read', 'wiki_write'] } })
    const writeStillDenied = await api('POST', xw(W, '/documents/preupload'), { token: AT_AR, body: { fileName: `ta-ar2-${TS}.md`, contentType: 'text/markdown', fileSize: 10, sha256: sha256Hex(`ta-ar2-${TS}`) } })
    const refAR = await api('POST', '/api/external/token/refresh', { body: { refreshToken: tokAR.json?.refreshToken } })
    const writeAfterRefresh = await api('POST', xw(W, '/documents/preupload'), { token: refAR.json?.accessToken, body: { fileName: `ta-ar3-${TS}.md`, contentType: 'text/markdown', fileSize: 10, sha256: sha256Hex(`ta-ar3-${TS}`) } })
    check('TA-27 改范围后旧 token 不变、refresh 后写放行', updAcc.status === 200 && writeStillDenied.status === 403 && refAR.status === 200 && writeAfterRefresh.status === 200, `upd=${updAcc.status} old=${writeStillDenied.status} ref=${refAR.status} new=${writeAfterRefresh.status}`)

    const accDefault = await retryApi('POST', '/api/access-app', { teamId: T, name: 'ta接入默认-' + TS, description: 'scope e2e' }, token)
    const tokAD = await api('POST', '/api/external/token', { body: { accessAppKey: accDefault.json?.key } })
    const dRead = await api('POST', xw(W, '/documents/list'), { token: tokAD.json?.accessToken, body: { pageNo: 1, pageSize: 10 } })
    const dWrite = await api('POST', xw(W, '/documents/preupload'), { token: tokAD.json?.accessToken, body: { fileName: `ta-ad-${TS}.md`, contentType: 'text/markdown', fileSize: 10, sha256: sha256Hex(`ta-ad-${TS}`) } })
    check('TA-28 兼容：不传 scopes 的接入 token 读写全放行', accDefault.status === 200 && tokAD.status === 200 && dRead.status === 200 && dWrite.status === 200, `tok=${tokAD.status} read=${dRead.status} write=${dWrite.status}`)

    // 读写范围接入的三段式真实上传（原 TA-15~17 行为，来源改应用接入）
    const accRw = await retryApi('POST', '/api/access-app', { teamId: T, name: 'ta接入读写-' + TS, description: 'scope e2e', scopes: ['wiki_read', 'wiki_write'] }, token)
    const tokRw = await mint(accRw.json?.key)
    const AT_RW = tokRw.json?.accessToken
    const mdContent = Buffer.from(`# team apikey scope e2e ${TS}\n\n内容用于验证写范围三段式上传。`, 'utf8')
    const pre = await api('POST', xw(W, '/documents/preupload'), { token: AT_RW, body: { fileName: `ta-rw-${TS}.md`, contentType: 'text/markdown', fileSize: mdContent.length, sha256: sha256Hex(mdContent) } })
    let uploaded = false
    if (pre.status === 200 && pre.json?.uploadUrl) {
      const put = await fetch(pre.json.uploadUrl, { method: 'PUT', headers: { 'Content-Type': 'text/markdown' }, body: mdContent })
      const comp = await api('POST', xw(W, '/documents/complete'), { token: AT_RW, body: { isSuccess: true, fileId: pre.json.fileId, fileName: `ta-rw-${TS}.md` } })
      uploaded = put.status === 200 && comp.status === 200
      check('TA-38a 写范围接入 token 三段式 PUT 直传 + complete 200', uploaded, `put=${put.status} comp=${comp.status} ${comp.text.slice(0, 120)}`)
    } else {
      check('TA-38a 写范围接入 token 三段式 PUT 直传 + complete 200', false, 'preupload 未成功')
    }
    const listRw = await api('POST', xw(W, '/documents/list'), { token: AT_RW, body: { pageNo: 1, pageSize: 50 } })
    check('TA-38b 写范围接入 token 文档列表出现新文档', uploaded && (listRw.json?.items ?? []).some((x) => Number(x.fileId) === Number(pre.json.fileId)), `${listRw.status}`)

    // 参数校验：伪造 key 换 token 401
    const forged = await mint('moai-ac-invalidinvalidinvalidinvalidinvalid')
    check('TA-39 伪造接入 key 换 token 401', forged.status === 401, `${forged.status}`)

    const userThrough = await api('POST', '/api/external/token', { body: { accessAppKey: accDefault.json?.key, appId: '00000000-0000-0000-0000-000000000000', externalUserId: 'ta-chat-' + TS } })
    check('TA-33 默认接入(含 app_chat) 换用户 token 过对话门禁 404 应用不存在', userThrough.status === 404, `${userThrough.status} ${userThrough.text.slice(0, 120)}`)

    const userBlocked = await api('POST', '/api/external/token', { body: { accessAppKey: KEY_READ, appId: '00000000-0000-0000-0000-000000000000', externalUserId: 'ta-nochat-' + TS } })
    check('TA-34 无 app_chat 接入换用户 token 403', userBlocked.status === 403, `${userBlocked.status} ${userBlocked.text.slice(0, 120)}`)

    // 清理接入
    await api('DELETE', `/api/access-app/${ID_READ}`, { token })
    await api('DELETE', `/api/access-app/${accDefault.json?.accessAppId}`, { token })
    await api('DELETE', `/api/access-app/${accRw.json?.accessAppId}`, { token })
  }

  // ===== TA-32 应用接入 key 直连：模型网关 + 团队资源免换 token =====
  {
    const accModel = await retryApi('POST', '/api/access-app', { teamId: T, name: 'ta接入网关-' + TS, description: 'key direct e2e', scopes: ['model', 'wiki_read'] }, token)
    const KEY_MODEL = accModel.json?.key
    const listM = await api('GET', `/api/access-app/list?teamId=${T}`, { token })
    const itemM = (listM.json?.items ?? []).find((x) => x.accessAppId === accModel.json?.accessAppId)
    check('TA-32a 创建含 model 接入 200 且回显 model', accModel.status === 200 && Array.isArray(itemM?.scopes) && itemM.scopes.includes('model'), `${accModel.status} ${JSON.stringify(itemM)}`)

    const gwAcc = await fetch(`${gwBase}/models`, { headers: kdHeaders(KEY_MODEL) })
    check('TA-32b 接入 key(model) 直连网关 models 200', gwAcc.status === 200, `${gwAcc.status}`)

    const kdRead = await fetch(`${BASE}${xw(W, '/documents/list')}`, { method: 'POST', headers: kdHeaders(KEY_MODEL), body: JSON.stringify({ pageNo: 1, pageSize: 10 }) })
    check('TA-32c 接入 key(wiki_read) 直连读知识库 200', kdRead.status === 200, `${kdRead.status}`)
    const kdWrite = await fetch(`${BASE}${xw(W, '/documents/preupload')}`, { method: 'POST', headers: kdHeaders(KEY_MODEL), body: JSON.stringify({ fileName: `ta-kd-${TS}.md`, contentType: 'text/markdown', fileSize: 10, sha256: sha256Hex(`ta-kd-${TS}`) }) })
    check('TA-32d 接入 key 无 wiki_write 直连上传 403', kdWrite.status === 403, `${kdWrite.status}`)

    const accNoModel = await retryApi('POST', '/api/access-app', { teamId: T, name: 'ta接入无网关-' + TS, description: 'key direct e2e', scopes: ['wiki_read'] }, token)
    const gwAcc2 = await fetch(`${gwBase}/models`, { headers: kdHeaders(accNoModel.json?.key) })
    const gwAcc2Body = await gwAcc2.json().catch(() => null)
    check('TA-32g 接入 key 无 model 直连网关 403 insufficient_scope', gwAcc2.status === 403 && gwAcc2Body?.error?.code === 'insufficient_scope', `${gwAcc2.status} ${JSON.stringify(gwAcc2Body)}`)

    const kdBad = await fetch(`${BASE}/api/external/wiki/${W}`, { headers: kdHeaders('moai-ac-invalid000000000000000000000') })
    check('TA-32h 伪造接入 key 直连 401', kdBad.status === 401, `${kdBad.status}`)

    const legacyDirect = await fetch(`${BASE}/api/external/wiki/${W}`, { headers: kdHeaders('moai-invalidinvalidinvalidinvalidinvalid') })
    check('TA-32j 已下线团队 key 前缀(moai-)直连 401', legacyDirect.status === 401, `${legacyDirect.status}`)

    const updAccModel = await api('PUT', `/api/access-app/${accNoModel.json?.accessAppId}`, { token, body: { name: 'ta接入无网关-' + TS, description: 'key direct e2e', scopes: ['wiki_read', 'model'] } })
    const gwAcc3 = await fetch(`${gwBase}/models`, { headers: kdHeaders(accNoModel.json?.key) })
    check('TA-32i 补 model 后接入 key 网关放行', updAccModel.status === 200 && gwAcc3.status === 200, `upd=${updAccModel.status} gw=${gwAcc3.status}`)

    await api('DELETE', `/api/access-app/${accModel.json?.accessAppId}`, { token })
    await api('DELETE', `/api/access-app/${accNoModel.json?.accessAppId}`, { token })
  }

  // ===== TA-36 范围/删除变更后直连立即生效（Redis 缓存失效链路）=====
  {
    const accInv = await retryApi('POST', '/api/access-app', { teamId: T, name: 'ta接入缓存-' + TS, description: 'cache invalidation e2e', scopes: ['wiki_read'] }, token)
    const KEY_INV = accInv.json?.key
    const ID_INV = accInv.json?.accessAppId
    const rdBefore = await fetch(`${BASE}${xw(W, '/documents/list')}`, { method: 'POST', headers: { Authorization: `Bearer ${KEY_INV}`, 'Content-Type': 'application/json' }, body: JSON.stringify({ pageNo: 1, pageSize: 10 }) })

    await api('PUT', `/api/access-app/${ID_INV}`, { token, body: { name: 'ta接入缓存-' + TS, description: 'cache invalidation e2e', scopes: ['kg_read'] } })
    const rdAfter = await fetch(`${BASE}${xw(W, '/documents/list')}`, { method: 'POST', headers: { Authorization: `Bearer ${KEY_INV}`, 'Content-Type': 'application/json' }, body: JSON.stringify({ pageNo: 1, pageSize: 10 }) })
    check('TA-36a 接入去掉 wiki_read 后直连读立即 403（缓存失效）', rdBefore.status === 200 && rdAfter.status === 403, `before=${rdBefore.status} after=${rdAfter.status}`)

    await api('DELETE', `/api/access-app/${ID_INV}`, { token })
    const rdDeleted = await fetch(`${BASE}${xw(W, '/documents/list')}`, { method: 'POST', headers: { Authorization: `Bearer ${KEY_INV}`, 'Content-Type': 'application/json' }, body: JSON.stringify({ pageNo: 1, pageSize: 10 }) })
    check('TA-36b 删除接入后直连读立即 401', rdDeleted.status === 401, `${rdDeleted.status}`)
  }

  // ===== 清理 =====
  const disable = await api('PUT', `/api/admin/team/${T}/disable`, { token, body: { isDisable: true } })
  check('清理：禁用团队', disable.status === 200, `${disable.status}`)
}

try {
  await main()
} catch (err) {
  console.log(`FAIL | 脚本异常 — ${err.message}`)
  fail++
}
console.log(`\n结果: ${pass} passed, ${fail} failed`)
process.exit(fail > 0 ? 1 : 0)
