// Browser file transfer for the web host: downloads exports to the visitor's
// device and reads picked JSON files as text. The MAUI host uses platform
// share sheets instead (see WebFileTransferService for the web side).
(() => {
    const triggerDownload = (fileName, href) => {
        const a = document.createElement("a");
        a.href = href;
        a.download = fileName;
        document.body.appendChild(a);
        a.click();
        a.remove();
    };

    const download = (fileName, text) => {
        const blob = new Blob([text], { type: "application/json" });
        const url = URL.createObjectURL(blob);
        triggerDownload(fileName, url);
        setTimeout(() => URL.revokeObjectURL(url), 5000);
    };

    const downloadBytes = (fileName, base64) => {
        const bytes = Uint8Array.from(atob(base64), (c) => c.charCodeAt(0));
        const blob = new Blob([bytes], { type: "image/png" });
        const url = URL.createObjectURL(blob);
        triggerDownload(fileName, url);
        setTimeout(() => URL.revokeObjectURL(url), 5000);
    };

    // Resolves with the file text, or null when the picker is cancelled.
    const pickText = (accept) => new Promise((resolve) => {
        const input = document.createElement("input");
        input.type = "file";
        input.accept = accept;
        input.style.display = "none";
        document.body.appendChild(input);
        let settled = false;
        const finish = (value) => {
            if (settled) return;
            settled = true;
            input.remove();
            resolve(value);
        };
        input.addEventListener("change", async () => {
            const file = input.files && input.files[0];
            if (!file) {
                finish(null);
                return;
            }
            try {
                finish(await file.text());
            }
            catch {
                finish(null);
            }
        });
        // Regaining window focus with no selection means the picker was cancelled.
        window.addEventListener("focus", () => {
            setTimeout(() => {
                if (!input.files || !input.files.length) finish(null);
            }, 300);
        }, { once: true });
        input.click();
    });

    window.physiquinatorFiles = { download, downloadBytes, pickText };
})();
