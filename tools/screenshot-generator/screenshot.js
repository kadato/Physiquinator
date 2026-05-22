const { chromium } = require('playwright');
const { spawn } = require('child_process');
const path = require('path');
const fs = require('fs');

const PORT = 9222;
const APP_PATH = path.resolve(__dirname, '../../artifacts/windows-debug/Physiquinator.exe');
const DOCS_DIR = path.resolve(__dirname, '../../docs');
const TEMP_DATA_DIR = path.resolve(__dirname, './temp-app-data');

// Stable identifiers from DemoDataIds.cs
const PUSH_PLAN_ID = "dead0000-0000-4000-8000-000000000001";

async function delay(ms) {
    return new Promise(resolve => setTimeout(resolve, ms));
}

async function blazorNavigate(page, relativeUrl) {
    console.log(`Navigating client-side to: ${relativeUrl}`);
    await page.evaluate((url) => {
        const anchor = document.createElement('a');
        anchor.href = url;
        anchor.style.display = 'none';
        document.body.appendChild(anchor);
        anchor.click();
        anchor.remove();
    }, relativeUrl);
}

// Ensure the docs directory exists
if (!fs.existsSync(DOCS_DIR)) {
    fs.mkdirSync(DOCS_DIR, { recursive: true });
}

// Clean up previous temp app data so we start with a fresh, seeded database
if (fs.existsSync(TEMP_DATA_DIR)) {
    console.log('Cleaning up previous temp app data...');
    try {
        fs.rmSync(TEMP_DATA_DIR, { recursive: true, force: true });
    } catch (e) {
        console.warn('Could not fully clean temp data dir: ', e.message);
    }
}
fs.mkdirSync(TEMP_DATA_DIR, { recursive: true });

async function run() {
    console.log('Starting Physiquinator with remote debugging...');
    
    // Launch the MAUI Windows app with remote debugging enabled
    // We override LOCALAPPDATA to isolate the SQLite database and MAUI preferences.
    const appProcess = spawn(APP_PATH, [], {
        env: {
            ...process.env,
            LOCALAPPDATA: TEMP_DATA_DIR,
            APPDATA: TEMP_DATA_DIR,
            PHYSIQUINATOR_SCREENSHOT_MODE: 'true',
            PHYSIQUINATOR_DB_DIR: TEMP_DATA_DIR,
            WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS: `--remote-debugging-port=${PORT}`
        },
        detached: false,
        stdio: 'ignore'
    });

    console.log('Waiting for WebView2 CDP server to spin up...');
    await delay(5000);

    console.log(`Connecting Playwright to http://localhost:${PORT}...`);
    let browser;
    try {
        browser = await chromium.connectOverCDP(`http://localhost:${PORT}`);
    } catch (err) {
        console.error('Failed to connect to the app. Make sure the app is built and running in debug mode.', err);
        appProcess.kill();
        process.exit(1);
    }

    const context = browser.contexts()[0];
    let page = context.pages()[0];
    if (!page) {
        console.log('Waiting for page to load...');
        page = await context.waitForEvent('page');
    }

    page.on('console', msg => {
        console.log(`[PAGE ${msg.type().toUpperCase()}]`, msg.text());
    });
    page.on('pageerror', err => {
        console.error('[PAGE EXCEPTION]', err.stack || err.message);
    });

    console.log('Page detected. Waiting for main application wrapper...');
    await page.waitForSelector('.app-shell', { timeout: 15000 });

    console.log('Waiting for setup overlay to disappear...');
    try {
        await page.waitForSelector('.app-startup-overlay', { state: 'detached', timeout: 30000 });
    } catch (e) {
        console.log('Setup overlay was not shown or did not disappear in time.');
    }

    const origin = new URL(page.url()).origin;
    console.log('Detected base origin:', origin);

    // Dismiss the first-time onboarding modal if it appears
    try {
        const onboardingBtn = page.locator('button:has-text("Get Started")');
        console.log('Waiting for onboarding welcome dialog...');
        await onboardingBtn.waitFor({ state: 'visible', timeout: 10000 });
        console.log('Dismissing onboarding welcome dialog...');
        await onboardingBtn.click();
        await delay(1000);
    } catch (e) {
        console.log('No onboarding dialog detected or already dismissed.');
    }

    // Emulate a standard modern phone viewport (aspect ratio ~9:19.5, iPhone 12/13/14 size)
    await page.setViewportSize({ width: 390, height: 844 });
    await delay(1000);

    // Helper: Select theme in Settings page
    async function selectTheme(themeName) {
        console.log(`Setting theme preference to: ${themeName}...`);
        
        // Go to settings page
        await blazorNavigate(page, '/settings');
        await page.waitForSelector('.settings-card', { timeout: 10000 });
        await delay(500);

        // Click on the MudSelect input for Theme
        await page.click('.mud-select');
        await delay(500);

        // Click the option
        if (themeName === 'light') {
            await page.click('.mud-list-item:has-text("Light (always)")');
        } else {
            await page.click('.mud-list-item:has-text("Dark (always)")');
        }
        await delay(1500); // Wait for Blazor and WebView to transition the theme colors
    }

    // Helper: Take a screenshot
    async function capture(name) {
        const filepath = path.join(DOCS_DIR, name);
        console.log(`Capturing screenshot: ${name}`);
        
        // Hide scrollbar briefly for a cleaner screenshot
        await page.evaluate(() => {
            document.documentElement.style.overflow = 'hidden';
            if (document.body) document.body.style.overflow = 'hidden';
        });
        
        await page.screenshot({ path: filepath });
        
        // Restore scrollbar
        await page.evaluate(() => {
            document.documentElement.style.overflow = '';
            if (document.body) document.body.style.overflow = '';
        });
    }

    try {
        // We capture Light theme screenshots first, then Dark theme screenshots.
        const themes = ['light', 'dark'];
        
        for (const theme of themes) {
            console.log(`--- CAPTURING ${theme.toUpperCase()} THEME SCREENSHOTS ---`);
            
            // 1. Set the theme preference
            await selectTheme(theme);

            // 2. Settings screen
            await capture(`settings-${theme}.png`);

            // 3. Home screen
            await blazorNavigate(page, '/');
            await page.waitForSelector('.home-hero', { timeout: 5000 });
            await delay(500);
            await capture(`home-${theme}.png`);

            // 4. Create Plan screen
            await blazorNavigate(page, '/plan');
            await page.waitForSelector('.plan-page', { timeout: 5000 });
            await page.fill('.plan-details-card input[type="text"]', 'My Custom Workout');
            await page.fill('input[placeholder="Add exercise…"]', 'Squats');
            await page.click('.plan-add-exercise__btn');
            await page.waitForSelector('.plan-exercise-sheet', { timeout: 5000 });
            await delay(500);
            await page.click('.plan-exercise-sheet button:has-text("Save exercise")');
            await page.waitForSelector('.plan-exercise-row', { timeout: 5000 });
            await delay(500);
            await capture(`create-plan-${theme}.png`);

            // 5. Edit Plan screen
            // Navigate to Home first to force Blazor to destroy and re-initialize the PlanWorkout component
            await blazorNavigate(page, '/');
            await delay(200);
            await blazorNavigate(page, `/plan/${PUSH_PLAN_ID}`);
            await page.waitForSelector('.plan-page', { timeout: 5000 });
            await delay(500);
            await capture(`edit-plan-${theme}.png`);

            // 6. History screen
            await blazorNavigate(page, '/history');
            await page.waitForSelector('.history-heatmap-panel', { timeout: 5000 });
            // Scroll the heatmap to the end to show the latest activity
            await page.evaluate(() => {
                const el = document.querySelector('.history-heatmap-panel div');
                if (el) el.scrollLeft = el.scrollWidth;
            });
            await delay(500);
            await capture(`history-${theme}.png`);

            // 7. Session Details screen (click the second history card which is a completed session)
            const cards = page.locator('.history-session-card');
            await cards.nth(1).click();
            await page.waitForSelector('.session-details-page, .mud-paper', { timeout: 5000 }); // Wait for navigation
            await delay(500);
            await capture(`session-details-${theme}.png`);

            // 8. Exercise Progression screen
            await blazorNavigate(page, `/history/exercise-progress/${PUSH_PLAN_ID}/Bench Press`);
            await page.waitForSelector('.exercise-progress-chart, .premium-table', { timeout: 10000 });
            await delay(1000); // Give the progression line chart time to draw
            await capture(`exercise-progression-${theme}.png`);

            // 9. Workout (Active Workout Rest Timer & Log Set Dialog)
            // Navigate to the in-progress workout
            await blazorNavigate(page, `/workout/${PUSH_PLAN_ID}`);
            await page.waitForSelector('.workout-exercise-layout', { timeout: 5000 });
            await delay(500);

            // Click Done to open the Log Set Dialog
            await page.click('.workout-upcoming-section button:has-text("Done")');
            await page.waitForSelector('.set-log-dialog', { timeout: 5000 });
            await delay(500);
            await capture(`log-set-${theme}.png`);

            // Confirm set log to start rest timer
            await page.click('.set-log-dialog__footer button:has-text("Log set")');
            await page.waitForSelector('.rest-timer-panel', { timeout: 5000 });
            await delay(500);
            await capture(`rest-timer-${theme}.png`);

            // Skip the rest timer so we are ready for next actions
            await page.click('button:has-text("Skip")');
            await page.waitForTimeout(500);
        }

        console.log('All screenshots captured successfully!');
    } catch (err) {
        console.error('An error occurred during screenshot generation:', err);
    } finally {
        console.log('Closing browser and terminating application...');
        await browser.close();
        appProcess.kill();
        
        // Clean up temp app data to be tidy
        try {
            fs.rmSync(TEMP_DATA_DIR, { recursive: true, force: true });
        } catch (e) {
            // Ignore lock issues
        }
    }
}

run();
