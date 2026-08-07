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
        // --- PHASE 1: STANDARD SCREENS & AI CHAT & GRANTED OVERLAY STATUS ---
        console.log('--- STARTING PHASE 1: STANDARD SCREENS & AI CHAT ---');
        
        const normalPrefs = {
            "physiquinator-theme-preference": "2", // Default to Dark
            "physiquinator_ai_enabled": "True",
            "physiquinator_ai_provider": "OpenAI",
            "physiquinator_ai_base_url": "http://127.0.0.1:9099/v1",
            "physiquinator_ai_api_key": "dummy-key-for-screenshots"
        };
        writePrefs(normalPrefs);

        const phase1 = await launchAppAndConnect();
        const themes = ['light', 'dark'];

        for (const theme of themes) {
            console.log(`--- CAPTURING ${theme.toUpperCase()} THEME SCREENSHOTS (PHASE 1) ---`);
            
            // Set the theme preference
            await selectTheme(phase1.page, theme);

            // Settings screen
            await capture(phase1.page, `settings-${theme}.png`);

            // Home screen
            await blazorNavigate(phase1.page, '/');
            await phase1.page.waitForSelector('.home-hero', { timeout: 5000 });
            await delay(500);
            await capture(phase1.page, `home-${theme}.png`);

            // Create Plan screen
            await blazorNavigate(phase1.page, '/plan');
            await phase1.page.waitForSelector('.plan-page', { timeout: 5000 });
            await phase1.page.fill('.plan-details-card input[type="text"]', 'My Custom Workout');
            await phase1.page.fill('input[placeholder="Add exercise…"]', 'Squats');
            await phase1.page.click('.plan-add-exercise__btn');
            await phase1.page.waitForSelector('.plan-exercise-sheet', { timeout: 5000 });
            await delay(500);
            await phase1.page.click('.plan-exercise-sheet button:has-text("Save exercise")');
            await phase1.page.waitForSelector('.plan-exercise-row', { timeout: 5000 });
            await delay(500);
            await capture(phase1.page, `create-plan-${theme}.png`);

            // Edit Plan screen
            // Navigate to Home first to force Blazor to destroy and re-initialize the PlanWorkout component
            await blazorNavigate(phase1.page, '/');
            await delay(200);
            await blazorNavigate(phase1.page, `/plan/${PUSH_PLAN_ID}`);
            await phase1.page.waitForSelector('.plan-page', { timeout: 5000 });
            await delay(500);
            await capture(phase1.page, `edit-plan-${theme}.png`);

            // History screen
            await blazorNavigate(phase1.page, '/history');
            await phase1.page.waitForSelector('.history-heatmap-panel', { timeout: 5000 });
            // Scroll the heatmap to the end to show the latest activity
            await phase1.page.evaluate(() => {
                const el = document.querySelector('.history-heatmap-panel div');
                if (el) el.scrollLeft = el.scrollWidth;
            });
            await delay(500);
            await capture(phase1.page, `history-${theme}.png`);

            // Session Details screen (click the second history card which is a completed session)
            const cards = phase1.page.locator('.history-session-card');
            await cards.nth(1).click();
            await phase1.page.waitForSelector('.session-details-page, .mud-paper', { timeout: 5000 }); // Wait for navigation
            await delay(500);
            await capture(phase1.page, `session-details-${theme}.png`);

            // Exercise Progression screen
            await blazorNavigate(phase1.page, `/history/exercise-progress/${PUSH_PLAN_ID}/Bench Press`);
            await phase1.page.waitForSelector('.exercise-progress-chart, .premium-table', { timeout: 10000 });
            await delay(1000); // Give the progression line chart time to draw
            await capture(phase1.page, `exercise-progression-${theme}.png`);

            // Workout (Active Workout Log Set & Rest Timer)
            await blazorNavigate(phase1.page, `/workout/${PUSH_PLAN_ID}`);
            await phase1.page.waitForSelector('.workout-exercise-layout', { timeout: 5000 });
            await phase1.page.waitForSelector('.set-active-panel', { timeout: 5000 });
            await delay(500);
            await capture(phase1.page, `log-set-${theme}.png`);

            // Click the inline Log Set button to complete a set and start the rest timer
            await phase1.page.click('.log-set-btn');
            await phase1.page.waitForSelector('.rest-timer-panel', { timeout: 5000 });
            await delay(500);
            await capture(phase1.page, `rest-timer-${theme}.png`);

            // Skip the rest timer so we are ready for next actions
            await phase1.page.click('button[aria-label="Skip rest"]');
            await phase1.page.waitForTimeout(500);

            // AI Chat modal while in use
            await blazorNavigate(phase1.page, '/');
            await phase1.page.waitForSelector('.home-hero', { timeout: 5000 });
            await delay(500);
            await phase1.page.click('button[arialabel="AI Assistant"]');
            await phase1.page.waitForSelector('.ai-chat-container', { timeout: 5000 });
            await delay(500);
            // Click "Progressive Overload" chip
            await phase1.page.click('.mud-chip:has-text("Progressive Overload")');
            // Wait for thinking indicator to appear (if slow) or wait directly for the streamed AI text response
            try {
                await phase1.page.waitForSelector('.ai-msg__content:has-text("Bench Press")', { timeout: 15000 });
            } catch (e) {
                // If it timed out, try waiting for any assistant message content bubble
                await phase1.page.waitForSelector('.ai-msg__content', { timeout: 10000 });
            }
            await delay(1000);
            await capture(phase1.page, `ai-chat-${theme}.png`);
            // Close AI chat modal
            await phase1.page.click('button[aria-label="Close"]');
            await delay(500);

            // Settings with Rest timer expanded (granted state)
            await blazorNavigate(phase1.page, '/settings');
            await phase1.page.waitForSelector('.settings-panel', { timeout: 5000 });
            await phase1.page.locator('.settings-panel:has-text("Rest timer") .mud-expand-panel-header').click();
            await phase1.page.waitForSelector('label:has-text("Notify when rest ends")', { state: 'visible', timeout: 5000 });
            await delay(500);
            await capture(phase1.page, `settings-overlay-granted-${theme}.png`);
            // Collapse rest timer panel so we leave it clean
            await phase1.page.locator('.settings-panel:has-text("Rest timer") .mud-expand-panel-header').click();
            await delay(500);
        }

        // Close Phase 1 app
        await currentBrowser.close();
        currentBrowser = null;
        currentAppProcess.kill();
        currentAppProcess = null;

        // Delay to allow file release
        await delay(2000);

        // --- PHASE 2: MISSING OVERLAY PERMISSION STATES ---
        console.log('--- STARTING PHASE 2: MISSING OVERLAY PERMISSION STATES ---');
        
        const missingOverlayPrefs = {
            "physiquinator-theme-preference": "2", // Default to Dark
            "physiquinator_ai_enabled": "True",
            "physiquinator_ai_provider": "OpenAI",
            "physiquinator_ai_base_url": "http://127.0.0.1:9099/v1",
            "physiquinator_ai_api_key": "dummy-key-for-screenshots",
            "physiquinator_simulate_no_overlay_permission": "True"
        };
        writePrefs(missingOverlayPrefs);

        const phase2 = await launchAppAndConnect();

        for (const theme of themes) {
            console.log(`--- CAPTURING ${theme.toUpperCase()} THEME FOR PHASE 2 ---`);
            
            // Set the theme preference
            await selectTheme(phase2.page, theme);

            // Settings with Rest timer expanded (missing state)
            await blazorNavigate(phase2.page, '/settings');
            await phase2.page.waitForSelector('.settings-panel', { timeout: 5000 });
            await phase2.page.locator('.settings-panel:has-text("Rest timer") .mud-expand-panel-header').click();
            await phase2.page.waitForSelector('label:has-text("Notify when rest ends")', { state: 'visible', timeout: 5000 });
            await delay(500);
            await capture(phase2.page, `settings-overlay-missing-${theme}.png`);
            // Collapse rest timer panel
            await phase2.page.locator('.settings-panel:has-text("Rest timer") .mud-expand-panel-header').click();
            await delay(500);

            // Active Workout page with warning alert shown
            await blazorNavigate(phase2.page, `/workout/${PUSH_PLAN_ID}`);
            await phase2.page.waitForSelector('.workout-exercise-layout', { timeout: 5000 });
            await phase2.page.waitForSelector('.mud-alert', { timeout: 5000 }); // Wait for the warning alert to show
            await delay(500);
            await capture(phase2.page, `workout-overlay-missing-${theme}.png`);
        }

        // Close Phase 2 app
        await currentBrowser.close();
        currentBrowser = null;
        currentAppProcess.kill();
        currentAppProcess = null;

        // Delay to allow file release
        await delay(2000);

        // --- PHASE 3: WORKING OVERLAY STATE ---
        console.log('--- STARTING PHASE 3: WORKING OVERLAY STATE ---');
        
        const workingOverlayPrefs = {
            "physiquinator-theme-preference": "2", // Default to Dark
            "physiquinator_ai_enabled": "True",
            "physiquinator_ai_provider": "OpenAI",
            "physiquinator_ai_base_url": "http://127.0.0.1:9099/v1",
            "physiquinator_ai_api_key": "dummy-key-for-screenshots",
            "physiquinator_simulate_no_overlay_permission": "False",
            "physiquinator_simulate_overlay_active": "True"
        };
        writePrefs(workingOverlayPrefs);

        const phase3 = await launchAppAndConnect();

        for (const theme of themes) {
            console.log(`--- CAPTURING ${theme.toUpperCase()} THEME FOR PHASE 3 ---`);
            
            // Set the theme preference
            await selectTheme(phase3.page, theme);

            // Active Workout page with working overlay shown floating
            await blazorNavigate(phase3.page, `/workout/${PUSH_PLAN_ID}`);
            await phase3.page.waitForSelector('.workout-exercise-layout', { timeout: 5000 });
            await phase3.page.waitForSelector('.simulated-overlay-bubble', { timeout: 5000 });
            await delay(500);
            await capture(phase3.page, `workout-overlay-working-${theme}.png`);
        }

        // Close Phase 3 app
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
