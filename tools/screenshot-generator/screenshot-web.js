// Screenshot generator for the Physiquinator web host (Linux/macOS/Windows).
// Mirrors screenshot.js: same routes, viewport, and themes, but drives
// Physiquinator.Web over plain HTTP instead of the MAUI exe over WebView2 CDP.
//
// Usage:
//   node screenshot-web.js                 # writes to ../../docs
//   SHOTS_DIR=/tmp/shots node screenshot-web.js
const { chromium } = require('playwright');
const { spawn } = require('child_process');
const path = require('path');
const fs = require('fs');
const http = require('http');

const PORT = Number(process.env.SHOT_WEB_PORT || 9255);
const BASE_URL = `http://localhost:${PORT}`;
// Overridable because 9099 can be taken outside WSL by an earlier Windows run.
const AI_PORT = Number(process.env.MOCK_AI_PORT || 9099);
const APP_PATH = path.resolve(__dirname, '../../Physiquinator.Web/bin/Debug/net11.0/Physiquinator.Web.dll');
const DOCS_DIR = process.env.SHOTS_DIR ? path.resolve(process.env.SHOTS_DIR) : path.resolve(__dirname, '../../docs');
const TEMP_DATA_DIR = path.resolve(__dirname, './temp-web-data');

let webProcess;
let mockAiServer;

// Stable identifier from DemoDataIds.cs
const PUSH_PLAN_ID = 'dead0000-0000-4000-8000-000000000001';

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

function startMockAiServer(port) {
    mockAiServer = http.createServer((req, res) => {
        if (req.method === 'POST' && req.url === '/v1/chat/completions') {
            let body = '';
            req.on('data', chunk => { body += chunk; });
            req.on('end', () => {
                res.writeHead(200, {
                    'Content-Type': 'text/event-stream',
                    'Cache-Control': 'no-cache',
                    'Connection': 'keep-alive'
                });

                let userPrompt = '';
                try {
                    const payload = JSON.parse(body);
                    const userMsg = payload.messages.find(m => m.role === 'user');
                    if (userMsg) userPrompt = userMsg.content;
                } catch (e) {
                    console.error('Failed to parse request JSON:', e);
                }

                let responseText = 'Sure! Here is a recommended progressive overload plan...';
                if (userPrompt.includes('progressive overload') || userPrompt.includes('Overload')) {
                    responseText = "Based on your last session for **Bench Press** (100kg for 3 sets of 5 reps, RPE 8.5), I recommend increasing the weight to **102.5kg** for your next workout, aiming for 5 reps on the first set. For **Squats**, since you completed all reps at 120kg, let's increase to **125kg** or add 1 rep to each set.";
                } else if (userPrompt.includes('stats') || userPrompt.includes('Stats')) {
                    responseText = 'Here are your stats for the last 4 weeks:\n- **Volume**: +12% increase\n- **Frequency**: 3.2 workouts/week average\n- **Bench Press 1RM**: Estimated at 116kg (New PR!)';
                }

                const words = responseText.split(' ');
                let index = 0;

                function sendChunk() {
                    if (index < words.length) {
                        const chunkText = words[index] + (index === words.length - 1 ? '' : ' ');
                        const data = { choices: [{ delta: { content: chunkText } }] };
                        res.write(`data: ${JSON.stringify(data)}\n\n`);
                        index++;
                        setTimeout(sendChunk, 30);
                    } else {
                        res.write('data: [DONE]\n\n');
                        res.end();
                    }
                }

                sendChunk();
            });
        } else {
            res.writeHead(404);
            res.end();
        }
    });
    mockAiServer.listen(port, '127.0.0.1', () => {
        console.log(`Mock AI completion server listening on port ${port}`);
    });
}

function waitForHealth(url, timeoutMs) {
    const started = Date.now();
    return new Promise((resolve, reject) => {
        const attempt = () => {
            http.get(url, res => {
                res.resume();
                if (res.statusCode === 200) return resolve();
                retry();
            }).on('error', retry);
        };
        const retry = () => {
            if (Date.now() - started > timeoutMs) return reject(new Error('Web host did not become healthy in time.'));
            setTimeout(attempt, 500);
        };
        attempt();
    });
}

async function startWebHost() {
    console.log('Starting Physiquinator.Web...');
    webProcess = spawn('dotnet', [APP_PATH, `--urls=http://localhost:${PORT}`], {
        env: {
            ...process.env,
            ASPNETCORE_ENVIRONMENT: 'Development',
            PHYSIQUINATOR_DB_DIR: TEMP_DATA_DIR
        },
        detached: false,
        stdio: ['ignore', 'pipe', 'pipe']
    });
    webProcess.stdout.on('data', d => process.stdout.write(`[WEB] ${d}`));
    webProcess.stderr.on('data', d => process.stderr.write(`[WEB] ${d}`));
    await waitForHealth(`${BASE_URL}/healthz`, 90000);
    console.log('Web host is healthy.');
}

async function stopWebHost() {
    if (!webProcess) return;
    try {
        webProcess.kill();
    } catch {}
    webProcess = null;
    // Give the port a moment to free up.
    await delay(1500);
}

// Dismisses the seeded-data onboarding dialog when it is up.
async function dismissOnboarding(page) {
    try {
        const btn = page.getByRole('button', { name: /get started/i });
        await btn.click({ timeout: 4000 });
        await delay(800);
        console.log('Dismissed onboarding dialog.');
    } catch {
        console.log('No onboarding dialog detected.');
    }
}

async function loginWithDemo(page) {
    console.log('Opening the app and logging in through the demo account...');
    await page.goto(BASE_URL, { waitUntil: 'domcontentloaded' });

    // A cookie from an earlier pass may still be valid; only the fresh case
    // shows the login panel.
    const loginVisible = await page.getByRole('button', { name: /explore demo/i })
        .waitFor({ state: 'visible', timeout: 8000 })
        .then(() => true)
        .catch(() => false);

    if (loginVisible) {
        await page.getByRole('button', { name: /explore demo/i }).click();
    } else {
        console.log('Existing session cookie still valid, skipping login.');
    }

    await page.waitForSelector('.app-shell', { timeout: 90000 });
    try {
        await page.waitForSelector('.app-startup-overlay', { state: 'detached', timeout: 30000 });
    } catch {
        console.log('Startup overlay was not shown or did not disappear in time.');
    }
    await dismissOnboarding(page);
    await page.setViewportSize({ width: 390, height: 844 });
    await delay(1000);
}

// Configures the AI provider once so quick actions work against the mock server.
async function configureAiSettings(page) {
    console.log('Configuring AI settings against the mock server...');
    await blazorNavigate(page, '/settings');
    await page.locator('.settings-tab:has-text("AI")').click();
    await page.getByLabel('API Base URL').waitFor({ state: 'visible', timeout: 10000 });

    // Ensure the assistant is enabled - toggle if the switch is off
    try {
        const enableSwitch = page.locator('.mud-switch:has-text("Enable AI Assistant") input');
        const isChecked = await enableSwitch.isChecked({ timeout: 2000 }).catch(() => null);
        if (isChecked === false) {
            await page.locator('.mud-switch:has-text("Enable AI Assistant")').click();
            await delay(300);
        }
    } catch {}

    await page.getByLabel('API Base URL').fill(`http://127.0.0.1:${AI_PORT}/v1`);
    await page.getByLabel('API Key', { exact: false }).fill('dummy-key-for-screenshots');
    await page.getByLabel('Model Name').fill('gpt-4o-mini');
    await page.getByRole('button', { name: 'Save settings' }).click();
    // Wait for success snackbar to appear and then disappear naturally
    await page.waitForSelector('.mud-snackbar', { state: 'visible', timeout: 4000 }).catch(() => {});
    await page.waitForSelector('.mud-snackbar', { state: 'detached', timeout: 4000 }).catch(() => {});
    await delay(300);
    console.log('AI settings saved.');
}

async function selectTheme(page, themeName) {
    console.log(`Setting theme preference to: ${themeName}...`);

    await blazorNavigate(page, '/settings');
    // Revisiting /settings while already there keeps the active tab, so pin
    // the General tab where the Appearance panel lives.
    await page.locator('.settings-tab:has-text("General")').click();
    await page.locator('.settings-panel:has-text("Appearance") .mud-expand-panel-header').click();
    await page.waitForSelector('.mud-select', { timeout: 10000 });
    await delay(500);

    await page.click('.mud-select');
    await delay(500);

    if (themeName === 'light') {
        await page.click('.mud-list-item:has-text("Light (always)")');
    } else {
        await page.click('.mud-list-item:has-text("Dark (always)")');
    }
    // Theme change shows a "Theme updated" snackbar for 3s. Wait for it to
    // appear and disappear so captures stay clean.
    await page.waitForSelector('.mud-snackbar', { state: 'visible', timeout: 3000 }).catch(() => {});
    await page.waitForSelector('.mud-snackbar', { state: 'detached', timeout: 4000 }).catch(() => {});
    await delay(300);
}

async function capture(page, name) {
    const filepath = path.join(DOCS_DIR, name);
    console.log(`Capturing screenshot: ${name}`);

    // Ensure any stray snackbar is gone before capture
    await page.waitForSelector('.mud-snackbar', { state: 'detached', timeout: 1000 }).catch(() => {});
    // Hide scrollbar briefly for a cleaner screenshot
    await page.evaluate(() => {
        document.documentElement.style.overflow = 'hidden';
        if (document.body) document.body.style.overflow = 'hidden';
    });

    await page.screenshot({ path: filepath });

    await page.evaluate(() => {
        document.documentElement.style.overflow = '';
        if (document.body) document.body.style.overflow = '';
    });
}

// Starts each theme pass from an identical state: the web host holds chat
// history and AI settings in memory, so restarting it clears both. The demo
// account database survives on disk, so any active workout is discarded here.
async function beginPass(page) {
    await stopWebHost();
    await startWebHost();
    await loginWithDemo(page);

    // Drop any workout left running by the previous pass.
    try {
        await page.locator('.home-hero__discard').click({ timeout: 3000 });
        await page.getByRole('button', { name: 'Discard', exact: true }).click({ timeout: 5000 });
        await delay(800);
        console.log('Discarded the previous pass workout.');
    } catch {
        console.log('No active workout hero to discard.');
    }
}

// Clears AI chat history through its confirmation dialog when messages exist.
async function clearAiHistory(page) {
    try {
        await blazorNavigate(page, '/ai');
        await page.waitForSelector('.ai-chat-messages', { timeout: 10000 });
        await page.locator('.ai-clear-btn').click({ timeout: 4000 });
        await page.getByRole('button', { name: 'Clear', exact: true }).click({ timeout: 5000 });
        await delay(600);
        console.log('Cleared AI chat history.');
    } catch {
        console.log('No AI chat history to clear.');
    }
}

if (!fs.existsSync(DOCS_DIR)) {
    fs.mkdirSync(DOCS_DIR, { recursive: true });
}

// Fresh database directory per run so captures always show the seeded demo state.
if (fs.existsSync(TEMP_DATA_DIR)) {
    console.log('Cleaning up previous temp web data...');
    try {
        fs.rmSync(TEMP_DATA_DIR, { recursive: true, force: true });
    } catch (e) {
        console.warn('Could not fully clean temp data dir: ', e.message);
    }
}
fs.mkdirSync(TEMP_DATA_DIR, { recursive: true });

async function run() {
    startMockAiServer(AI_PORT);

    try {
        console.log('--- STARTING SCREENSHOT CAPTURE ---');
        await startWebHost();

        const browser = await chromium.launch({ headless: true });
        const context = await browser.newContext({
            viewport: { width: 390, height: 844 },
            deviceScaleFactor: 1,
            locale: 'en-US'
        });
        const page = await context.newPage();
        page.on('pageerror', err => console.error('[PAGE EXCEPTION]', err.stack || err.message));

        await loginWithDemo(page);
        await configureAiSettings(page);

        const themes = ['dark', 'light'];

        for (const [index, theme] of themes.entries()) {
            if (index > 0) {
                // Fresh in-memory state per pass: chat history, AI settings,
                // and theme preference all reset with the host.
                console.log('--- RESTARTING WEB HOST FOR A CLEAN PASS ---');
                await beginPass(page);
                await configureAiSettings(page);
            }
            console.log(`--- CAPTURING ${theme.toUpperCase()} THEME SCREENSHOTS ---`);

            await selectTheme(page, theme);

            // Settings screen
            await capture(page, `settings-${theme}.png`);

            // Home screen
            await blazorNavigate(page, '/');
            await page.waitForSelector('.home-page', { timeout: 10000 });
            await delay(500);
            await capture(page, `home-${theme}.png`);

            // Create Plan screen
            await blazorNavigate(page, '/plan');
            await page.waitForSelector('.plan-page', { timeout: 10000 });
            await page.fill('.plan-details-card input[type="text"]', 'My Custom Workout');
            await page.fill('input[placeholder="Add exercise…"]', 'Squats');
            await page.click('.plan-add-exercise__btn');
            await page.waitForSelector('.plan-exercise-sheet', { timeout: 10000 });
            await delay(500);
            // The sheet's confirm control is a corner FAB rendered outside the
            // dialog element, labeled "Add exercise" for a new exercise.
            await page.click('button[aria-label="Add exercise"]');
            await page.waitForSelector('.plan-exercise-row', { timeout: 10000 });
            await delay(500);
            await page.mouse.move(0, 0);
            await delay(200);
            await capture(page, `create-plan-${theme}.png`);

            // Edit Plan screen (navigate home first so the editor re-initializes)
            await blazorNavigate(page, '/');
            await delay(300);
            await blazorNavigate(page, `/plan/${PUSH_PLAN_ID}`);
            await page.waitForSelector('.plan-page', { timeout: 10000 });
            await delay(500);
            await page.mouse.move(0, 0);
            await delay(200);
            await capture(page, `edit-plan-${theme}.png`);

            // History screen
            await blazorNavigate(page, '/history');
            await page.waitForSelector('.history-heatmap-panel', { timeout: 10000 });
            await page.evaluate(() => {
                const el = document.querySelector('.history-heatmap-panel div');
                if (el) el.scrollLeft = el.scrollWidth;
            });
            await delay(500);
            await capture(page, `history-${theme}.png`);

            // Session Details (second history card is a completed session)
            const cards = page.locator('.history-session-card');
            await cards.nth(1).click();
            await page.waitForSelector('.session-details-page, .mud-paper', { timeout: 10000 });
            await delay(500);
            await capture(page, `session-details-${theme}.png`);

            // Exercise Progression screen
            await blazorNavigate(page, `/history/exercise-progress/${PUSH_PLAN_ID}/Bench Press`);
            await page.waitForSelector('.exercise-progress-chart, .premium-table', { timeout: 15000 });
            await delay(1000);
            await capture(page, `exercise-progression-${theme}.png`);

            // Active Workout rest timer - brutal deck layout
            await blazorNavigate(page, `/workout/${PUSH_PLAN_ID}`);
            await page.waitForSelector('.workout-brutal', { timeout: 15000 });
            await page.waitForSelector('.workout-focus__log', { timeout: 10000 });
            await delay(500);

            // Log a set to start the rest timer
            await page.click('.workout-focus__log');
            await page.waitForSelector('.rest-timer-panel', { timeout: 10000 });
            await delay(500);
            await capture(page, `rest-timer-${theme}.png`);

            // Skip the rest timer before moving on
            try {
                await page.click('button[aria-label="Skip rest"]');
                await delay(500);
            } catch {
                console.log('Rest timer already gone.');
            }

            // AI assistant chat while in use
            await blazorNavigate(page, '/ai');
            await page.waitForSelector('.ai-chat-messages', { timeout: 10000 });
            await delay(500);
            // Clear leftover conversation so the quick-action chips render again
            await clearAiHistory(page);
            await page.click('.ai-quick-btn:has-text("Progressive Overload")');
            try {
                // Wait for the streamed response to finish, not just start.
                await page.waitForSelector('.ai-msg__content:has-text("each set")', { timeout: 20000 });
            } catch {
                await page.waitForSelector('.ai-msg__content', { timeout: 10000 });
            }
            await delay(1000);
            await capture(page, `ai-chat-${theme}.png`);
        }

        await browser.close();

        console.log('All screenshots captured successfully!');
    } catch (err) {
        console.error('An error occurred during screenshot generation:', err);
        process.exitCode = 1;
    } finally {
        if (webProcess) {
            try {
                webProcess.kill();
            } catch (e) {}
        }
        if (mockAiServer) {
            console.log('Stopping mock AI completion server...');
            mockAiServer.close();
        }

        try {
            fs.rmSync(TEMP_DATA_DIR, { recursive: true, force: true });
        } catch (e) {}
    }
}

run();
