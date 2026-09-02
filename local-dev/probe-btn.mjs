// 探针：诊断 /users 查询按钮为何无法点击
import { createRequire } from 'node:module'
const require = createRequire('/Users/wen/project/maomi/moai/ui/package.json')
const { chromium } = require('playwright')

const browser = await chromium.launch()
const page = await browser.newPage({ viewport: { width: 1440, height: 900 } })
await page.goto('http://localhost:4000/login')
await page.getByPlaceholder('请输入用户名').fill('admin')
await page.getByPlaceholder('请输入密码').fill('abcd123456')
await page.getByRole('button', { name: '立即登录' }).click()
await page.waitForURL('**/dashboard', { timeout: 8000 })
await page.getByRole('menuitem', { name: '用户' }).click()
await page.waitForURL('**/users', { timeout: 5000 })
await page.waitForSelector('tbody tr')
await page.waitForTimeout(4000)

// 先复现故障路径：打开查看弹窗 → Escape 关闭
const row = page.locator('tbody tr').filter({ has: page.getByRole('button', { name: '重置密码' }) }).first();
await row.getByRole('button', { name: '查看' }).click();
await page.waitForSelector('.ant-modal:visible');
await page.keyboard.press('Escape');
await page.waitForTimeout(800);
const bodyAria = await page.evaluate(() => document.body.getAttribute('aria-hidden'));
console.log('body aria-hidden after modal close =', bodyAria);
// 尝试点击查询按钮
let clicked = true, err = null;
try {
  await page.getByRole('button', { name: '查询' }).click({ timeout: 4000 });
} catch (e) { clicked = false; err = e.message.split('\n')[0]; }
console.log('direct click after modal:', clicked ? 'OK' : 'FAIL :: ' + err);

const out = await page.evaluate(async () => {
  const btn = [...document.querySelectorAll('button')].find(b => b.textContent?.includes('查询'));
  if (!btn) return { found: false };
  const r1 = btn.getBoundingClientRect();
  await new Promise(rs => setTimeout(rs, 600));
  const r2 = btn.getBoundingClientRect();
  const cx = Math.round(r1.x + r1.width / 2), cy = Math.round(r1.y + r1.height / 2);
  const hit = document.elementFromPoint(cx, cy);
  const s = getComputedStyle(btn);
  return {
    found: true,
    stable: r1.x === r2.x && r1.y === r2.y && r1.width === r2.width,
    rect: { x: r1.x, y: r1.y, w: r1.width, h: r1.height },
    center: { cx, cy },
    hitAtCenter: hit ? `${hit.tagName}.${(hit.className || '').toString().slice(0, 40)}` : null,
    hitIsInsideBtn: btn.contains(hit),
    visible: s.visibility, display: s.display, pointerEvents: s.pointerEvents,
    disabled: btn.disabled,
    animRunning: document.getAnimations ? document.getAnimations().filter(a => a.effect?.target && btn.contains(a.effect.target)).length : -1,
    animTotal: document.getAnimations ? document.getAnimations().length : -1,
  };
});
console.log(JSON.stringify(out, null, 2));
await browser.close();
