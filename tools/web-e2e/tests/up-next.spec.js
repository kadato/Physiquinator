// @ts-check
const { test, expect } = require('@playwright/test');

const password = 'e2e-pass-123';

// Dismisses the seeded-data onboarding dialog when it is up. No-op otherwise.
async function dismissOnboarding(page) {
    try {
        await page.getByRole('button', { name: 'Get started' }).click({ timeout: 3000 });
    } catch {
        // No dialog. Nothing to dismiss.
    }
}

// Lands on Home. Registers a fresh account when the host shows a login screen;
// otherwise (demo-first host) proceeds straight to the seeded profile.
async function openHome(page) {
    await page.goto('/');

    const signIn = page.getByRole('button', { name: 'Sign in' });
    if (await signIn.isVisible({ timeout: 15_000 }).catch(() => false)) {
        const username = `e2e-${Date.now()}-${Math.floor(Math.random() * 100000)}`;
        await page.getByRole('button', { name: /create an account|register/i }).click();
        await expect(page.getByRole('button', { name: /create account|register/i })).toBeVisible();
        await page.getByLabel('Username').fill(username);
        await page.getByLabel('Password').fill(password);
        await page.getByRole('button', { name: /create account|register/i }).click();
    }

    await expect(page.getByText('Your plans')).toBeVisible({ timeout: 60_000 });
    await dismissOnboarding(page);
}

test('the up-next card follows a manually picked exercise into rest', async ({ page }) => {
    // Regression: jumping ahead to a later exercise and logging a set used to
    // snap the rest "up next" back to the first uncompleted exercise, so the
    // rest overlay named a different exercise than the one being worked.
    await openHome(page);

    await page.getByRole('button', { name: 'Start Full Body Workout' }).click();

    // Jump ahead to Plank from the sequence strip (it is not the first exercise).
    await page.getByRole('button', { name: /Go to Plank,/ }).click();

    // Log the focused Plank set, which arms the rest timer.
    await page.getByRole('button', { name: /Log set .* for Plank/ }).click();

    // While resting, the up-next card must still name Plank, not Squats.
    const nextName = page.locator('.workout-rest-takeover__next-name');
    await expect(nextName).toBeVisible({ timeout: 30_000 });
    await expect(nextName).toHaveText('Plank');
    await expect(nextName).not.toHaveText('Squats');
});
