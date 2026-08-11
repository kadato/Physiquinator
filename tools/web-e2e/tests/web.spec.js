// @ts-check
const { test, expect } = require('@playwright/test');

const password = 'e2e-pass-123';

// Fresh account per test so re-runs and repeated tests never collide.
async function registerAndExpectHome(page) {
    const username = `e2e-${Date.now()}-${Math.floor(Math.random() * 100000)}`;
    await page.goto('/');
    await expect(page.getByRole('button', { name: 'Sign in' })).toBeVisible({ timeout: 30_000 });

    // Switch to registration mode, then submit.
    await page.getByRole('button', { name: /create an account/i }).click();
    await expect(page.getByRole('button', { name: 'Create account' })).toBeVisible();

    await page.getByLabel('Username').fill(username);
    await page.getByLabel('Password').fill(password);
    await page.getByRole('button', { name: 'Create account' }).click();

    await expect(page.getByText('Your plans')).toBeVisible({ timeout: 60_000 });
}

test('registration lands on the app with seeded demo plans', async ({ page }) => {
    await registerAndExpectHome(page);
    await expect(page.getByText('Push Day').first()).toBeVisible({ timeout: 30_000 });
});

test('the database syncs to IndexedDB', async ({ page }) => {
    await registerAndExpectHome(page);

    // DbSyncHost uploads the account database right after init and then every 15s.
    await page.waitForFunction(async () => {
        const names = await window.physiquinatorDb.list();
        return names.some((name) => typeof name === 'string' && name.startsWith('physiquinator_') && name.endsWith('.db3'));
    }, null, { timeout: 60_000 });

    const names = await page.evaluate(() => window.physiquinatorDb.list());
    console.log('IndexedDB contents:', JSON.stringify(names));
    expect(names.some((name) => typeof name === 'string' && !name.includes('physiquinator-users'))).toBeTruthy();
});

test('login with the wrong password shows an error', async ({ page }) => {
    await page.goto('/');
    await expect(page.getByRole('button', { name: 'Sign in' })).toBeVisible({ timeout: 30_000 });

    await page.getByLabel('Username').fill('demo');
    await page.getByLabel('Password').fill('definitely-wrong');
    await page.getByRole('button', { name: 'Sign in' }).click();

    await expect(page.getByText(/Invalid username or password/i)).toBeVisible({ timeout: 30_000 });
});

test('one-click demo login opens the app', async ({ page }) => {
    await page.goto('/');
    await expect(page.getByRole('button', { name: 'Try the demo' })).toBeVisible({ timeout: 30_000 });

    await page.getByRole('button', { name: 'Try the demo' }).click();
    await expect(page.getByText('Your plans')).toBeVisible({ timeout: 60_000 });
});

test('sign out returns to the login screen', async ({ page }) => {
    await registerAndExpectHome(page);

    // Dismiss the seeded-data onboarding dialog if it appears.
    try {
        await page.getByRole('button', { name: 'Get started' }).click({ timeout: 5000 });
    } catch {
        // No dialog; nothing to dismiss.
    }

    // The Account panel lives on the Settings page: expand it, then sign out.
    await page.goto('/settings');
    await page.locator('.mud-expand-panel-header', { hasText: 'Account' }).click();
    await page.getByRole('button', { name: 'Sign out' }).click();
    await expect(page.getByRole('button', { name: 'Sign in' })).toBeVisible({ timeout: 30_000 });
    await expect(page.getByLabel('Username')).toBeVisible({ timeout: 30_000 });
});

// Dismisses the seeded-data onboarding dialog when it is up; no-op otherwise.
async function dismissOnboarding(page) {
    try {
        await page.getByRole('button', { name: 'Get started' }).click({ timeout: 3000 });
    } catch {
        // No dialog; nothing to dismiss.
    }
}

test('the rest timer resumes after leaving and returning to a workout', async ({ page }) => {
    // Regression: navigating away from the active workout and back used to
    // freeze the countdown (the page-disposed timer interop poisoned the
    // shared JS module, so the tick chain never restarted).
    await registerAndExpectHome(page);
    await dismissOnboarding(page);

    // Start the seeded plan and log a set to arm the rest timer.
    await page.getByRole('button', { name: 'Start Push Day' }).click();
    await page.getByRole('button', { name: 'Log set' }).click();

    const digits = page.locator('.rest-timer-digits');
    await expect(digits).toBeVisible({ timeout: 30_000 });

    // Leave through the back guard, then resume the session from Home.
    await page.goBack();
    await page.getByRole('button', { name: 'Leave' }).click();
    await dismissOnboarding(page);
    await page.getByRole('button', { name: 'Continue workout' }).click();

    // The countdown must be live again: the digits change across a ~2.5s window.
    await expect(digits).toBeVisible({ timeout: 30_000 });
    const readDigits = async () => (await digits.textContent()).replace(/\s+/g, '');
    const before = await readDigits();
    await page.waitForTimeout(2500);
    const after = await readDigits();
    expect(after).not.toBe(before);
});
