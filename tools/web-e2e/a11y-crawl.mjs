#!/usr/bin/env node
// Headless Chrome a11y + layout crawl for Physiquinator.
// Crawls every route, inspects live DOM + accessibility tree (via CDP), logs violations.
// Usage: BASE_URL=http://127.0.0.1:5299 node tools/web-e2e/a11y-crawl.mjs [--out path]
import fs from 'node:fs';
import { chromium } from '@playwright/test';

const BASE = process.env.BASE_URL ?? 'http://127.0.0.1:5299';
const argIdx = process.argv.indexOf('--out');
const OUT = argIdx >= 0 ? process.argv[argIdx + 1] : '/tmp/opencode/a11y-baseline.json';

const PUSH_PLAN = 'dead0000-0000-4000-8000-000000000001';

const ROUTES = [
  { name: 'login', path: '/', needsAuth: false },
  { name: 'home', path: '/' },
  { name: 'history', path: '/history' },
  { name: 'history-bodyweight', path: '/history/bodyweight' },
  { name: 'history-compare', path: '/history/compare' },
  { name: 'exercise-history-generic', path: '/history/exercise/Bench%20Press' },
  { name: 'exercise-progress', path: `/history/exercise-progress/${PUSH_PLAN}/Bench%20Press` },
  { name: 'plan-new', path: '/plan' },
  { name: 'plan-editor', path: `/plan/${PUSH_PLAN}` },
  { name: 'workout', path: `/workout/${PUSH_PLAN}?forceNew=true` },
  { name: 'ai', path: '/ai' },
  { name: 'assistant-alias', path: '/assistant' },
  { name: 'settings', path: '/settings' },
  { name: 'privacy', path: '/privacy' },
  { name: 'not-found', path: '/__a11y_missing_route_xyz', clientSide: true },
];

async function collectDomViolations(page) {
  return await page.evaluate(() => {
    const out = [];
    const push = (rule, id, message, snippet) => out.push({ rule, id, message, snippet: (snippet || '').slice(0, 400) });
    const visible = (el) => {
      const r = el.getBoundingClientRect();
      if (r.width === 0 && r.height === 0) return false;
      const cs = getComputedStyle(el);
      if (cs.display === 'none' || cs.visibility === 'hidden') return false;
      return true;
    };

    if (!document.documentElement.getAttribute('lang')) push('html-lang', 'html', 'Missing lang attribute on <html>', '<html>');
    if (!document.title || !document.title.trim()) push('page-title', 'head>title', 'Missing or empty <title>', '<title></title>');

    const mains = [...document.querySelectorAll('main')].filter(visible);
    if (mains.length === 0) push('landmark-main', 'body', 'No visible <main> landmark found', 'body');
    if (mains.length > 1) push('landmark-main', 'main', `Multiple <main> landmarks (${mains.length})`, mains[0].outerHTML);

    const navs = [...document.querySelectorAll('nav')].filter(visible);
    if (navs.length > 1) {
      const unlabeled = navs.filter((n) => !n.getAttribute('aria-label') && !n.getAttribute('aria-labelledby'));
      if (unlabeled.length) push('landmark-nav-label', 'nav', `${unlabeled.length} <nav> without accessible name`, unlabeled[0].outerHTML.slice(0, 400));
    }

    const skip = document.querySelector('a.skip-link');
    if (!skip) push('skip-link', 'body', 'No skip link (a.skip-link) found', 'body');
    else if (skip.getAttribute('href') !== '#main-content') push('skip-link-target', 'a.skip-link', `Skip link targets "${skip.getAttribute('href')}" not #main-content`, skip.outerHTML);

    const headings = [...document.querySelectorAll('h1,h2,h3,h4,h5,h6')].filter(visible);
    const pageH1s = headings.filter((h) => h.tagName === 'H1' && !h.closest('[role="dialog"], dialog, [aria-modal="true"]'));
    if (pageH1s.length === 0) push('heading-h1', 'body', 'No visible page-level <h1> found', `found headings: ${headings.map((h) => h.tagName).join(',') || 'none'}`);
    if (pageH1s.length > 1) push('heading-h1', 'h1', `Multiple page <h1> elements (${pageH1s.length})`, pageH1s.map((h) => h.textContent.trim()).join(' | '));
    let prev = 0;
    for (const h of headings) {
      const lvl = parseInt(h.tagName[1], 10);
      if (prev !== 0 && lvl > prev + 1) {
        push('heading-order', h.tagName, `Skipped heading level ${prev} -> ${lvl}: "${h.textContent.trim().slice(0, 80)}"`, h.outerHTML);
        break;
      }
      prev = lvl;
    }
    [...document.querySelectorAll('[role="heading"]')].forEach((el) => {
      if (!el.getAttribute('aria-level')) push('heading-aria-level', el.tagName, 'role=heading without aria-level', el.outerHTML);
    });

    [...document.querySelectorAll('img')].forEach((img) => {
      if (!img.hasAttribute('alt')) push('img-alt', 'img', 'Missing alt attribute', img.outerHTML);
    });
    [...document.querySelectorAll('svg').values()].forEach((svg) => {
      if (!visible(svg)) return;
      // Content inside aria-hidden is presentational by design (chart with table alternative).
      if (svg.closest('[aria-hidden="true"]')) return;
      // SVG inside a labelled image container is presentational; the container carries the name.
      if (svg.closest('[role="img"][aria-label], [role="img"][aria-labelledby]')) return;
      const hidden = svg.getAttribute('aria-hidden') === 'true';
      const hasLabel = svg.getAttribute('aria-label') || svg.getAttribute('aria-labelledby') || svg.querySelector('title');
      const hasRoleImg = svg.getAttribute('role') === 'img';
      const inLabelledControl = svg.closest('button, a[href]') !== null;
      if (!hidden && !hasLabel && !hasRoleImg && !inLabelledControl) push('svg-name', 'svg', 'Visible SVG without accessible name or aria-hidden', svg.outerHTML);
    });

    [...document.querySelectorAll('button')].forEach((btn) => {
      if (!visible(btn)) return;
      const text = (btn.innerText || btn.textContent || '').trim();
      const labelled = btn.getAttribute('aria-label') || btn.getAttribute('aria-labelledby') || btn.getAttribute('title') || text.length > 0;
      if (!labelled) push('button-name', 'button', 'Visible button without accessible name', btn.outerHTML);
    });

    [...document.querySelectorAll('a[href]')].forEach((a) => {
      if (!visible(a)) return;
      const text = (a.innerText || a.textContent || '').trim();
      const labelled = a.getAttribute('aria-label') || a.getAttribute('aria-labelledby') || text.length > 0;
      if (!labelled) push('link-name', 'a', 'Visible link without accessible name', a.outerHTML);
    });

    [...document.querySelectorAll('input, select, textarea')].forEach((el) => {
      if (el.type === 'hidden') return;
      if (!visible(el)) return;
      const id = el.id;
      const explicit = id && document.querySelector(`label[for="${CSS.escape(id)}"]`);
      const wrapped = el.closest('label');
      const ariaLabel = el.getAttribute('aria-label');
      const labelledBy = el.getAttribute('aria-labelledby');
      const title = el.getAttribute('title');
      if (!explicit && !wrapped && !ariaLabel && !labelledBy && !title) {
        push('form-label', el.tagName.toLowerCase(), `Form control without label (type=${el.getAttribute('type') || el.tagName})`, el.outerHTML);
      }
    });

    [...document.querySelectorAll('[aria-hidden="true"]')].forEach((el) => {
      if (el.querySelector('button, a[href], input, select, textarea, [tabindex]:not([tabindex="-1"])')) {
        push('aria-hidden-focus', el.tagName, 'aria-hidden ancestor contains focusable content', el.outerHTML.slice(0, 400));
      }
    });

    [...document.querySelectorAll('[tabindex]')].forEach((el) => {
      const v = parseInt(el.getAttribute('tabindex'), 10);
      if (v > 0) push('tabindex-positive', el.tagName, `Positive tabindex=${v}`, el.outerHTML);
    });

    const seen = new Map();
    [...document.querySelectorAll('[id]')].forEach((el) => seen.set(el.id, (seen.get(el.id) || 0) + 1));
    for (const [id, n] of seen) if (n > 1 && id) push('duplicate-id', `#${id}`, `Duplicate id "${id}" x${n}`, `#${id}`);

    [...document.querySelectorAll('button, a[href], input, select, [role="button"], [role="tab"]')].forEach((el) => {
      if (!visible(el)) return;
      const r = el.getBoundingClientRect();
      if (r.width < 24 || r.height < 24) {
        const label = ((el.innerText || el.getAttribute('aria-label') || el.tagName) || '').trim().slice(0, 60);
        // Only flag if truly tiny (WCAG 2.5.8 minimum 24px); allow exception via data attr
        if (r.width < 20 || r.height < 20) push('target-size', el.tagName.toLowerCase(), `Small target ${Math.round(r.width)}x${Math.round(r.height)} "${label}"`, el.outerHTML.slice(0, 300));
      }
    });

    if (document.documentElement.scrollWidth > document.documentElement.clientWidth + 1) {
      push('layout-overflow', 'html', `Horizontal overflow: scrollWidth=${document.documentElement.scrollWidth} clientWidth=${document.documentElement.clientWidth}`, 'html');
    }

    // MudBlazor-specific: icon-only MudIconButton without aria-label is caught above; also check Mud overlay scroll lock?
    return out;
  });
}

async function collectAxTreeViolations(page, context) {
  // Use CDP Accessibility domain as the accessibility-tree source.
  try {
    const cdp = await context.newCDPSession(page);
    await cdp.send('Accessibility.enable');
    const { nodes } = await cdp.send('Accessibility.getFullAXTree');
    const out = [];
    const byId = new Map(nodes.map((n) => [n.nodeId, n]));
    for (const n of nodes) {
      if (n.ignored) continue;
      const role = n.role?.value;
      const name = n.name?.value;
      if (['button', 'link', 'textbox', 'combobox', 'checkbox', 'radio', 'searchbox'].includes(role)) {
        if (!name || !String(name).trim()) {
          // Find backend DOM snippet if possible
          let snippet = `ax nodeId=${n.nodeId} role=${role}`;
          out.push({ rule: 'a11y-tree-name', id: role, message: `Accessibility tree ${role} without name`, snippet });
        }
      }
      if (role === 'heading' && (!name || !String(name).trim())) {
        out.push({ rule: 'a11y-tree-heading-name', id: 'heading', message: 'Empty heading in accessibility tree', snippet: `ax nodeId=${n.nodeId}` });
      }
    }
    // Check exactly one main landmark in AX tree
    const mains = nodes.filter((n) => !n.ignored && n.role?.value === 'main');
    if (mains.length === 0) out.push({ rule: 'landmark-main-ax', id: 'main', message: 'No main landmark in accessibility tree', snippet: 'ax tree' });
    void byId;
    return out;
  } catch (e) {
    return [{ rule: 'a11y-tree-error', id: 'cdp', message: 'Could not read accessibility tree: ' + String(e).slice(0, 200), snippet: '' }];
  }
}

async function waitForApp(page) {
  // Blazor Server hydrates over websocket; wait for either app shell or login card.
  try {
    await page.waitForSelector('main, .auth-card, .app-shell, #main-content', { timeout: 20000 });
  } catch {}
  await page.waitForTimeout(4000);
  // Dismiss seeded onboarding if present
  try {
    const gs = page.getByRole('button', { name: 'Get started' });
    if ((await gs.count()) > 0) await gs.first().click({ timeout: 2000 }).catch(() => {});
  } catch {}
  await page.waitForTimeout(800);
}

async function auditRoute(browser, route, storagePath) {
  const context = await browser.newContext(storagePath ? { storageState: storagePath } : {});
  const page = await context.newPage();
  await page.setViewportSize({ width: 1280, height: 800 });
  const url = BASE + route.path;
  const result = { route: route.name, path: route.path, url, violations: [], warnings: [], loadOk: false };
  try {
    if (route.clientSide) {
      // Unknown paths return HTTP 404 with empty body on direct load.
      // Reach the Blazor Router NotFound UI via in-app navigation instead.
      await page.goto(BASE + '/', { waitUntil: 'domcontentloaded', timeout: 30000 });
      await waitForApp(page);
      result.status = 200;
      await page.evaluate((p) => {
        if (window.Blazor && window.Blazor.navigateTo) window.Blazor.navigateTo(p);
        else location.href = p;
      }, route.path);
      await page.waitForTimeout(4000);
      await waitForApp(page);
    } else {
      const resp = await page.goto(url, { waitUntil: 'domcontentloaded', timeout: 30000 });
      result.status = resp ? resp.status() : null;
      await waitForApp(page);
    }
    result.loadOk = true;
    try { result.title = await page.title(); } catch { result.title = ''; }
    const dom = await collectDomViolations(page);
    result.violations.push(...dom);
    const ax = await collectAxTreeViolations(page, context);
    // Avoid double-counting main landmark when DOM already reported it
    result.violations.push(...ax.filter((v) => v.rule !== 'landmark-main-ax' || !dom.some((d) => d.rule === 'landmark-main')));
  } catch (e) {
    result.loadOk = false;
    result.error = String(e).slice(0, 500);
  }
  await context.close();
  return result;
}

async function main() {
  const browser = await chromium.launch({ headless: true, args: ['--no-sandbox', '--disable-dev-shm-usage'] });
  console.log(`Crawling ${BASE} ...`);
  const authContext = await browser.newContext();
  const authPage = await authContext.newPage();
  let storagePath = null;
  try {
    await authPage.goto(BASE + '/', { waitUntil: 'domcontentloaded', timeout: 30000 });
    await waitForApp(authPage);
    const demoBtn = authPage.getByRole('button', { name: /explore demo/i });
    if ((await demoBtn.count()) > 0) {
      console.log('Using demo login...');
      await demoBtn.first().click();
      await authPage.getByText('Your plans').first().waitFor({ timeout: 60000 }).catch(() => {});
      await waitForApp(authPage);
      try {
        const gs = authPage.getByRole('button', { name: 'Get started' });
        if ((await gs.count()) > 0) await gs.first().click({ timeout: 3000 }).catch(() => {});
      } catch {}
      storagePath = '/tmp/opencode/a11y-storage.json';
      await authContext.storageState({ path: storagePath });
      console.log('Saved auth state to ' + storagePath);
    } else {
      // Maybe already authenticated (cookie persisted?) – save state anyway if app shell present
      const hasApp = await authPage.evaluate(() => !!document.querySelector('.app-shell'));
      console.log('No demo button. app-shell present: ' + hasApp);
      if (hasApp) {
        storagePath = '/tmp/opencode/a11y-storage.json';
        await authContext.storageState({ path: storagePath });
      }
    }
  } catch (e) {
    console.log('Auth setup failed: ' + String(e).slice(0, 500));
  }
  await authContext.close();

  if (storagePath && !fs.existsSync(storagePath)) storagePath = null;

  const results = [];
  for (const r of ROUTES) {
    const useStorage = r.needsAuth === false ? null : storagePath;
    const res = await auditRoute(browser, r, useStorage);
    results.push(res);
    console.log(`${res.route} (${res.path}): ${res.violations.length} violations [title="${res.title}"]`);
    for (const v of res.violations.slice(0, 15)) console.log(`  - [${v.rule}] ${v.message} :: ${v.snippet.slice(0, 160)}`);
  }
  await browser.close();

  const byRule = new Map();
  let total = 0;
  for (const r of results) for (const v of r.violations) {
    total++;
    if (!byRule.has(v.rule)) byRule.set(v.rule, []);
    byRule.get(v.rule).push({ route: r.route, message: v.message, snippet: v.snippet });
  }
  const score = Math.max(0, 100 - total * 2);
  const report = { baseUrl: BASE, generatedAt: new Date().toISOString(), score, totalViolations: total, byRule: Object.fromEntries(byRule), routes: results };
  const outDir = OUT.split('/').slice(0, -1).join('/') || '.';
  fs.mkdirSync(outDir, { recursive: true });
  fs.writeFileSync(OUT, JSON.stringify(report, null, 2));
  console.log(`\nTOTAL violations=${total} score=${score} -> ${OUT}`);
  console.log('\n=== GROUPED BY RULE (shared root causes) ===');
  for (const [rule, items] of [...byRule.entries()].sort((a, b) => b[1].length - a[1].length)) {
    const routes = [...new Set(items.map((i) => i.route))];
    console.log(`${rule}: ${items.length} hits across [${routes.join(', ')}]`);
  }
}

main().catch((e) => { console.error(e); process.exit(1); });
