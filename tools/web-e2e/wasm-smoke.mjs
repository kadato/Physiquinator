// End-to-end smoke test for the Physiquinator WebAssembly host, verbose edition.
import { chromium } from 'playwright';

const base = process.env.WASM_URL ?? 'http://localhost:5080/';
const browser = await chromium.launch();
const page = await browser.newPage();

const errors = [];
page.on('pageerror', (err) => errors.push(`[pageerror] ${err.message}\n  ${String(err.stack ?? '').slice(0, 400)}`));
page.on('console', async (msg) => {
    let text = msg.text();
    try {
        const args = msg.args();
        if (args.length) {
            const parts = await Promise.all(args.map((a) => a.jsonValue().catch(() => '?')));
            text = parts.join(' ');
        }
    } catch { /* keep msg.text */ }
    errors.push(`[${msg.type()}] ${String(text).slice(0, 300)}`);
});
page.on('requestfailed', (req) => errors.push(`[requestfailed] ${req.url()} :: ${req.failure()?.errorText}`));

await page.goto(base, { waitUntil: 'domcontentloaded' });

try {
    await page.waitForFunction(
        () => document.querySelector('#app') && document.querySelector('#app').innerText.length > 200,
        null,
        { timeout: 150_000 }
    );
} catch {
    console.log('(timeout waiting for rich app text)');
}

await page.waitForTimeout(2_000);

const appEl = await page.$('#app');
const info = await page.evaluate(() => ({
    appTextLength: document.querySelector('#app')?.innerText.length ?? -1,
    bodyHasLoading: document.body.innerText.includes('Loading Physiquinator'),
    bodyHasGate: document.body.innerText.includes('Preparing your local database'),
}));
console.log('=== STATE ===');
console.log(JSON.stringify(info));
const text = await appEl?.innerText() ?? '(no #app)';
console.log('=== APP TEXT (first 1200) ===');
console.log(text.slice(0, 1200));
console.log('=== CONSOLE / ERRORS (first 25) ===');
for (const e of errors.slice(0, 25)) {
    console.log(e);
}
if (errors.length === 0) {
    console.log('(none captured)');
}
await browser.close();
