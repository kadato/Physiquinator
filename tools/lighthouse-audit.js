#!/usr/bin/env node
// Lighthouse audit runner for Physiquinator Web
const { execSync } = require('node:child_process');
const fs = require('node:fs');
const nodePath = require('node:path');

const BASE_URL = 'http://localhost:5200';
const OUTPUT_DIR = nodePath.join(__dirname, '..', 'artifacts', 'lighthouse');
const PAGES = [
  { name: 'home', path: '/' },
  { name: 'history', path: '/history' },
  { name: 'settings', path: '/settings' },
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

function collectIssues(report) {
  const failures = [];
  const warnings = [];
  for (const audit of Object.values(report.audits)) {
    if (audit.score === null || audit.score >= 1 || audit.scoreDisplayMode === 'notApplicable') continue;
    const item = { id: audit.id, title: audit.title, value: audit.displayValue || '' };
    if (audit.score === 0) failures.push(item);
    else warnings.push(item);
  }
  return { failures, warnings };
}

function runLighthouse(url, outputPath) {
  const flags = ['--headless', '--no-sandbox', '--disable-gpu', '--disable-dev-shm-usage'].join(' ');
  const cmd = `lighthouse "${url}" --output=json --output-path="${outputPath}" --chrome-flags="${flags}" --only-categories=performance,accessibility,best-practices,seo --quiet`;
  execSync(cmd, { stdio: 'pipe', timeout: 120000 });
}

async function runAudit(page) {
  const jsonPath = nodePath.join(OUTPUT_DIR, `${page.name}.json`);
  const url = `${BASE_URL}${page.path}`;

  console.log(`\n>>> Auditing ${page.name}: ${url}`);
  try {
    runLighthouse(url, jsonPath);
  } catch (e) {
    console.error(`  Failed: ${e.message?.substring(0, 200)}`);
    return null;
  }

  if (!fs.existsSync(jsonPath)) {
    console.error(`  Report not found at ${jsonPath}`);
    return null;
  }

  const report = JSON.parse(fs.readFileSync(jsonPath, 'utf8'));
  const scores = extractScores(report);
  const { failures, warnings } = collectIssues(report);

  console.log(`  Perf: ${scores.performance} | A11y: ${scores.accessibility} | BP: ${scores.bestPractices} | SEO: ${scores.seo}`);
  failures.forEach(f => console.log(`  FAIL: ${f.title}: ${f.value}`));
  warnings.forEach(w => console.log(`  WARN: ${w.title}: ${w.value}`));

  return { name: page.name, scores, failures, warnings };
}

function printSummary(results) {
  console.log('\n\n========== SUMMARY ==========');
  console.log('Page'.padEnd(25) + 'Perf'.padEnd(8) + 'A11y'.padEnd(8) + 'BP'.padEnd(8) + 'SEO');
  console.log('-'.repeat(50));
  for (const r of results) {
    const label = r.name === 'home' ? '/' : r.name;
    console.log(
      label.padEnd(25) +
      String(r.scores.performance).padEnd(8) +
      String(r.scores.accessibility).padEnd(8) +
      String(r.scores.bestPractices).padEnd(8) +
      String(r.scores.seo)
    );
  }
}

function aggregateIssues(results, key) {
  const map = new Map();
  for (const r of results) {
    for (const item of r[key]) {
      if (!map.has(item.id)) map.set(item.id, { ...item, count: 0, pages: [] });
      const entry = map.get(item.id);
      entry.count++;
      entry.pages.push(r.name);
    }
  }
  return map;
}

async function main() {
  fs.mkdirSync(OUTPUT_DIR, { recursive: true });

  const results = [];
  for (const page of PAGES) {
    const result = await runAudit(page);
    if (result) results.push(result);
  }

  printSummary(results);

  for (const [label, key] of [['FAILURES', 'failures'], ['WARNINGS', 'warnings']]) {
    const aggregated = aggregateIssues(results, key);
    if (aggregated.size === 0) continue;
    console.log(`\n\n========== AGGREGATED ${label} ==========`);
    for (const [, item] of aggregated) {
      console.log(`\n[${item.count}x] ${item.title}: ${item.value}`);
      console.log(`  Pages: ${item.pages.join(', ')}`);
    }
  }

  const summary = { results: results.map(r => ({ name: r.name, scores: r.scores, failures: r.failures, warnings: r.warnings })) };
  fs.writeFileSync(nodePath.join(OUTPUT_DIR, 'summary.json'), JSON.stringify(summary, null, 2));
  console.log(`\nFull reports saved to ${OUTPUT_DIR}`);
}

try {
  await main();
} catch (e) {
  console.error(e);
  process.exit(1);
}
