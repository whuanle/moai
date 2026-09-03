// 知识库文档 E2E（内容层，场景 @WD-Sn；依赖 team/wiki 工厂；后端 127.0.0.1:5210）
import crypto from 'node:crypto'

const BASE = 'http://127.0.0.1:5210'
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

async function main() {
  const si = await api('GET', '/api/common/serverinfo')
  RSA_KEY = si.json.rsaPublic

  const owner = await mkuser('do')
  const member = await mkuser('dm')
  const outsider = await mkuser('dx')

  // 准备团队 + 知识库
  const TID = Number((await api('POST', '/api/team', { token: owner.token, body: { name: 'doc-team-' + TS } })).json.value)
  await api('POST', `/api/team/${TID}/users`, { token: owner.token, body: { userId: member.userId, role: 2 } })
  const WID = Number((await api('POST', '/api/wiki', { token: owner.token, body: { teamId: TID, name: 'doc-wiki-' + TS } })).json.value)

  // WD-01 无 token 401
  check('WD-01 无 token 查文档列表 401', (await api('GET', `/api/wiki/${WID}/documents`)).status === 401)

  // WD-02 校验：空标题 400 / 超长 400
  check('WD-02a 空标题 400', (await api('POST', `/api/wiki/${WID}/documents`, { token: owner.token, body: { title: '' } })).status === 400)
  check('WD-02b 超长标题 400', (await api('POST', `/api/wiki/${WID}/documents`, { token: owner.token, body: { title: 'x'.repeat(101) } })).status === 400)

  // WD-03 非成员 404
  check('WD-03a 非成员建文档 404', (await api('POST', `/api/wiki/${WID}/documents`, { token: outsider.token, body: { title: 't' } })).status === 404)
  check('WD-03b 非成员查列表 404', (await api('GET', `/api/wiki/${WID}/documents`, { token: outsider.token })).status === 404)

  // WD-04 Member 可创建（内容协作）
  const d1 = await api('POST', `/api/wiki/${WID}/documents`, { token: member.token, body: { title: '安装指南', content: '# 安装\n\n步骤……' } })
  check('WD-04a Member 创建文档 200', d1.status === 200 && Number(d1.json?.value) > 0, `${d1.status} ${d1.text.slice(0, 100)}`)
  const DID = Number(d1.json?.value)

  // WD-05 列表（不含正文）+ 详情（含正文）
  {
    const r = await api('GET', `/api/wiki/${WID}/documents`, { token: member.token })
    const item = (r.json?.items ?? []).find(i => Number(i.documentId) === DID)
    check('WD-05a 列表 200 且不含正文字段', r.status === 200 && !!item && !('content' in item), JSON.stringify(item))
    check('WD-05b 列表含 myRole', r.json?.myRole === 2)
  }
  {
    const r = await api('GET', `/api/wiki/document/${DID}`, { token: member.token })
    check('WD-05c 详情含 Markdown 正文', r.status === 200 && r.json?.content?.startsWith('# 安装'), r.text.slice(0, 100))
  }

  // WD-06 Member 可编辑
  const upd = await api('PUT', `/api/wiki/document/${DID}`, { token: member.token, body: { title: '安装指南 v2', content: '# 安装 v2\n\n更新步骤' } })
  check('WD-06a Member 编辑 200', upd.status === 200, `${upd.status}`)
  check('WD-06b 详情回显更新', (await api('GET', `/api/wiki/document/${DID}`, { token: member.token })).json?.title === '安装指南 v2')

  // WD-07 删除权限：Member 403 / Admin 200
  const d2 = await api('POST', `/api/wiki/${WID}/documents`, { token: member.token, body: { title: '待删除' } })
  check('WD-07a Member 删除 403', (await api('DELETE', `/api/wiki/document/${Number(d2.json?.value)}`, { token: member.token })).status === 403)
  check('WD-07b Admin 删除 200', (await api('DELETE', `/api/wiki/document/${Number(d2.json?.value)}`, { token: owner.token })).status === 200)
  check('WD-07c 删除后详情 404', (await api('GET', `/api/wiki/document/${Number(d2.json?.value)}`, { token: owner.token })).status === 404)

  // WD-08 知识库删除后文档不可访问
  const t2 = Number((await api('POST', '/api/team', { token: owner.token, body: { name: 'doc-team2-' + TS } })).json.value)
  const w2 = Number((await api('POST', '/api/wiki', { token: owner.token, body: { teamId: t2, name: 'w2' } })).json.value)
  const d3 = Number((await api('POST', `/api/wiki/${w2}/documents`, { token: owner.token, body: { title: 'x' } })).json.value)
  await api('DELETE', `/api/wiki/${w2}`, { token: owner.token })
  check('WD-08 知识库删除后其文档 404', (await api('GET', `/api/wiki/document/${d3}`, { token: owner.token })).status === 404)

  // 清理
  await api('DELETE', `/api/wiki/document/${DID}`, { token: owner.token })
  await api('DELETE', `/api/wiki/${WID}`, { token: owner.token })
  await api('DELETE', `/api/team/${TID}`, { token: owner.token })

  console.log(`\n===== 文档 E2E 汇总: PASS=${PASS} FAIL=${FAIL} =====`)
  process.exit(FAIL > 0 ? 1 : 0)
}

main().catch(e => { console.error('脚本异常:', e); process.exit(2) })
