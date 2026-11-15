// @ts-check
const { test, expect } = require('@playwright/test');
const fs = require('fs');
const path = require('path');

function findMudCss() {
  const candidate1 = path.resolve(__dirname, '../../../obj/Release/net11.0-android/assets/wwwroot/_content/MudBlazor/MudBlazor.min.css');
  if (fs.existsSync(candidate1)) return fs.readFileSync(candidate1, 'utf8');
  const candidate2 = path.resolve(__dirname, '../../../obj/Debug/net11.0-android/assets/wwwroot/_content/MudBlazor/MudBlazor.min.css');
  if (fs.existsSync(candidate2)) return fs.readFileSync(candidate2, 'utf8');
  const nugetBase = path.join(process.env.HOME || '', '.nuget/packages/mudblazor');
  if (fs.existsSync(nugetBase)) {
    const versions = fs.readdirSync(nugetBase);
    for (const v of versions.sort().reverse()) {
      const p = path.join(nugetBase, v, 'staticwebassets/MudBlazor.min.css');
      if (fs.existsSync(p)) return fs.readFileSync(p, 'utf8');
    }
  }
  return '';
}

const mudCss = findMudCss();
const tokensCss = fs.readFileSync(path.resolve(__dirname, '../../../Physiquinator.UI/wwwroot/css/tokens.css'), 'utf8');
const overridesCss = fs.readFileSync(path.resolve(__dirname, '../../../Physiquinator.UI/wwwroot/css/app-overrides.css'), 'utf8');

function renderAlertHtml(message) {
  return `
<!DOCTYPE html>
<html>
<head>
  <meta name="viewport" content="width=device-width, initial-scale=1.0, viewport-fit=cover">
  <style>
    ${mudCss}
    ${tokensCss}
    ${overridesCss}
  </style>
</head>
<body data-theme-mode="dark" style="margin: 0; padding: 16px; background: #141624;">
  <div style="width: 100%; max-width: 100%; box-sizing: border-box;">
    <div class="mud-alert mud-alert-filled-error">
      <div class="mud-alert-icon">
        <svg class="mud-icon-root mud-svg-icon mud-icon-size-medium" viewBox="0 0 24 24"><path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1 15h-2v-2h2v2zm0-4h-2V7h2v6z"/></svg>
      </div>
      <div class="mud-alert-message">${message}</div>
    </div>
  </div>
</body>
</html>
  `;
}

function renderPopoverHtml(items) {
  return `
<!DOCTYPE html>
<html>
<head>
  <meta name="viewport" content="width=device-width, initial-scale=1.0, viewport-fit=cover">
  <style>
    ${mudCss}
    ${tokensCss}
    ${overridesCss}
  </style>
</head>
<body data-theme-mode="dark" style="margin: 0; padding: 0; background: #141624;">
  <div class="mud-popover-provider">
    <div class="mud-popover mud-popover-open mud-popover-top-left" style="top: 20px; left: 10px;">
      <div class="mud-list mud-list-padding">
        ${items.map(item => `
          <div class="mud-list-item mud-list-item-gutters mud-list-item-clickable">
            <div class="mud-list-item-text">
              <p class="mud-typography mud-typography-body1">${item}</p>
            </div>
          </div>
        `).join('')}
      </div>
    </div>
  </div>
</body>
</html>
  `;
}

function renderDialogHtml(title, message, btn1, btn2) {
  return `
<!DOCTYPE html>
<html>
<head>
  <meta name="viewport" content="width=device-width, initial-scale=1.0, viewport-fit=cover">
  <style>
    ${mudCss}
    ${tokensCss}
    ${overridesCss}
  </style>
</head>
<body data-theme-mode="dark" style="margin: 0; padding: 0; background: #141624;">
  <div class="mud-dialog-container">
    <div class="mud-dialog" role="dialog" aria-modal="true">
      <div class="mud-dialog-title">
        <h6 class="mud-typography mud-typography-h6">${title}</h6>
      </div>
      <div class="mud-dialog-content">
        <p class="mud-typography mud-typography-body1">${message}</p>
      </div>
      <div class="mud-dialog-actions">
        <button class="mud-button-root mud-button mud-button-outlined">${btn1}</button>
        <button class="mud-button-root mud-button mud-button-filled mud-button-filled-primary">${btn2}</button>
      </div>
    </div>
  </div>
</body>
</html>
  `;
}

const viewports = [
  { name: 'small-320', width: 320, height: 568 },
  { name: 'android-360', width: 360, height: 740 },
  { name: 'iphone-375', width: 375, height: 667 },
];

test.describe('Mobile Overlay & Component Layout Audit', () => {
  for (const vp of viewports) {
    test.describe(`Viewport: ${vp.name} (${vp.width}px)`, () => {
      test('MudAlert with long unbroken error string does not overflow viewport', async ({ page }) => {
        await page.setViewportSize({ width: vp.width, height: vp.height });
        const unbrokenError = 'System.InvalidOperationException_DatabaseConnectionFailure_Code_0x80004005_ServerName_LocalDatabaseInstance_PhysiquinatorAppData_V2';
        await page.setContent(renderAlertHtml(unbrokenError));

        const alertBox = await page.locator('.mud-alert').boundingBox();
        expect(alertBox).not.toBeNull();
        const alertRight = alertBox.x + alertBox.width;
        expect(alertRight).toBeLessThanOrEqual(vp.width);

        const iconBox = await page.locator('.mud-alert-icon svg').boundingBox();
        expect(iconBox).not.toBeNull();
        // Icon should not be squashed horizontally
        expect(Math.abs(iconBox.width - iconBox.height)).toBeLessThanOrEqual(2);

        // Check horizontal scroll on page
        const scrollWidth = await page.evaluate(() => document.documentElement.scrollWidth);
        expect(scrollWidth).toBeLessThanOrEqual(vp.width);
      });

      test('MudPopover with long menu options does not overflow viewport', async ({ page }) => {
        await page.setViewportSize({ width: vp.width, height: vp.height });
        const items = [
          'Edit workout details',
          'Deficit Romanian Deadlift with Extra Long Resistance Band (Single Leg)',
          'Export complete workout data backup file (JSON format)',
        ];
        await page.setContent(renderPopoverHtml(items));

        const popoverBox = await page.locator('.mud-popover.mud-popover-open').boundingBox();
        expect(popoverBox).not.toBeNull();
        const popoverRight = popoverBox.x + popoverBox.width;
        expect(popoverRight).toBeLessThanOrEqual(vp.width);
        expect(popoverBox.width).toBeLessThanOrEqual(vp.width - 16);

        const scrollWidth = await page.evaluate(() => document.documentElement.scrollWidth);
        expect(scrollWidth).toBeLessThanOrEqual(vp.width);
      });

      test('MudDialog with long title and wide actions fits viewport without clipping', async ({ page }) => {
        await page.setViewportSize({ width: vp.width, height: vp.height });
        const title = "Confirm Delete 'Upper Body Power & Hypertrophy A'?";
        const message = 'Are you sure you want to permanently delete this workout plan? This action cannot be undone.';
        const btn1 = 'Cancel & Go Back';
        const btn2 = 'Permanently Delete Plan';
        await page.setContent(renderDialogHtml(title, message, btn1, btn2));

        const dialogBox = await page.locator('.mud-dialog').boundingBox();
        expect(dialogBox).not.toBeNull();
        const dialogRight = dialogBox.x + dialogBox.width;
        expect(dialogRight).toBeLessThanOrEqual(vp.width);

        const titleBox = await page.locator('.mud-dialog-title .mud-typography-h6').boundingBox();
        expect(titleBox).not.toBeNull();
        const titleRight = titleBox.x + titleBox.width;
        expect(titleRight).toBeLessThanOrEqual(dialogRight);

        // Check that buttons fit within the dialog width
        const buttons = await page.locator('.mud-dialog-actions button').all();
        for (const btn of buttons) {
          const btnBox = await btn.boundingBox();
          expect(btnBox).not.toBeNull();
          const btnRight = btnBox.x + btnBox.width;
          expect(btnRight).toBeLessThanOrEqual(dialogRight + 1);
        }

        const scrollWidth = await page.evaluate(() => document.documentElement.scrollWidth);
        expect(scrollWidth).toBeLessThanOrEqual(vp.width);
      });
    });
  }
});
