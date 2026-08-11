// Renders the app-icon favicon SVG to PNG at the sizes needed by the web host.
const { chromium } = require('playwright');
const path = require('path');

const SVG = path.resolve(__dirname, '../../Physiquinator.UI/wwwroot/favicon.svg');
const OUT = path.resolve(__dirname, '../../Physiquinator.UI/wwwroot');

(async () => {
  const browser = await chromium.launch();
  const page = await browser.newPage({ viewport: { width: 400, height: 400 } });
  await page.goto('file:///' + SVG.replace(/\\/g, '/'), { waitUntil: 'load' });

  const svg = page.locator('svg');
  const sizes = [['favicon.png', 64], ['apple-touch-icon.png', 180]];
  for (const [name, size] of sizes) {
    await page.evaluate((s) => {
      const el = document.querySelector('svg');
      el.setAttribute('width', String(s));
      el.setAttribute('height', String(s));
    }, size);
    await svg.screenshot({ path: path.join(OUT, name) });
    console.log('wrote', name, size + 'x' + size);
  }
  await browser.close();
})();
