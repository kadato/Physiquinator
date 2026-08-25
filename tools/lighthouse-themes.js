#!/usr/bin/env node
// Dual-theme Lighthouse auditor: runs every page in both dark and light modes.
const { execSync } = require('node:child_process');
const fs = require('node:fs');
const nodePath = require('node:path');

const BASE_URL = process.env.LH_BASE_URL ?? 'http://127.0.0.1:5149';
const OUTPUT_DIR = process.env.LH_OUTPUT_DIR ?? nodePath.join(__dirname, '..', 'artifacts', 'lighthouse-themes');

const PAGES = [
  { name: 'home', path: '/' },
  { name: 'settings', path: '/settings' },
  { name: 'history', path: '/history' },
  { name: 'history-detail', path: '/history/efbeadde-0000-0000-6e00-000000000000' },
  { name: 'bodyweight', path: '/history/bodyweight' },
  { name: 'exercise-progress', path: '/history/exercise-progress/dead0000-0000-4000-8000-000000000001/Bench%20Press' },
  { name: 'plan-editor', path: '/plan/dead0000-0000-4000-8000-000000000001' },
  { name: 'workout', path: '/workout/dead0000-0000-4000-8000-000000000001?forceNew=true' },
  { name: 'ai', path: '/ai' },
  { name: 'error', path: '/Error' },
];

const THEMES = [
  { name: 'dark', flag: '--force-prefers-color-scheme=dark' },
  { name: 'light', flag: '--force-prefers-color-scheme=light' },
];

function extractScores(report) {
  const cats = report.categories;
  return {
    performance: Math.round(cats.performance.score * 100),
    accessibility: Math.round(cats.accessibility.score * 100),
    bestPractices: Math.round(cats['best-practices'].score * 100),
    seo: Math.round(cats.seo.score * 100),
  };
}

function runLighthouse(url, outputPath, chromeFlags) {
  const cmd = `lighthouse "${url}" --output=json --output-path="${outputPath}" --chrome-flags="${chromeFlags}" --only-categories=performance,accessibility,best-practices,seo --quiet`;
  execSync(cmd, { stdio: 'pipe', timeout: 120000 });
}

function auditPage(pg, theme) {
  const tag = `${pg.name}-${theme.name}`;
  const url = `${BASE_URL}${pg.path}`;
  const jsonPath = nodePath.join(OUTPUT_DIR, `${tag}.json`);

  process.stdout.write(`  ${tag}... `);

  const chromeFlags = ['--headless=new', '--no-sandbox', '--disable-gpu', '--disable-dev-shm-usage', theme.flag].join(' ');

  try {
    runLighthouse(url, jsonPath, chromeFlags);
  } catch {
    console.log('FAILED');
    return null;
  }

  if (!fs.existsSync(jsonPath)) {
    console.log('NO REPORT');
    return null;
  }

  const report = JSON.parse(fs.readFileSync(jsonPath, 'utf8'));
  const scores = extractScores(report);
  const bgColor = report.audits['background-color']?.details?.items?.[0]?.value || '?';

  console.log(`Perf=${scores.performance} A11y=${scores.accessibility} BP=${scores.bestPractices} SEO=${scores.seo} bg=${bgColor}`);
  return { tag, name: pg.name, theme: theme.name, scores, bgColor };
}

function formatThemeResult(result) {
  if (!result) return 'N/A';
  return `P:${result.scores.performance} A:${result.scores.accessibility} bg:${result.bgColor}`;
}

function formatDelta(dark, light) {
  if (!dark || !light) return '';
  const delta = light.scores.performance - dark.scores.performance;
  if (delta > 0) return `+${delta}`;
  if (delta === 0) return '=';
  return `${delta}`;
}

function groupResultsByPage(results) {
  const byPage = new Map();
  for (const r of results) {
    if (!byPage.has(r.name)) byPage.set(r.name, {});
    byPage.get(r.name)[r.theme] = r;
  }
  return byPage;
}

function printComparison(results) {
  console.log('\n\n========== DARK vs LIGHT COMPARISON ==========');
  console.log('Page'.padEnd(22) + 'Dark'.padEnd(20) + 'Light'.padEnd(20) + 'Perf Delta');
  console.log('-'.repeat(70));

  const byPage = groupResultsByPage(results);
  for (const [name, themes] of byPage) {
    const dStr = formatThemeResult(themes.dark);
    const lStr = formatThemeResult(themes.light);
    const arrow = formatDelta(themes.dark, themes.light);
    console.log(`${name.padEnd(22)}${dStr.padEnd(20)}${lStr.padEnd(20)}${arrow}`);
  }
}

function checkAccessibilityDifferences(results) {
  console.log('\n========== ACCESSIBILITY DIFFERENCES ==========');
  const byPage = groupResultsByPage(results);

  let allSame = true;
  for (const [name, themes] of byPage) {
    const d = themes.dark;
    const l = themes.light;
    if (d && l && d.scores.accessibility !== l.scores.accessibility) {
      console.log(`${name}: dark=${d.scores.accessibility} light=${l.scores.accessibility} - THEME-SPECIFIC A11Y ISSUE!`);
      allSame = false;
    }
  }
  if (allSame) {
    console.log('No accessibility differences between dark and light modes. Both score 100.');
  }
}

function main() {
  fs.mkdirSync(OUTPUT_DIR, { recursive: true });
  const results = [];

  for (const theme of THEMES) {
    console.log(`\n=== ${theme.name.toUpperCase()} MODE ===`);
    for (const pg of PAGES) {
      const result = auditPage(pg, theme);
      if (result) results.push(result);
    }
  }

  printComparison(results);
  checkAccessibilityDifferences(results);
  console.log(`\nFull reports saved to ${OUTPUT_DIR}`);
}

main();
