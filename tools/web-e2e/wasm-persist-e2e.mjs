// App-level persistence proof for the WebAssembly host.
// 1. Boot the published app, create a uniquely named plan through the real UI
// 2. Wait for the autosave loop to push database bytes into Cache Storage
// 3. Hard-reload the page
// 4. The plan must still exist, restored from Cache Storage SQLite
import { chromium } from 'playwright';

const base = process.env.WASM_URL ?? 'http://localhost:5090/';
const PLAN_NAME = `PERSIST CHECK ${Date.now() % 100000}`;
const browser = await chromium.launch();
const page = await browser.newPage();

const errors = [];
page.on('pageerror', (err) => errors.push(`[pageerror] ${err.message}`));
page.on('console', (msg) => {
    const t = msg.text();
    if (t.includes('[persist]')) {
        console.log(t.slice(0, 300));
    }
});

await page.goto(base, { waitUntil: 'domcontentloaded' });
await page.waitForFunction(
    () => document.querySelector('#app') && document.querySelector('#app').innerText.length > 200,
    null,
    { timeout: 180_000 }
);

// Dismiss the onboarding dialog if present this run.
const getStarted = page.getByRole('button', { name: /get started/i });
if (await getStarted.count()) {
    await getStarted.first().click();
    await page.waitForTimeout(1_000);
}

// Create the plan through the real UI flow.
await page.getByRole('button', { name: /new plan/i }).first().click();
await page.waitForURL(/\/plan/, { timeout: 60_000 });
await page.getByLabel('Plan name').fill(PLAN_NAME);
await page.locator('#new-exercise-input').fill('Squat');
await page.getByRole('button', { name: 'Add', exact: true }).click();
await page.waitForTimeout(800);

// Adding an exercise can open a bottom-sheet editor; close whatever dialog
// is covering the page so the Save FAB becomes clickable.
for (let i = 0; i < 4; i++) {
    const dialogOpen = await page.locator('.mud-dialog-container').count();
    if (!dialogOpen) {
        break;
    }
    console.log(`dialog present (attempt ${i + 1}), dismissing`);
    const closeBtn = page.locator('.mud-dialog-container [aria-label*="lose" i]').first();
    if (await closeBtn.count()) {
        await closeBtn.click().catch(() => {});
    } else {
        await page.mouse.click(20, 20);
        await page.keyboard.press('Escape').catch(() => {});
    }
    await page.waitForTimeout(900);
}

await page.locator('[aria-label="Save plan"]').click({ timeout: 30_000 });
await page.waitForURL(/\/(home)?$/i, { timeout: 60_000 });

const createdVisible = await page.getByText(PLAN_NAME, { exact: false }).first().isVisible().catch(() => false);
console.log(`created plan visible after save: ${createdVisible}`);
if (!createdVisible) {
    console.log('FAIL: created plan not visible on home');
    await browser.close();
    process.exit(1);
}

// Wait for the autosave tick (>20s interval), then confirm Cache Storage holds a db.
console.log('waiting 24s for autosave...');
await page.waitForTimeout(24_000);
const cacheKeys = await page.evaluate(async () => {
    const cache = await caches.open('physiquinator-db');
    return (await cache.keys()).map((r) => decodeURIComponent(r.url));
});
console.log(`cache storage entries: ${JSON.stringify(cacheKeys.map((u) => u.slice(u.lastIndexOf('/') + 1)))}`);

// Hard reload: everything must come back from Cache Storage.
await page.reload({ waitUntil: 'domcontentloaded' });
await page.waitForFunction(
    () => document.querySelector('#app') && document.querySelector('#app').innerText.includes('Your plans'),
    null,
    { timeout: 180_000 }
);

const postReloadText = await page.evaluate(() => document.querySelector('#app').innerText.replace(/\n{2,}/g, '\n'));
console.log('=== POST-RELOAD APP TEXT (first 700) ===');
console.log(postReloadText.slice(0, 700));

const survived = await page.getByText(PLAN_NAME, { exact: false }).first().isVisible().catch(() => false);
console.log(`plan visible after reload: ${survived}`);

// Byte-level probe: does the cached database actually contain the plan name?
const probe = await page.evaluate(async ([planName]) => {
    const cache = await caches.open('physiquinator-db');
    const keys = await cache.keys();
    for (const request of keys) {
        const resp = await cache.match(request);
        const buf = new Uint8Array(await resp.arrayBuffer());
        // UTF-8/ASCII scan for the plan name inside the sqlite file
        const needle = Array.from(planName).map((c) => c.charCodeAt(0));
        let found = false;
        outer: for (let i = 0; i <= buf.length - needle.length; i++) {
            for (let j = 0; j < needle.length; j++) {
                if (buf[i + j] !== needle[j]) {
                    continue outer;
                }
            }
            found = true;
            break;
        }
        const url = String(request.url ?? request);
        return { file: url.slice(url.lastIndexOf('/') + 1), size: buf.length, containsPlan: found };
    }
    return null;
}, [PLAN_NAME]);
console.log(`cache blob probe: ${JSON.stringify(probe)}`);

for (const e of errors.slice(0, 5)) {
    console.log(e);
}
await browser.close();

if (survived && cacheKeys.some((k) => k.endsWith('.db3'))) {
    console.log(`PERSISTENCE PASS: "${PLAN_NAME}" survived a full page reload`);
    process.exit(0);
} else {
    console.log('PERSISTENCE FAIL');
    process.exit(1);
}