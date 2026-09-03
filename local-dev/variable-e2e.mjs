// 变量管理 E2E（场景 @VR-Sn；后端 127.0.0.1:5210）
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

  const owner = await mkuser('vo')
  const member = await mkuser('vm')
  const outsider = await mkuser('vx')

  const TID = Number((await api('POST', '/api/team', { token: owner.token, body: { name: 'var-team-' + TS } })).json.value)
  await api('POST', `/api/team/${TID}/users`, { token: owner.token, body: { userId: member.userId, role: 2 } })

  // VR-01 无 token 401
  check('VR-01 无 token 查列表 401', (await api('GET', `/api/variable/list?teamId=${TID}`)).status === 401)

  // VR-02 校验
  check('VR-02a 非法变量名 400', (await api('POST', '/api/variable', { token: owner.token, body: { teamId: TID, key: '1bad-key', value: 'v' } })).status === 400)
  check('VR-02b 空值 400', (await api('POST', '/api/variable', { token: owner.token, body: { teamId: TID, key: 'OK_KEY', value: '' } })).status === 400)
  check('VR-02c 非法 teamId 400', (await api('POST', '/api/variable', { token: owner.token, body: { teamId: 0, key: 'OK_KEY', value: 'v' } })).status === 400)

  // VR-03 权限：Member 403 / 非成员 404
  check('VR-03a Member 创建 403', (await api('POST', '/api/variable', { token: member.token, body: { teamId: TID, key: 'NOPE', value: 'v' } })).status === 403)
  check('VR-03b 非成员创建 404', (await api('POST', '/api/variable', { token: outsider.token, body: { teamId: TID, key: 'NOPE', value: 'v' } })).status === 404)

  // VR-04 创建普通 + 私密变量（分组）
  const p1 = await api('POST', '/api/variable', { token: owner.token, body: { teamId: TID, key: 'WIKI_NAME', groupName: '基础配置', value: '团队知识库', description: '站点名' } })
  check('VR-04a 创建普通变量 200', p1.status === 200 && Number(p1.json?.value) > 0, `${p1.status}`)
  const PLAIN_ID = Number(p1.json?.value)
  const p2 = await api('POST', '/api/variable', { token: owner.token, body: { teamId: TID, key: 'FEISHU_SECRET', groupName: '飞书', isSecret: true, value: 'super-secret-abc', description: '飞书应用密钥' } })
  check('VR-04b 创建私密变量 200', p2.status === 200, `${p2.status} ${p2.text.slice(0, 100)}`)
  const SECRET_ID = Number(p2.json?.value)

  // VR-05 重名 409
  check('VR-05 同团队重名 409', (await api('POST', '/api/variable', { token: owner.token, body: { teamId: TID, key: 'WIKI_NAME', value: 'x' } })).status === 409)

  // VR-06 列表：Member 可见普通值、私密值掩码
  {
    const r = await api('GET', `/api/variable/list?teamId=${TID}`, { token: member.token })
    const items = r.json?.items ?? []
    const plain = items.find(i => i.key === 'WIKI_NAME')
    const secret = items.find(i => i.key === 'FEISHU_SECRET')
    check('VR-06a Member 列表 200 且 myRole=2', r.status === 200 && r.json.myRole === 2)
    check('VR-06b 普通变量成员可见值', plain?.value === '团队知识库', JSON.stringify(plain))
    check('VR-06c 私密变量值掩码(字段不回传)且名字可见', !!secret && secret.isSecret === true && secret.value == null, JSON.stringify(secret))
  }

  // VR-07 详情：私密值 Member 403 / Admin 解密可见
  check('VR-07a Member 查私密详情 403', (await api('GET', `/api/variable/${SECRET_ID}`, { token: member.token })).status === 403)
  {
    const r = await api('GET', `/api/variable/${SECRET_ID}`, { token: owner.token })
    check('VR-07b Admin 查私密详情解密回原值', r.status === 200 && r.json?.value === 'super-secret-abc', r.text.slice(0, 120))
  }

  // VR-08 非成员列表/详情 404
  check('VR-08a 非成员列表 404', (await api('GET', `/api/variable/list?teamId=${TID}`, { token: outsider.token })).status === 404)
  check('VR-08b 非成员详情 404', (await api('GET', `/api/variable/${PLAIN_ID}`, { token: outsider.token })).status === 404)

  // VR-09 更新：Member 403；分组/描述/值更新；私密留空保持
  check('VR-09a Member 更新 403', (await api('PUT', `/api/variable/${PLAIN_ID}`, { token: member.token, body: { value: 'hack' } })).status === 403)
  check('VR-09b Admin 更新普通值 200', (await api('PUT', `/api/variable/${PLAIN_ID}`, { token: owner.token, body: { groupName: '基础配置', value: '团队知识库 v2', description: '站点名改' } })).status === 200)
  check('VR-09c 私密留空(null)保持不变', await (async () => {
    await api('PUT', `/api/variable/${SECRET_ID}`, { token: owner.token, body: { description: '飞书密钥改' } })
    return (await api('GET', `/api/variable/${SECRET_ID}`, { token: owner.token })).json?.value
  })() === 'super-secret-abc')
  check('VR-09d 私密提供新值则更新', await (async () => {
    await api('PUT', `/api/variable/${SECRET_ID}`, { token: owner.token, body: { value: 'rotated-secret-xyz' } })
    return (await api('GET', `/api/variable/${SECRET_ID}`, { token: owner.token })).json?.value
  })() === 'rotated-secret-xyz')

  // VR-10 替换：普通+私密均替换；未知保留；Member 403
  {
    const content = 'feishu app=${WIKI_NAME} secret=${FEISHU_SECRET} unknown=${NOPE_KEY}'
    const r = await api('POST', '/api/variable/substitute', { token: owner.token, body: { teamId: TID, content } })
    check('VR-10a 替换含私密与未知保留', r.status === 200 && r.json?.content === 'feishu app=团队知识库 v2 secret=rotated-secret-xyz unknown=${NOPE_KEY}', JSON.stringify(r.json))
    check('VR-10b Member 替换 403', (await api('POST', '/api/variable/substitute', { token: member.token, body: { teamId: TID, content: 'x' } })).status === 403)
  }

  // VR-11 分组筛选
  check('VR-11 分组筛选命中', await (async () => {
    const r = await api('GET', `/api/variable/list?teamId=${TID}&groupName=飞书`, { token: owner.token })
    const keys = (r.json?.items ?? []).map(i => i.key)
    return keys.length === 1 && keys[0] === 'FEISHU_SECRET'
  })())

  // VR-12 删除：Member 403 / Admin 200 / 删除后同名可重建
  check('VR-12a Member 删除 403', (await api('DELETE', `/api/variable/${PLAIN_ID}`, { token: member.token })).status === 403)
  check('VR-12b Admin 删除 200', (await api('DELETE', `/api/variable/${PLAIN_ID}`, { token: owner.token })).status === 200)
  check('VR-12c 删除后同名可重建', (await api('POST', '/api/variable', { token: owner.token, body: { teamId: TID, key: 'WIKI_NAME', value: 're' } })).status === 200)

  console.log(`\n===== 变量 E2E 汇总: PASS=${PASS} FAIL=${FAIL} =====`)
  process.exit(FAIL > 0 ? 1 : 0)
}

main().catch(e => { console.error('脚本异常:', e); process.exit(2) })
