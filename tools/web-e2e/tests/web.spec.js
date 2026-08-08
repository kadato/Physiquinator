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
        await page.getByRole('button', { name: 'Get Started' }).click({ timeout: 5000 });
    } catch {
        // No dialog; nothing to dismiss.
    }

    await page.getByTitle('Sign out of this account').click();
    await expect(page.getByRole('button', { name: 'Sign in' })).toBeVisible({ timeout: 30_000 });
    await expect(page.getByLabel('Username')).toBeVisible({ timeout: 30_000 });
});
