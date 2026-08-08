// Runs after blazor.web.js loads (autostart=false). Restores the account's databases
// from IndexedDB before the first circuit starts, then boots Blazor.
(async function () {
    try {
        await window.physiquinatorDb.restoreToServer();
    } catch (error) {
        console.error('Physiquinator database restore failed:', error);
    }
    Blazor.start();

    var dismiss = document.querySelector('#blazor-error-ui .dismiss');
    if (dismiss) {
        dismiss.addEventListener('click', function () {
            document.getElementById('blazor-error-ui').style.display = 'none';
        });
    }
})();

window.physiquinatorAuth = {
    logout: async function () {
        await fetch('/api/auth/logout', { method: 'POST' });
    },
    // Auth must run in the browser: the Set-Cookie response header only reaches
    // the browser when the fetch originates from the page itself.
    login: async function (endpoint, username, password) {
        const response = await fetch('/api/auth/' + endpoint, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ username: username, password: password })
        });
        return await authResult(response);
    },
    demoLogin: async function () {
        const response = await fetch('/api/auth/demo', { method: 'POST' });
        return await authResult(response);
    }
};

async function authResult(response) {
    const text = await response.text();
    let message = text.trim();
    try {
        const parsed = JSON.parse(text);
        if (parsed && typeof parsed.message === 'string') {
            message = parsed.message;
        }
    } catch (e) {
        // not JSON; use the raw text
    }
    return { ok: response.ok, status: response.status, message: message };
}
