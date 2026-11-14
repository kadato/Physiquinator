#!/usr/bin/env node
// Full a11y crawl: every route x light/dark, every dialog, workout flow states.
// Usage: node tools/web-e2e/a11y-full.mjs [--out path]
import fs from 'node:fs';
import { chromium } from '@playwright/test';

const BASE = process.env.BASE_URL ?? 'http://127.0.0.1:5299';
const argIdx = process.argv.indexOf('--out');
const OUT = argIdx >= 0 ? process.argv[argIdx + 1] : '/tmp/opencode/a11y-full.json';

const PUSH_PLAN = 'dead0000-0000-4000-8000-000000000001';
const THEMES = ['light', 'dark'];

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
      if (cs.display === 'none' || cs.visibility === 'hidden' || cs.opacity === '0') return false;
      return true;
    };

    if (!document.documentElement.getAttribute('lang')) push('html-lang', 'html', 'Missing lang', '<html>');
    if (!document.title || !document.title.trim()) push('page-title', 'head>title', 'Missing title', '<title></title>');

    const mains = [...document.querySelectorAll('main')].filter(visible);
    if (mains.length === 0) push('landmark-main', 'body', 'No visible main', 'body');
    if (mains.length > 1) push('landmark-main', 'main', `Multiple mains (${mains.length})`, mains[0].outerHTML);

    const navs = [...document.querySelectorAll('nav')].filter(visible);
    if (navs.length > 1) {
      const unlabeled = navs.filter((n) => !n.getAttribute('aria-label') && !n.getAttribute('aria-labelledby'));
      if (unlabeled.length) push('landmark-nav-label', 'nav', `${unlabeled.length} nav without name`, unlabeled[0].outerHTML.slice(0, 400));
    }

    const skip = document.querySelector('a.skip-link');
    if (!skip) push('skip-link', 'body', 'No skip link', 'body');
    else if (skip.getAttribute('href') !== '#main-content') push('skip-link-target', 'a.skip-link', `Skip targets ${skip.getAttribute('href')}`, skip.outerHTML);

    const inDialog = (el) => !!el.closest('[role="dialog"], dialog, .mud-dialog');
    const headings = [...document.querySelectorAll('h1,h2,h3,h4,h5,h6')].filter(visible);
    const pageHeadings = headings.filter((h) => !inDialog(h));
    const pageH1s = pageHeadings.filter((h) => h.tagName === 'H1');
    if (pageH1s.length === 0) push('heading-h1', 'body', 'No page h1', `found: ${pageHeadings.map((h) => h.tagName).join(',') || 'none'}`);
    if (pageH1s.length > 1) push('heading-h1', 'h1', `Multiple h1 (${pageH1s.length})`, pageH1s.map((h) => h.textContent.trim()).join(' | '));
    let prev = 0;
    for (const h of pageHeadings) {
      const lvl = parseInt(h.tagName[1], 10);
      if (prev !== 0 && lvl > prev + 1) { push('heading-order', h.tagName, `Skipped ${prev} -> ${lvl}: "${h.textContent.trim().slice(0, 80)}"`, h.outerHTML); break; }
      prev = lvl;
    }
    // Dialog headings should start at h1/h2 inside dialog context; just require at least one heading in dialog
    [...document.querySelectorAll('.mud-dialog')].filter(visible).forEach((d) => {
      const dh = [...d.querySelectorAll('h1,h2,h3,h4,h5,h6')].filter(visible);
      if (dh.length === 0) push('dialog-heading', 'dialog', 'Dialog without heading', d.outerHTML.slice(0, 400));
    });

    [...document.querySelectorAll('img')].forEach((img) => { if (!img.hasAttribute('alt')) push('img-alt', 'img', 'Missing alt', img.outerHTML); });
    [...document.querySelectorAll('svg').values()].forEach((svg) => {
      if (!visible(svg)) return;
      if (svg.closest('[aria-hidden="true"]')) return;
      if (svg.closest('[role="img"][aria-label], [role="img"][aria-labelledby]')) return;
      const hidden = svg.getAttribute('aria-hidden') === 'true';
      const hasLabel = svg.getAttribute('aria-label') || svg.getAttribute('aria-labelledby') || svg.querySelector('title');
      const hasRoleImg = svg.getAttribute('role') === 'img';
      const inLabelledControl = svg.closest('button, a[href]') !== null;
      if (!hidden && !hasLabel && !hasRoleImg && !inLabelledControl) push('svg-name', 'svg', 'SVG without name', svg.outerHTML);
    });

    [...document.querySelectorAll('button')].forEach((btn) => {
      if (!visible(btn)) return;
      const text = (btn.innerText || btn.textContent || '').trim();
      const labelled = btn.getAttribute('aria-label') || btn.getAttribute('aria-labelledby') || btn.getAttribute('title') || text.length > 0;
      if (!labelled) push('button-name', 'button', 'Button without name', btn.outerHTML);
    });
    [...document.querySelectorAll('a[href]')].forEach((a) => {
      if (!visible(a)) return;
      const text = (a.innerText || a.textContent || '').trim();
      const labelled = a.getAttribute('aria-label') || a.getAttribute('aria-labelledby') || text.length > 0;
      if (!labelled) push('link-name', 'a', 'Link without name', a.outerHTML);
    });
    [...document.querySelectorAll('input, select, textarea')].forEach((el) => {
      if (el.type === 'hidden') return;
      if (!visible(el)) return;
      const id = el.id;
      const explicit = id && document.querySelector(`label[for="${CSS.escape(id)}"]`);
      const wrapped = el.closest('label');
      if (!explicit && !wrapped && !el.getAttribute('aria-label') && !el.getAttribute('aria-labelledby') && !el.getAttribute('title')) {
        push('form-label', el.tagName.toLowerCase(), `Control without label (type=${el.getAttribute('type') || el.tagName})`, el.outerHTML);
      }
    });
    [...document.querySelectorAll('[aria-hidden="true"]')].forEach((el) => {
      if (el.querySelector('button, a[href], input, select, textarea, [tabindex]:not([tabindex="-1"])')) {
        push('aria-hidden-focus', el.tagName, 'aria-hidden contains focusable', el.outerHTML.slice(0, 400));
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
      if (r.width < 20 || r.height < 20) {
        const label = ((el.innerText || el.getAttribute('aria-label') || el.tagName) || '').trim().slice(0, 60);
        push('target-size', el.tagName.toLowerCase(), `Small target ${Math.round(r.width)}x${Math.round(r.height)} "${label}"`, el.outerHTML.slice(0, 300));
      }
    });

    if (document.documentElement.scrollWidth > document.documentElement.clientWidth + 1) {
      push('layout-overflow', 'html', `Overflow: scrollWidth=${document.documentElement.scrollWidth} clientWidth=${document.documentElement.clientWidth}`, 'html');
    }

    // Dialog semantics: open Mud dialogs must be labelled and modal
    [...document.querySelectorAll('.mud-dialog')].filter(visible).forEach((d) => {
      const container = d.closest('[role="dialog"]') || d;
      const role = (container.getAttribute && container.getAttribute('role')) || d.getAttribute('role');
      // MudBlazor renders role on overlay; require accessible name on dialog title
      const title = d.querySelector('[class*="title"], h1, h2, h3, h4, h5, h6');
      const labelled = d.getAttribute('aria-label') || d.getAttribute('aria-labelledby') || (title && title.textContent.trim().length > 0);
      if (!labelled) push('dialog-name', 'dialog', 'Dialog without accessible name', d.outerHTML.slice(0, 400));
    });

    // Contrast: 4.5 normal, 3.0 large (>=24px or >=18.66px bold)
    const parseColor = (s) => {
      if (!s) return null;
      if (s === 'transparent') return null;
      let m = s.match(/rgba?\(([^)]+)\)/);
      if (!m) return null;
      const p = m[1].split(',').map((x) => parseFloat(x.trim()));
      if (p.length < 3 || p.slice(0, 3).some(isNaN)) return null;
      if (p.length >= 4 && p[3] < 0.99) return null;
      return p.slice(0, 3);
    };
    const lum = (c) => { const s = c / 255; return s <= 0.03928 ? s / 12.92 : Math.pow((s + 0.055) / 1.055, 2.4); };
    const ratio = (f, b) => {
      const L1 = 0.2126 * lum(f[0]) + 0.7152 * lum(f[1]) + 0.0722 * lum(f[2]);
      const L2 = 0.2126 * lum(b[0]) + 0.7152 * lum(b[1]) + 0.0722 * lum(b[2]);
      const [hi, lo] = L1 >= L2 ? [L1, L2] : [L2, L1];
      return (hi + 0.05) / (lo + 0.05);
    };
    const effectiveBg = (el) => {
      let cur = el;
      while (cur && cur !== document.documentElement) {
        const bg = getComputedStyle(cur).backgroundColor;
        const parsed = parseColor(bg);
        if (parsed) return parsed;
        cur = cur.parentElement;
      }
      const htmlBg = parseColor(getComputedStyle(document.documentElement).backgroundColor);
      if (htmlBg) return htmlBg;
      const bodyBg = parseColor(getComputedStyle(document.body).backgroundColor);
      if (bodyBg) return bodyBg;
      // Fall back by theme mode
      const mode = document.documentElement.getAttribute('data-theme-mode');
      return mode === 'light' ? [230, 226, 208] : [14, 15, 23];
    };
    const textEls = [...document.querySelectorAll('body p, body span, body div, body button, body a, body h1, body h2, body h3, body td, body th, body label, body li')].filter(visible).slice(0, 120);
    for (const el of textEls) {
      if (el.closest('[aria-hidden="true"]')) continue;
      const text = (el.childNodes.length === 1 && el.childNodes[0].nodeType === 3) ? el.textContent.trim() : '';
      // Only test leaf text to avoid double counting containers
      const isLeaf = [...el.childNodes].every((n) => n.nodeType === 3 || (n.nodeType === 1 && !visible(n)));
      const t = (el.innerText || el.textContent || '').trim();
      if (!t || t.length > 200) continue;
      if (!isLeaf && el.children.length > 0) continue;
      const cs = getComputedStyle(el);
      const fg = parseColor(cs.color);
      if (!fg) continue;
      const bg = effectiveBg(el);
      if (!bg) continue;
      const r = ratio(fg, bg);
      const fs = parseFloat(cs.fontSize || '16');
      const bold = (parseInt(cs.fontWeight || '400', 10) >= 700) || cs.fontWeight === 'bold';
      const large = fs >= 24 || (fs >= 18.66 && bold);
      const thresh = large ? 3.0 : 4.5;
      if (r < thresh - 0.02) {
        push('contrast', el.tagName.toLowerCase(), `Contrast ${r.toFixed(2)}:1 below ${thresh}:1 for "${t.slice(0, 60)}" (${cs.color} on rgb(${bg.map((v) => Math.round(v)).join(',')}))`, el.outerHTML.slice(0, 300));
        if (out.filter((v) => v.rule === 'contrast').length >= 10) break;
      }
    }

    return out;
  });
}

async function collectAxTreeViolations(page, context) {
  try {
    const cdp = await context.newCDPSession(page);
    await cdp.send('Accessibility.enable');
    const { nodes } = await cdp.send('Accessibility.getFullAXTree');
    const out = [];
    for (const n of nodes) {
      if (n.ignored) continue;
      const role = n.role?.value;
      const name = n.name?.value;
      if (['button', 'link', 'textbox', 'combobox', 'checkbox', 'radio', 'searchbox'].includes(role)) {
        if (!name || !String(name).trim()) out.push({ rule: 'a11y-tree-name', id: role, message: `AX ${role} without name`, snippet: `ax ${n.nodeId} ${role}` });
      }
      if (role === 'heading' && (!name || !String(name).trim())) out.push({ rule: 'a11y-tree-heading-name', id: 'heading', message: 'Empty heading in AX', snippet: `ax ${n.nodeId}` });
    }
    const mains = nodes.filter((n) => !n.ignored && n.role?.value === 'main');
    if (mains.length === 0) out.push({ rule: 'landmark-main-ax', id: 'main', message: 'No main in AX', snippet: 'ax' });
    return out;
  } catch (e) {
    return [{ rule: 'a11y-tree-error', id: 'cdp', message: 'AX failed: ' + String(e).slice(0, 200), snippet: '' }];
  }
}

async function waitForApp(page) {
  try { await page.waitForSelector('main, .auth-card, .app-shell, #main-content', { timeout: 20000 }); } catch {}
  await page.waitForTimeout(3500);
  try {
    const gs = page.getByRole('button', { name: 'Get started' });
    if ((await gs.count()) > 0) await gs.first().click({ timeout: 2000 }).catch(() => {});
  } catch {}
  await page.waitForTimeout(600);
}

async function forceTheme(page, theme) {
  // Preference is per-profile; set all known keys then apply via app JS and reload if needed.
  await page.evaluate((t) => {
    try {
      const keys = [];
      for (let i = 0; i < localStorage.length; i++) { const k = localStorage.key(i); if (k && k.includes('physiquinator-theme-preference')) keys.push(k); }
      keys.forEach((k) => localStorage.setItem(k, t));
      localStorage.setItem('physiquinator-theme-preference', t);
    } catch {}
    try { if (window.physiquinatorTheme) window.physiquinatorTheme.setPreference(t); } catch {}
    try {
      document.documentElement.setAttribute('data-theme', t);
      document.documentElement.setAttribute('data-theme-mode', t === 'light' ? 'light' : 'dark');
    } catch {}
  }, theme);
  await page.waitForTimeout(800);
  const mode = await page.evaluate(() => ({ theme: document.documentElement.getAttribute('data-theme'), mode: document.documentElement.getAttribute('data-theme-mode') }));
  return mode;
}

async function auditPageState(page, context, name, path) {
  const result = { route: name, path, violations: [], warnings: [] };
  try { result.title = await page.title(); } catch { result.title = ''; }
  result.theme = await page.evaluate(() => ({ theme: document.documentElement.getAttribute('data-theme'), mode: document.documentElement.getAttribute('data-theme-mode') }));
  const dom = await collectDomViolations(page);
  result.violations.push(...dom);
  const ax = await collectAxTreeViolations(page, context);
  result.violations.push(...ax.filter((v) => v.rule !== 'landmark-main-ax' || !dom.some((d) => d.rule === 'landmark-main')));
  // Dialog focus check: if dialog open, active element should be inside dialog or overlay
  try {
    const dlg = await page.evaluate(() => {
      const d = [...document.querySelectorAll('.mud-dialog')].find((el) => el.getBoundingClientRect().width > 0);
      if (!d) return null;
      const active = document.activeElement;
      const inside = d.contains(active) || (active && active.closest && !!active.closest('.mud-overlay'));
      return { inside, activeTag: active ? active.tagName : 'none', activeText: (active?.outerHTML || '').slice(0, 200) };
    });
    if (dlg && !dlg.inside) result.warnings.push(`dialog focus outside: ${dlg.activeTag}`);
  } catch {}
  return result;
}

async function main() {
  const browser = await chromium.launch({ headless: true, args: ['--no-sandbox', '--disable-dev-shm-usage'] });
  console.log(`Full crawl ${BASE} themes=${THEMES.join(',')}`);
  // Auth once (light), reuse cookies for both themes
  const authContext = await browser.newContext({ colorScheme: 'light' });
  const authPage = await authContext.newPage();
  let storagePath = null;
  await authPage.goto(BASE + '/', { waitUntil: 'domcontentloaded', timeout: 30000 });
  await waitForApp(authPage);
  // Capture onboarding dialog before dismissing
  let onboardingViolations = [];
  try {
    const gs = authPage.getByRole('button', { name: 'Get started' });
    if ((await gs.count()) > 0) {
      console.log('Auditing onboarding dialog...');
      const dom = await collectDomViolations(authPage);
      onboardingViolations = dom;
      console.log(`onboarding: ${dom.length} violations`);
      dom.slice(0, 10).forEach((v) => console.log(`  - [${v.rule}] ${v.message}`));
    }
  } catch (e) { console.log('onboarding check failed: ' + String(e).slice(0, 200)); }
  const demoBtn = authPage.getByRole('button', { name: /explore demo/i });
  if ((await demoBtn.count()) > 0) {
    await demoBtn.first().click();
    await authPage.getByText('Your plans').first().waitFor({ timeout: 60000 }).catch(() => {});
    await waitForApp(authPage);
    try { const gs = authPage.getByRole('button', { name: 'Get started' }); if ((await gs.count()) > 0) await gs.first().click({ timeout: 3000 }).catch(() => {}); } catch {}
    storagePath = '/tmp/opencode/a11y-storage.json';
    await authContext.storageState({ path: storagePath });
  } else {
    const hasApp = await authPage.evaluate(() => !!document.querySelector('.app-shell'));
    if (hasApp) { storagePath = '/tmp/opencode/a11y-storage.json'; await authContext.storageState({ path: storagePath }); }
  }
  await authContext.close();

  const results = [];
  if (onboardingViolations.length) results.push({ route: 'dialog-onboarding', path: '/', theme: 'light', violations: onboardingViolations, warnings: [], title: 'Onboarding' });

  for (const theme of THEMES) {
    const colorScheme = theme === 'light' ? 'light' : 'dark';
    // Routes
    for (const r of ROUTES) {
      const context = await browser.newContext(storagePath ? { storageState: storagePath, colorScheme } : { colorScheme });
      const page = await context.newPage();
      await page.setViewportSize({ width: 1280, height: 800 });
      try {
        if (r.clientSide) {
          await page.goto(BASE + '/', { waitUntil: 'domcontentloaded', timeout: 30000 });
          await waitForApp(page);
          await forceTheme(page, theme);
          await page.evaluate((p) => { if (window.Blazor && window.Blazor.navigateTo) window.Blazor.navigateTo(p); else location.href = p; }, r.path);
          await page.waitForTimeout(3500);
          await waitForApp(page);
          await forceTheme(page, theme);
        } else if (r.needsAuth === false) {
          await page.goto(BASE + r.path, { waitUntil: 'domcontentloaded', timeout: 30000 });
          await waitForApp(page);
          await forceTheme(page, theme);
          await page.reload({ waitUntil: 'domcontentloaded' }).catch(() => {});
          await waitForApp(page);
          await forceTheme(page, theme);
        } else {
          await page.goto(BASE + r.path, { waitUntil: 'domcontentloaded', timeout: 30000 });
          await waitForApp(page);
          await forceTheme(page, theme);
        }
        const res = await auditPageState(page, context, `${r.name}[${theme}]`, r.path);
        res.themeName = theme;
        results.push(res);
        console.log(`${res.route}: ${res.violations.length} violations theme=${JSON.stringify(res.theme)} title="${res.title}"`);
        res.violations.slice(0, 8).forEach((v) => console.log(`  - [${v.rule}] ${v.message}`));
      } catch (e) {
        results.push({ route: `${r.name}[${theme}]`, path: r.path, violations: [], warnings: [String(e).slice(0, 300)], title: '', themeName: theme });
      }
      await context.close();
    }

    // Dialogs per theme (authenticated)
    const dlgCtx = await browser.newContext(storagePath ? { storageState: storagePath, colorScheme } : { colorScheme });
    const dlgPage = await dlgCtx.newPage();
    await dlgPage.setViewportSize({ width: 1280, height: 800 });
    await dlgPage.goto(BASE + '/', { waitUntil: 'domcontentloaded', timeout: 30000 });
    await waitForApp(dlgPage);
    await forceTheme(dlgPage, theme);

    async function auditDialog(name, openFn, closeFn) {
      try {
        await openFn();
        await dlgPage.waitForTimeout(1200);
        const hasDialog = await dlgPage.evaluate(() => !!document.querySelector('.mud-dialog'));
        if (!hasDialog) return { route: `${name}[${theme}]`, path: 'dialog-not-open', violations: [], warnings: ['dialog did not open'], title: '', themeName: theme };
        const res = await auditPageState(dlgPage, dlgCtx, `${name}[${theme}]`, 'dialog');
        res.themeName = theme;
        console.log(`${res.route}: ${res.violations.length} violations`);
        res.violations.slice(0, 8).forEach((v) => console.log(`  - [${v.rule}] ${v.message}`));
        if (closeFn) await closeFn().catch(() => {});
        else await dlgPage.keyboard.press('Escape').catch(() => {});
        await dlgPage.waitForTimeout(800);
        return res;
      } catch (e) {
        return { route: `${name}[${theme}]`, path: 'dialog-error', violations: [], warnings: [String(e).slice(0, 300)], title: '', themeName: theme };
      }
    }

    // PlanExerciseSheet via /plan New plan Add exercise
    try {
      await dlgPage.goto(BASE + '/plan', { waitUntil: 'domcontentloaded', timeout: 30000 });
      await waitForApp(dlgPage);
      await forceTheme(dlgPage, theme);
      results.push(await auditDialog('dialog-plan-exercise-sheet', async () => {
        const btn = dlgPage.getByRole('button', { name: 'Add exercise' });
        if ((await btn.count()) > 0) await btn.first().click();
        else throw new Error('Add exercise not found');
        await dlgPage.waitForSelector('.mud-dialog', { timeout: 10000 });
      }));
    } catch (e) { results.push({ route: `dialog-plan-exercise-sheet[${theme}]`, path: '/plan', violations: [], warnings: [String(e).slice(0, 200)], title: '', themeName: theme }); }

    // ConfirmDialog via Settings remove all data (cancel after)
    try {
      await dlgPage.goto(BASE + '/settings', { waitUntil: 'domcontentloaded', timeout: 30000 });
      await waitForApp(dlgPage);
      await forceTheme(dlgPage, theme);
      results.push(await auditDialog('dialog-confirm-remove-data', async () => {
        // Expand Remove all data panel if needed and click its button
        const btn = dlgPage.getByRole('button', { name: /remove all data|delete all|erase/i });
        if ((await btn.count()) > 0) await btn.first().click();
        else {
          // Fallback: click any danger button
          const danger = dlgPage.locator('button:has-text("Remove"), button:has-text("Delete")');
          if ((await danger.count()) > 0) await danger.first().click();
          else throw new Error('remove button not found');
        }
        await dlgPage.waitForSelector('.mud-dialog', { timeout: 10000 });
      }));
    } catch (e) { results.push({ route: `dialog-confirm-remove-data[${theme}]`, path: '/settings', violations: [], warnings: [String(e).slice(0, 200)], title: '', themeName: theme }); }

    await dlgCtx.close();

    // Workout flow per theme (fresh workout each time)
    const wCtx = await browser.newContext(storagePath ? { storageState: storagePath, colorScheme } : { colorScheme });
    const wPage = await wCtx.newPage();
    await wPage.setViewportSize({ width: 1280, height: 800 });
    try {
      await wPage.goto(BASE + `/workout/${PUSH_PLAN}?forceNew=true`, { waitUntil: 'domcontentloaded', timeout: 30000 });
      await waitForApp(wPage);
      await forceTheme(wPage, theme);
      // focus state
      let res = await auditPageState(wPage, wCtx, `workout-focus[${theme}]`, 'workout');
      res.themeName = theme; results.push(res);
      console.log(`${res.route}: ${res.violations.length} violations`);
      res.violations.slice(0, 8).forEach((v) => console.log(`  - [${v.rule}] ${v.message}`));

      // menu open
      try {
        const menuBtn = wPage.locator('.workout-focus__menu-btn').first();
        if ((await menuBtn.count()) > 0) {
          await menuBtn.click();
          await wPage.waitForTimeout(1000);
          res = await auditPageState(wPage, wCtx, `workout-menu-open[${theme}]`, 'workout');
          res.themeName = theme; results.push(res);
          console.log(`${res.route}: ${res.violations.length}`);
          res.violations.slice(0, 8).forEach((v) => console.log(`  - [${v.rule}] ${v.message}`));
          await wPage.keyboard.press('Escape').catch(() => {});
          await wPage.waitForTimeout(600);
        }
      } catch (e) { results.push({ route: `workout-menu-open[${theme}]`, path: 'workout', violations: [], warnings: [String(e).slice(0, 200)], title: '', themeName: theme }); }

      // log set -> rest takeover
      try {
        const logBtn = wPage.locator('.workout-focus__log').first();
        if ((await logBtn.count()) > 0) {
          await logBtn.click();
          await wPage.waitForSelector('.workout-rest-takeover', { timeout: 15000 });
          await wPage.waitForTimeout(1000);
          res = await auditPageState(wPage, wCtx, `workout-rest[${theme}]`, 'workout');
          res.themeName = theme; results.push(res);
          console.log(`${res.route}: ${res.violations.length}`);
          res.violations.slice(0, 8).forEach((v) => console.log(`  - [${v.rule}] ${v.message}`));

          // skip rest -> back to focus (times-up path shares components; rest panel already audited)
          const skipBtn = wPage.getByRole('button', { name: 'Skip rest' });
          if ((await skipBtn.count()) > 0) { await skipBtn.first().click(); await wPage.waitForTimeout(1200); }
        }
      } catch (e) { results.push({ route: `workout-rest[${theme}]`, path: 'workout', violations: [], warnings: [String(e).slice(0, 300)], title: '', themeName: theme }); }

      // logged history expanded
      try {
        const summary = wPage.locator('.workout-focus__history-summary').first();
        if ((await summary.count()) > 0) {
          await summary.click();
          await wPage.waitForTimeout(800);
          res = await auditPageState(wPage, wCtx, `workout-history-expanded[${theme}]`, 'workout');
          res.themeName = theme; results.push(res);
          console.log(`${res.route}: ${res.violations.length}`);
        }
      } catch {}

      // end workout -> confirm -> summary dialog
      try {
        const endBtn = wPage.locator('.workout-actions__btn--end').first();
        if ((await endBtn.count()) > 0) {
          await endBtn.click();
          await wPage.waitForSelector('.mud-dialog', { timeout: 10000 });
          res = await auditPageState(wPage, wCtx, `workout-end-confirm[${theme}]`, 'workout');
          res.themeName = theme; results.push(res);
          console.log(`${res.route}: ${res.violations.length}`);
          res.violations.slice(0, 8).forEach((v) => console.log(`  - [${v.rule}] ${v.message}`));
          // Confirm end
          const confirm = wPage.getByRole('button', { name: /^End$/ });
          if ((await confirm.count()) > 0) await confirm.first().click();
          else { const b = wPage.locator('.mud-dialog .mud-button-filled-primary').first(); if ((await b.count()) > 0) await b.click(); }
          await wPage.waitForTimeout(2000);
          const hasSummary = await wPage.evaluate(() => !!document.querySelector('.mud-dialog'));
          if (hasSummary) {
            res = await auditPageState(wPage, wCtx, `workout-summary-dialog[${theme}]`, 'workout');
            res.themeName = theme; results.push(res);
            console.log(`${res.route}: ${res.violations.length}`);
            res.violations.slice(0, 8).forEach((v) => console.log(`  - [${v.rule}] ${v.message}`));
          }
        }
      } catch (e) { results.push({ route: `workout-end-confirm[${theme}]`, path: 'workout', violations: [], warnings: [String(e).slice(0, 300)], title: '', themeName: theme }); }
    } catch (e) {
      results.push({ route: `workout-focus[${theme}]`, path: 'workout', violations: [], warnings: [String(e).slice(0, 300)], title: '', themeName: theme });
    }
    await wCtx.close();

    // HistoryDetail dialogs per theme (need a session id)
    try {
      const hCtx = await browser.newContext(storagePath ? { storageState: storagePath, colorScheme } : { colorScheme });
      const hPage = await hCtx.newPage();
      await hPage.setViewportSize({ width: 1280, height: 800 });
      await hPage.goto(BASE + '/history', { waitUntil: 'domcontentloaded', timeout: 30000 });
      await waitForApp(hPage);
      await forceTheme(hPage, theme);
      // Find first session link
      const href = await hPage.evaluate(() => {
        const a = [...document.querySelectorAll('a[href*="/history/"]')].map((x) => x.getAttribute('href')).find((h) => h && /\/history\/[0-9a-f-]{10,}/i.test(h));
        return a || null;
      });
      if (href) {
        await hPage.goto(BASE + href, { waitUntil: 'domcontentloaded', timeout: 30000 });
        await waitForApp(hPage);
        await forceTheme(hPage, theme);
        let res = await auditPageState(hPage, hCtx, `history-detail[${theme}]`, href);
        res.themeName = theme; results.push(res);
        console.log(`${res.route}: ${res.violations.length}`);
        // AddSetDialog
        try {
          const addBtn = hPage.getByRole('button', { name: 'Add set' });
          if ((await addBtn.count()) > 0) {
            await addBtn.first().click();
            await hPage.waitForSelector('.mud-dialog', { timeout: 10000 });
            await hPage.waitForTimeout(800);
            res = await auditPageState(hPage, hCtx, `dialog-add-set[${theme}]`, href);
            res.themeName = theme; results.push(res);
            console.log(`${res.route}: ${res.violations.length}`);
            res.violations.slice(0, 8).forEach((v) => console.log(`  - [${v.rule}] ${v.message}`));
            await hPage.keyboard.press('Escape').catch(() => {});
            await hPage.waitForTimeout(600);
          }
        } catch (e) { results.push({ route: `dialog-add-set[${theme}]`, path: href, violations: [], warnings: [String(e).slice(0, 200)], title: '', themeName: theme }); }
        // ShareCardOptionsDialog
        try {
          const shareBtn = hPage.getByRole('button', { name: 'Share session' });
          if ((await shareBtn.count()) > 0) {
            await shareBtn.first().click();
            await hPage.waitForSelector('.mud-dialog', { timeout: 10000 });
            await hPage.waitForTimeout(800);
            res = await auditPageState(hPage, hCtx, `dialog-share[${theme}]`, href);
            res.themeName = theme; results.push(res);
            console.log(`${res.route}: ${res.violations.length}`);
            await hPage.keyboard.press('Escape').catch(() => {});
            await hPage.waitForTimeout(600);
          }
        } catch {}
      } else {
        results.push({ route: `history-detail[${theme}]`, path: '/history', violations: [], warnings: ['no session link found'], title: '', themeName: theme });
      }
      await hCtx.close();
    } catch (e) { results.push({ route: `history-detail[${theme}]`, path: '/history', violations: [], warnings: [String(e).slice(0, 200)], title: '', themeName: theme }); }

    // PersonalRecordsDialog via ExerciseProgress PRs button
    try {
      const pCtx = await browser.newContext(storagePath ? { storageState: storagePath, colorScheme } : { colorScheme });
      const pPage = await pCtx.newPage();
      await pPage.setViewportSize({ width: 1280, height: 800 });
      await pPage.goto(BASE + `/history/exercise-progress/${PUSH_PLAN}/Bench%20Press`, { waitUntil: 'domcontentloaded', timeout: 30000 });
      await waitForApp(pPage);
      await forceTheme(pPage, theme);
      const prBtn = pPage.getByRole('button', { name: 'PRs' });
      if ((await prBtn.count()) > 0) {
        await prBtn.first().click();
        await pPage.waitForSelector('.mud-dialog', { timeout: 10000 });
        await pPage.waitForTimeout(800);
        const res = await auditPageState(pPage, pCtx, `dialog-personal-records[${theme}]`, 'exercise-progress');
        res.themeName = theme; results.push(res);
        console.log(`${res.route}: ${res.violations.length}`);
        res.violations.slice(0, 8).forEach((v) => console.log(`  - [${v.rule}] ${v.message}`));
        await pPage.keyboard.press('Escape').catch(() => {});
      } else {
        results.push({ route: `dialog-personal-records[${theme}]`, path: 'exercise-progress', violations: [], warnings: ['PRs button not found (no data?)'], title: '', themeName: theme });
      }
      await pCtx.close();
    } catch (e) { results.push({ route: `dialog-personal-records[${theme}]`, path: 'exercise-progress', violations: [], warnings: [String(e).slice(0, 200)], title: '', themeName: theme }); }
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
  console.log('\n=== GROUPED BY RULE ===');
  for (const [rule, items] of [...byRule.entries()].sort((a, b) => b[1].length - a[1].length)) {
    const routes = [...new Set(items.map((i) => i.route))];
    console.log(`${rule}: ${items.length} hits across [${routes.join(', ')}]`);
  }
}

main().catch((e) => { console.error(e); process.exit(1); });
