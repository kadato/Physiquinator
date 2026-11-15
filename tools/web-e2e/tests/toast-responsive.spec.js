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

function renderSnackbarHtml(message, hasAction = false) {
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
<body data-theme-mode="dark">
  <div id="mud-snackbar-container" class="mud-snackbar-location-bottom-left">
    <div role="alert" aria-live="polite" class="mud-snackbar mud-alert-filled-success">
      <div class="mud-snackbar-icon">
        <svg class="mud-icon-root mud-svg-icon mud-icon-size-medium" viewBox="0 0 24 24"><path d="M9 16.2L4.8 12l-1.4 1.4L9 19 21 7l-1.4-1.4L9 16.2z"/></svg>
      </div>
      <div class="mud-snackbar-content-message">${message}</div>
      <div class="mud-snackbar-content-action">
        ${hasAction ? '<button class="mud-button-root mud-button mud-button-text mud-button-text-primary mud-snackbar-action-button">UNDO</button>' : ''}
        <button class="mud-button-root mud-icon-button mud-icon-button-size-small mud-snackbar-close-button ms-2" aria-label="Close">
          <svg class="mud-icon-root mud-svg-icon mud-icon-size-small" viewBox="0 0 24 24"><path d="M19 6.41L17.59 5 12 10.59 6.41 5 5 6.41 10.59 12 5 17.59 6.41 19 12 13.41 17.59 19 19 17.59 13.41 12z"/></svg>
        </button>
      </div>
    </div>
  </div>
</body>
</html>
  `;
}

const testCases = [
  { name: 'Short message', msg: 'Plan saved.', hasAction: false },
  { name: 'Short with undo action', msg: 'Session deleted.', hasAction: true },
  { name: 'Medium info message', msg: 'No sets were logged, so nothing was saved.', hasAction: false },
  { name: 'Duplicate exercise warning', msg: "'Incline Dumbbell Bench Press' is already in this plan.", hasAction: false },
  { name: 'Long corrupt layout warning', msg: 'Saved workout layout was corrupt, using the current plan instead.', hasAction: false },
  { name: 'Long exception error', msg: 'Could not load sessions: System.InvalidOperationException: SQLite connection failed', hasAction: false },
  { name: 'Plan deleted with undo', msg: "'Upper Body Power & Hypertrophy A' deleted.", hasAction: true },
];

const mobileViewports = [
  { name: 'small-320', width: 320, height: 600, minRightMargin: 10 },
  { name: 'android-360', width: 360, height: 740, minRightMargin: 10 },
  { name: 'iphone-se-375', width: 375, height: 667, minRightMargin: 16 },
  { name: 'iphone-14-390', width: 390, height: 844, minRightMargin: 16 },
  { name: 'pixel-412', width: 412, height: 915, minRightMargin: 16 },
];

for (const vp of mobileViewports) {
  test.describe(`Toast bounds on ${vp.name} (${vp.width}px)`, () => {
    test.use({ viewport: { width: vp.width, height: vp.height } });

    for (const tc of testCases) {
      test(`does not clip out on the right: ${tc.name}`, async ({ page }) => {
        await page.setContent(renderSnackbarHtml(tc.msg, tc.hasAction));

        const metrics = await page.evaluate(() => {
          const sb = document.querySelector('.mud-snackbar');
          const closeBtn = document.querySelector('.mud-snackbar-close-button');
          const actionBtn = document.querySelector('.mud-snackbar-action-button');
          if (!sb) return null;
          const sbr = sb.getBoundingClientRect();
          const cr = closeBtn ? closeBtn.getBoundingClientRect() : null;
          const ar = actionBtn ? actionBtn.getBoundingClientRect() : null;
          return {
            sbRight: sbr.right,
            sbLeft: sbr.left,
            sbWidth: sbr.width,
            closeRight: cr ? cr.right : 0,
            actionRight: ar ? ar.right : 0,
            docScrollWidth: document.documentElement.scrollWidth,
            vpWidth: window.innerWidth
          };
        });

        expect(metrics).not.toBeNull();
        // Toast right edge must never exceed viewport width minus required right margin
        expect(metrics.sbRight).toBeLessThanOrEqual(vp.width - vp.minRightMargin);
        // Close button must never clip
        expect(metrics.closeRight).toBeLessThanOrEqual(vp.width - vp.minRightMargin);
        // Action button (if present) must never clip
        if (tc.hasAction) {
          expect(metrics.actionRight).toBeLessThanOrEqual(vp.width - vp.minRightMargin);
        }
        // Page must never have horizontal overflow
        expect(metrics.docScrollWidth).toBeLessThanOrEqual(vp.width);
      });
    }
  });
}
