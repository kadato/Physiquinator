// Mobile layout audit for Physiquinator at small viewports.
// Uses Playwright's CDP evaluate (bypasses page CSP) to measure layout issues.
const { chromium } = require('playwright');
const fs = require('fs');
const path = require('path');

const BASE = 'http://localhost:8080';
const OUT = path.resolve(__dirname, 'audit-out');
fs.mkdirSync(OUT, { recursive: true });

const VIEWPORTS = [
  { name: 'small-android', width: 360, height: 740 },   // ~Pixel 5 css width
  { name: 'tiny', width: 320, height: 568 },            // very small phone
  { name: 'narrow', width: 280, height: 653 },          // split-screen / small foldable
];

function auditScript() {
  return () => {
    const vw = document.documentElement.clientWidth;
    const vh = document.documentElement.clientHeight;
    const issues = [];
    const overflowEls = [];
    const smallTargets = [];

    // 1. Page-level horizontal overflow
    if (document.documentElement.scrollWidth > vw + 1) {
      issues.push(`page-h-overflow: scrollWidth=${document.documentElement.scrollWidth} vw=${vw}`);
    }

    // 2. Elements overflowing the right edge (exclude fixed-position chrome)
    for (const el of document.querySelectorAll('body *')) {
      const r = el.getBoundingClientRect();
      if (r.width > 0 && r.right > vw + 1 && getComputedStyle(el).position !== 'fixed') {
        overflowEls.push({ tag: el.tagName, cls: String(el.className).slice(0, 70), right: Math.round(r.right), w: Math.round(r.width) });
      }
    }
    if (overflowEls.length) issues.push(`overflow-elements: ${overflowEls.length}`);

    // 3. Touch targets smaller than 44x44 CSS px (coarse pointer minimum)
    const interactive = 'button, a, input, select, textarea, [role="button"], [role="tab"], [role="menuitem"], .mud-button-root, label';
    for (const el of document.querySelectorAll(interactive)) {
      const r = el.getBoundingClientRect();
      const cs = getComputedStyle(el);
      if (r.width === 0 || r.height === 0) continue;
      if (cs.visibility === 'hidden' || cs.display === 'none') continue;
      if (r.width < 43.5 || r.height < 43.5) {
        // Ignore inline text links inside paragraphs and tiny decorative icons
        smallTargets.push({ tag: el.tagName, cls: String(el.className).slice(0, 60), w: Math.round(r.width), h: Math.round(r.height), text: (el.textContent || '').trim().slice(0, 24) });
      }
    }
    if (smallTargets.length) issues.push(`small-touch-targets: ${smallTargets.length}`);

    // 4. Elements with white-space:nowrap that overflow their container
    const wrapOverflow = [];
    for (const el of document.querySelectorAll('body *')) {
      const cs = getComputedStyle(el);
      if (cs.whiteSpace === 'nowrap') {
        if (el.scrollWidth > el.clientWidth + 2 && el.clientWidth > 0) {
          wrapOverflow.push({ tag: el.tagName, cls: String(el.className).slice(0, 60), scrollW: el.scrollWidth, clientW: el.clientWidth, text: (el.textContent || '').trim().slice(0, 30) });
        }
      }
    }
    if (wrapOverflow.length) issues.push(`nowrap-overflow: ${wrapOverflow.length}`);

    return {
      vw, vh,
      path: location.pathname,
      issues,
      overflowEls: overflowEls.slice(0, 20),
      smallTargets: smallTargets.slice(0, 25),
      wrapOverflow: wrapOverflow.slice(0, 15),
    };
  };
}

async function loginDemo(page) {
  await page.goto(BASE + '/', { waitUntil: 'domcontentloaded', timeout: 60000 });
  await page.getByRole('button', { name: 'Try the demo' }).click();
  await page.getByText('Your plans').first().waitFor({ timeout: 60000 });
}

async function auditPage(page, label) {
  // allow Blazor to settle
  await page.waitForTimeout(800);
  const result = await page.evaluate(auditScript());
  const file = path.join(OUT, `${label}.json`);
  fs.writeFileSync(file, JSON.stringify(result, null, 2));
  await page.screenshot({ path: path.join(OUT, `${label}.png`), fullPage: false });
  console.log(`\n=== ${label} @ ${result.vw}x${result.vh} ${result.path} ===`);
  if (result.issues.length === 0) {
    console.log('  OK: no issues');
  } else {
    for (const i of result.issues) console.log('  ISSUE: ' + i);
    if (result.overflowEls.length) {
      console.log('  overflow sample:');
      for (const o of result.overflowEls.slice(0, 8)) console.log(`    ${o.tag} .${o.cls} right=${o.right} w=${o.w}`);
    }
    if (result.smallTargets.length) {
      console.log('  small targets sample:');
      for (const t of result.smallTargets.slice(0, 10)) console.log(`    ${t.tag} .${t.cls} ${t.w}x${t.h} "${t.text}"`);
    }
    if (result.wrapOverflow.length) {
      console.log('  nowrap overflow sample:');
      for (const w of result.wrapOverflow.slice(0, 6)) console.log(`    ${w.tag} .${w.cls} scrollW=${w.scrollW} clientW=${w.clientW} "${w.text}"`);
    }
  }
}

(async () => {
  const browser = await chromium.launch();
  try {
    for (const vp of VIEWPORTS) {
      console.log(`\n########## VIEWPORT ${vp.name} ${vp.width}x${vp.height} ##########`);
      const page = await browser.newPage({ viewport: { width: vp.width, height: vp.height } });
      await loginDemo(page);
      await auditPage(page, `${vp.name}-home`);

      // History
      await page.goto(BASE + '/history', { waitUntil: 'domcontentloaded' });
      await auditPage(page, `${vp.name}-history`);

      // Settings
      await page.goto(BASE + '/settings', { waitUntil: 'domcontentloaded' });
      await auditPage(page, `${vp.name}-settings`);

      // Plan editor (demo plan id from screenshot tooling: push day)
      await page.goto(BASE + '/plan/dead0000-0000-4000-8000-000000000001', { waitUntil: 'domcontentloaded' });
      await auditPage(page, `${vp.name}-plan`);

      // Active workout page
      await page.goto(BASE + '/workout/dead0000-0000-4000-8000-000000000001?forceNew=true', { waitUntil: 'domcontentloaded' });
      await auditPage(page, `${vp.name}-workout`);

      await page.close();
    }
  } finally {
    await browser.close();
  }
  console.log('\nDone. Output in ' + OUT);
})();
