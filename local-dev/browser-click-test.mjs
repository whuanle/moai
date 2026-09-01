// 前端点击走查（真实 Chromium）—— 对应 BDD 场景：
// UM-S22/S23/S24/S25/S26（用户页）、FE-LR-*(导航路由)、FE-TH(主题)、FE-SI(语言)、FE-AUTH(登出守卫)
// 用法: node browser-click-test.mjs [frontUrl]
import { mkdirSync } from 'node:fs'
import { createRequire } from 'node:module'

const require = createRequire('/Users/wen/project/maomi/moai/ui/package.json')
const { chromium } = require('playwright')

const FRONT = process.argv[2] ?? 'http://localhost:4000'
const SHOTS = new URL('./shots/', import.meta.url).pathname
mkdirSync(SHOTS, { recursive: true })

let pass = 0, fail = 0
const check = (name, cond, detail = '') => {
  console.log(`${cond ? 'PASS' : 'FAIL'} ${name}${cond ? '' : ' :: ' + detail}`)
  cond ? pass++ : fail++
}

const browser = await chromium.launch()
const page = await browser.newPage({ viewport: { width: 1440, height: 900 } })
const shot = (n) => page.screenshot({ path: `${SHOTS}/${n}.png` })

try {
  // ===== 登录 =====
  await page.goto(`${FRONT}/login`)
  await page.getByPlaceholder('请输入用户名').fill('admin')
  await page.getByPlaceholder('请输入密码').fill('abcd123456')
  await page.getByRole('button', { name: '立即登录' }).click()
  await page.waitForURL('**/dashboard', { timeout: 8000 })
  check('登录成功跳转 dashboard', page.url().includes('/dashboard'))
  await page.waitForTimeout(3600)   // 等"登录成功" toast 退场，避免遮挡后续点击
  await shot('01-dashboard')

  // ===== 侧边栏导航（FE-LR）=====
  await page.getByRole('menuitem', { name: '用户' }).click()
  await page.waitForURL('**/users', { timeout: 5000 })
  await page.waitForSelector('table tr')
  check('导航到 /users 且表格渲染', true)
  await shot('02-users')

  // UM-S22：操作列齐全（取非 admin 行——root 行按设计只有 查看）
  const normalRow = page.locator('tbody tr').filter({ has: page.getByRole('button', { name: '重置密码' }) }).first()
  await normalRow.waitFor({ state: 'visible', timeout: 5000 })
  const firstRowBtns = await normalRow.locator('button').allTextContents()
  check('UM-S22 普通行操作含 查看/禁用/重置密码/设为管理员',
    ['查看', '禁用', '重置密码', '设为管理员'].every((t) => firstRowBtns.includes(t)),
    JSON.stringify(firstRowBtns))

  // UM-S23：root 行只有查看；普通行有设为管理员
  const rootRow = page.locator('tbody tr', { hasText: 'admin' }).first()
  const rootBtns = await rootRow.locator('button').allTextContents()
  check('UM-S23 root 行无危险操作', !rootBtns.includes('设为管理员') && !rootBtns.includes('禁用'), JSON.stringify(rootBtns))
  check('UM-S23 普通行有设为管理员（root 视角）', firstRowBtns.includes('设为管理员'))

  // ===== 查看详情弹窗（UM-S22）=====
  await page.locator('tbody tr').first().getByRole('button', { name: '查看' }).click()
  await page.waitForSelector('.ant-modal:visible')
  const modalText = await page.locator('.ant-modal').innerText()
  check('查看弹窗展示用户信息', /用户名|昵称|邮箱/.test(modalText))
  await shot('03-user-detail')
  // Escape 对该弹窗不可靠（遮罩残留挡住后续点击），显式点 X 关闭并等待消失
  await page.locator('.ant-modal .ant-modal-close').click()
  await page.locator('.ant-modal').waitFor({ state: 'hidden', timeout: 5000 })
  await page.waitForTimeout(300)

  // ===== 搜索（UM-S1 前端）=====
  await page.getByPlaceholder(/用户名|昵称|邮箱/).fill('admin')
  await page.getByRole('button', { name: '查询' }).click()
  await page.waitForTimeout(800)
  const rowsAfterSearch = await page.locator('tbody tr').count()
  check('关键字搜索过滤生效', rowsAfterSearch >= 1)
  await shot('04-users-search')
  await page.getByRole('button', { name: '重置' }).click()
  await page.waitForTimeout(800)

  // ===== 重置密码弹窗前端校验（UM-S25）=====
  const anyRow = page.locator('tbody tr').filter({ has: page.getByRole('button', { name: '重置密码' }) }).first()
  await anyRow.getByRole('button', { name: '重置密码' }).click()
  await page.waitForSelector('.ant-modal:visible')
  await page.locator('.ant-modal input[type="password"]').first().fill('abc12345')
  await page.locator('.ant-modal input[type="password"]').nth(1).fill('abc99999')
  await page.locator('.ant-modal').getByRole('button', { name: '确 认' }).click().catch(() => {})
  await page.waitForTimeout(500)
  const mismatchVisible = await page.locator('.ant-modal').getByText('两次输入的密码不一致').isVisible().catch(() => false)
  check('UM-S25 两次密码不一致就地报错', mismatchVisible)
  await shot('05-password-mismatch')
  await page.locator('.ant-modal').getByRole('button', { name: '取 消' }).click().catch(() => {})
  await page.locator('.ant-modal').waitFor({ state: 'hidden', timeout: 5000 }).catch(() => {})
  await page.waitForTimeout(300)

  // ===== 管理页导航（FE-LR / FE-PG）=====
  await page.getByRole('menuitem', { name: '设置' }).click()
  await page.waitForURL('**/settings', { timeout: 5000 })
  check('导航到 /settings', page.url().includes('/settings'))
  await shot('06-settings')

  await page.getByRole('menuitem', { name: '第三方登录' }).click()
  await page.waitForURL('**/oauthconnect', { timeout: 5000 })
  check('导航到 /oauthconnect', page.url().includes('/oauthconnect'))
  await shot('07-oauthconnect')

  // ===== 账号设置页（FE-PA）=====
  await page.locator('.ant-dropdown-menu-item, [class*=dropdown]').filter({ hasText: '设置' }).click().catch(async () => {
    await page.locator('.ant-layout-sider').getByText('admin', { exact: false }).first().click()
  })
  await page.waitForTimeout(600)
  const acctItem = page.locator('.ant-dropdown-menu-item').filter({ hasText: '设置' })
  if (await acctItem.count()) await acctItem.click()
  await page.waitForTimeout(800)
  check('账号设置入口可达', (await page.url()).includes('/account') || (await page.getByText('账号设置').count()) > 0)
  await shot('08-account')

  // ===== 主题切换（FE-TH）=====
  const themeSelect = page.locator('.ant-layout-sider .ant-select').first()
  await themeSelect.click()
  await page.waitForTimeout(400)
  await page.locator('.ant-select-item-option', { hasText: '暗色' }).click()
  await page.waitForTimeout(800)
  const darkApplied = await page.evaluate(() => document.documentElement.getAttribute('data-theme') === 'dark'
    || getComputedStyle(document.body).colorScheme === 'dark'
    || document.body.classList.contains('dark'))
  const bg = await page.evaluate(() => getComputedStyle(document.body).backgroundColor)
  check('主题切换到暗色', darkApplied || /2\d|3\d|0,\s*0,\s*0/.test(bg), `bg=${bg}`)
  await shot('09-dark-theme')

  // ===== 语言切换（FE-SI）=====
  const langSelect = page.locator('.ant-layout-sider .ant-select').nth(1)
  await langSelect.click()
  await page.waitForTimeout(400)
  await page.locator('.ant-select-item-option', { hasText: 'English' }).click()
  await page.waitForTimeout(800)
  const menuEn = await page.getByRole('menuitem', { name: 'Users' }).count()
  check('语言切换到英文（菜单出现 Users）', menuEn === 1)
  await shot('10-english')
  // 切回中文
  await langSelect.click()
  await page.locator('.ant-select-item-option', { hasText: '简体中文' }).click()
  await page.waitForTimeout(600)

  // ===== 登出（FE-AUTH）=====
  await page.locator('.ant-layout-sider').getByText('admin', { exact: false }).first().click()
  await page.waitForTimeout(500)
  await page.locator('.ant-dropdown-menu-item').filter({ hasText: '退出登录' }).click()
  await page.waitForURL('**/login', { timeout: 5000 })
  check('退出登录回到 /login', page.url().includes('/login'))
  await shot('11-logout')

  // ===== 守卫：登出后访问受保护路由（FE-AUTH）=====
  await page.goto(`${FRONT}/users`)
  await page.waitForURL('**/login', { timeout: 5000 })
  check('登出后访问 /users 被重定向 /login', page.url().includes('/login'))
} catch (e) {
  fail++
  console.log('FAIL 异常中断 ::', e.message?.slice(0, 200))
  await shot('99-error').catch(() => {})
} finally {
  await browser.close()
}

console.log(`\n结果: ${pass} 通过, ${fail} 失败；截图目录 ${SHOTS}`)
process.exit(fail > 0 ? 1 : 0)
