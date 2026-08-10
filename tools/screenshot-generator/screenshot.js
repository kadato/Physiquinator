const { chromium } = require('playwright');
const { spawn } = require('child_process');
const path = require('path');
const fs = require('fs');
const http = require('http');

const PORT = 9255;
const APP_PATH = path.resolve(__dirname, '../../artifacts/windows-debug/Physiquinator.exe');
const DOCS_DIR = path.resolve(__dirname, '../../docs');
const TEMP_DATA_DIR = path.resolve(__dirname, './temp-app-data');

let currentAppProcess;
let currentBrowser;


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

function writePrefs(prefs) {
    if (!fs.existsSync(TEMP_DATA_DIR)) {
        fs.mkdirSync(TEMP_DATA_DIR, { recursive: true });
    }
    const filepath = path.join(TEMP_DATA_DIR, 'screenshot_preferences.json');
    fs.writeFileSync(filepath, JSON.stringify(prefs, null, 2));
    console.log(`Wrote preferences to: ${filepath}`);
}

let mockAiServer;
function startMockAiServer(port) {
    mockAiServer = http.createServer((req, res) => {
        if (req.method === 'POST' && req.url === '/v1/chat/completions') {
            let body = '';
            req.on('data', chunk => {
                body += chunk;
            });
            req.on('end', () => {
                console.log('Mock AI server received request:', body);
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

                let responseText = "Sure! Here is a recommended progressive overload plan...";
                if (userPrompt.includes("progressive overload") || userPrompt.includes("Overload")) {
                    responseText = "Based on your last session for **Bench Press** (100kg for 3 sets of 5 reps, RPE 8.5), I recommend increasing the weight to **102.5kg** for your next workout, aiming for 5 reps on the first set. For **Squats**, since you completed all reps at 120kg, let's increase to **125kg** or add 1 rep to each set.";
                } else if (userPrompt.includes("stats") || userPrompt.includes("Stats")) {
                    responseText = "Here are your stats for the last 4 weeks:\n- **Volume**: +12% increase\n- **Frequency**: 3.2 workouts/week average\n- **Bench Press 1RM**: Estimated at 116kg (New PR!)";
                }

                // Stream response in chunks
                const words = responseText.split(' ');
                let index = 0;

                function sendChunk() {
                    if (index < words.length) {
                        const chunkText = words[index] + (index === words.length - 1 ? '' : ' ');
                        const data = {
                            choices: [{
                                delta: {
                                    content: chunkText
                                }
                            }]
                        };
                        res.write(`data: ${JSON.stringify(data)}\n\n`);
                        index++;
                        setTimeout(sendChunk, 30); // wait 30ms between chunks
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

async function launchAppAndConnect() {
    console.log('Starting Physiquinator with remote debugging...');
    
    // Launch the MAUI Windows app with remote debugging enabled
    // We override LOCALAPPDATA to isolate the SQLite database and MAUI preferences.
    currentAppProcess = spawn(APP_PATH, [], {
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
    try {
        currentBrowser = await chromium.connectOverCDP(`http://localhost:${PORT}`);
    } catch (err) {
        console.error('Failed to connect to the app. Make sure the app is built and running in debug mode.', err);
        currentAppProcess.kill();
        currentAppProcess = null;
        process.exit(1);
    }

    const context = currentBrowser.contexts()[0];
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

    return { page };
}

// Helper: Select theme in Settings page
async function selectTheme(page, themeName) {
    console.log(`Setting theme preference to: ${themeName}...`);
    
    // Go to settings and expand the Appearance panel
    await blazorNavigate(page, '/settings');
    await page.locator('.settings-panel:has-text("Appearance") .mud-expand-panel-header').click();
    await page.waitForSelector('.mud-select', { timeout: 10000 });
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
async function capture(page, name) {
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
    // 1. Start mock AI server on port 9099
    startMockAiServer(9099);

    try {
        // --- STANDARD SCREENS & AI CHAT ---
        console.log('--- STARTING SCREENSHOT CAPTURE ---');
        
        const normalPrefs = {
            "physiquinator-theme-preference": "2", // Default to Dark
            "physiquinator_ai_enabled": "True",
            "physiquinator_ai_provider": "OpenAI",
            "physiquinator_ai_base_url": "http://127.0.0.1:9099/v1",
            "physiquinator_ai_api_key": "dummy-key-for-screenshots"
        };
        writePrefs(normalPrefs);

        const app = await launchAppAndConnect();
        const themes = ['light', 'dark'];

        for (const theme of themes) {
            console.log(`--- CAPTURING ${theme.toUpperCase()} THEME SCREENSHOTS ---`);
            
            // Set the theme preference
            await selectTheme(app.page, theme);

            // Settings screen
            await capture(app.page, `settings-${theme}.png`);

            // Home screen
            await blazorNavigate(app.page, '/');
            await app.page.waitForSelector('.home-page', { timeout: 5000 });
            await delay(500);
            await capture(app.page, `home-${theme}.png`);

            // Create Plan screen
            await blazorNavigate(app.page, '/plan');
            await app.page.waitForSelector('.plan-page', { timeout: 5000 });
            await app.page.fill('.plan-details-card input[type="text"]', 'My Custom Workout');
            await app.page.fill('input[placeholder="Add exercise…"]', 'Squats');
            await app.page.click('.plan-add-exercise__btn');
            await app.page.waitForSelector('.plan-exercise-sheet', { timeout: 5000 });
            await delay(500);
            await app.page.click('.plan-exercise-sheet button:has-text("Save exercise")');
            await app.page.waitForSelector('.plan-exercise-row', { timeout: 5000 });
            await delay(500);
            await capture(app.page, `create-plan-${theme}.png`);

            // Edit Plan screen
            // Navigate to Home first to force Blazor to destroy and re-initialize the PlanWorkout component
            await blazorNavigate(app.page, '/');
            await delay(200);
            await blazorNavigate(app.page, `/plan/${PUSH_PLAN_ID}`);
            await app.page.waitForSelector('.plan-page', { timeout: 5000 });
            await delay(500);
            await capture(app.page, `edit-plan-${theme}.png`);

            // History screen
            await blazorNavigate(app.page, '/history');
            await app.page.waitForSelector('.history-heatmap-panel', { timeout: 5000 });
            // Scroll the heatmap to the end to show the latest activity
            await app.page.evaluate(() => {
                const el = document.querySelector('.history-heatmap-panel div');
                if (el) el.scrollLeft = el.scrollWidth;
            });
            await delay(500);
            await capture(app.page, `history-${theme}.png`);

            // Session Details screen (click the second history card which is a completed session)
            const cards = app.page.locator('.history-session-card');
            await cards.nth(1).click();
            await app.page.waitForSelector('.session-details-page, .mud-paper', { timeout: 5000 }); // Wait for navigation
            await delay(500);
            await capture(app.page, `session-details-${theme}.png`);

            // Exercise Progression screen
            await blazorNavigate(app.page, `/history/exercise-progress/${PUSH_PLAN_ID}/Bench Press`);
            await app.page.waitForSelector('.exercise-progress-chart, .premium-table', { timeout: 10000 });
            await delay(1000); // Give the progression line chart time to draw
            await capture(app.page, `exercise-progression-${theme}.png`);

            // Workout (Active Workout rest timer)
            await blazorNavigate(app.page, `/workout/${PUSH_PLAN_ID}`);
            await app.page.waitForSelector('.workout-exercise-layout', { timeout: 5000 });
            await app.page.waitForSelector('.set-active-panel', { timeout: 5000 });
            await delay(500);

            // Click the inline Log Set button to complete a set and start the rest timer
            await app.page.click('.log-set-btn');
            await app.page.waitForSelector('.rest-timer-panel', { timeout: 5000 });
            await delay(500);
            await capture(app.page, `rest-timer-${theme}.png`);

            // Skip the rest timer so we are ready for next actions
            await app.page.click('button[aria-label="Skip rest"]');
            await app.page.waitForTimeout(500);

            // AI Chat modal while in use
            await blazorNavigate(app.page, '/');
            await app.page.waitForSelector('.home-page', { timeout: 5000 });
            await delay(500);
            await app.page.click('button[aria-label="AI assistant"]');
            await app.page.waitForSelector('.ai-chat-container', { timeout: 5000 });
            await delay(500);
            // Clear any conversation left over from a previous theme so the
            // quick-action chips render again (they only show on an empty chat).
            try {
                await app.page.click('button[aria-label="Clear history"]');
                await delay(300);
            } catch (e) {
                console.log('No chat history to clear.');
            }
            // Click "Progressive Overload" chip
            await app.page.click('.mud-chip:has-text("Progressive Overload")');
            // Wait for thinking indicator to appear (if slow) or wait directly for the streamed AI text response
            try {
                await app.page.waitForSelector('.ai-msg__content:has-text("Bench Press")', { timeout: 15000 });
            } catch (e) {
                // If it timed out, try waiting for any assistant message content bubble
                await app.page.waitForSelector('.ai-msg__content', { timeout: 10000 });
            }
            await delay(1000);
            await capture(app.page, `ai-chat-${theme}.png`);
            // Close AI chat modal
            await app.page.click('button[aria-label="Close"]');
            await delay(500);
        }

        // Close the app
        await currentBrowser.close();
        currentBrowser = null;
        currentAppProcess.kill();
        currentAppProcess = null;

        console.log('All screenshots captured successfully!');
    } catch (err) {
        console.error('An error occurred during screenshot generation:', err);
    } finally {
        if (currentBrowser) {
            try {
                await currentBrowser.close();
            } catch (e) {}
        }
        if (currentAppProcess) {
            try {
                currentAppProcess.kill();
            } catch (e) {}
        }
        if (mockAiServer) {
            console.log('Stopping mock AI completion server...');
            mockAiServer.close();
        }

        // Clean up temp app data to be tidy
        try {
            fs.rmSync(TEMP_DATA_DIR, { recursive: true, force: true });
        } catch (e) {
            // Ignore lock issues
        }
    }
}

run();
