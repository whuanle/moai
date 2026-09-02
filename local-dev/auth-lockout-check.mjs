// 登录锁定自检：5 次错误密码 → 第 6 次正确密码也应 403 → 清 Redis key → 恢复
// 用法: node auth-lockout-check.mjs [baseUrl] [userName] [password]
const BASE = process.argv[2] ?? 'http://127.0.0.1:5210'
const USER = process.argv[3] ?? 'admin'
const PASS = process.argv[4] ?? 'abcd123456'
const crypto = await import('node:crypto')

const { rsaPublic } = await fetch(`${BASE}/api/common/serverinfo`).then((r) => r.json())

function enc(text) {
  const key = crypto.createPublicKey({ key: Buffer.from(rsaPublic, 'base64'), format: 'der', type: 'spki' })
  return crypto.publicEncrypt({ key, padding: crypto.constants.RSA_PKCS1_PADDING }, Buffer.from(text)).toString('base64')
}

async function login(password) {
  const r = await fetch(`${BASE}/api/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ userName: USER, password: enc(password) }),
  })
  return { status: r.status, body: await r.json().catch(() => null) }
}

let pass = 0
let fail = 0
const check = (name, cond) => {
  console.log(`${cond ? 'PASS' : 'FAIL'} ${name}`)
  cond ? pass++ : fail++
}

// 1. 正常登录可用
const ok0 = await login(PASS)
check('基线正确密码可登录', ok0.status === 200)

// 2. 5 次错误密码
for (let i = 1; i <= 5; i++) {
  const bad = await login('wrong' + i + 'A')
  check(`第 ${i} 次错误密码 401`, bad.status === 401)
}

// 3. 第 6 次即使密码正确也应 403 锁定
const locked = await login(PASS)
check('锁定后正确密码也 403', locked.status === 403 && /次数过多/.test(locked.body?.detail ?? ''))

// 4. 清 key 恢复（StackExchange.Redis.Extensions 默认带 moai: 前缀）
const { execSync } = await import('node:child_process')
execSync(`docker exec moai-redis redis-cli DEL "moai:login:fail:${USER}"`)
const ok1 = await login(PASS)
check('清 key 后恢复登录', ok1.status === 200)

console.log(`\n结果: ${pass} 通过, ${fail} 失败`)
process.exit(fail > 0 ? 1 : 0)
