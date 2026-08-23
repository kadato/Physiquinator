// Plain (non-module) script so it loads before Blazor boots.
// The WebAssembly host calls these through IJSRuntime by full name.
window.physiquinatorWasm = {
    downloadFile(fileName, mimeType, base64) {
        const binary = atob(base64);
        const bytes = new Uint8Array(binary.length);
        for (let i = 0; i < binary.length; i++) {
            bytes[i] = binary.charCodeAt(i);
        }
        const blob = new Blob([bytes], { type: mimeType });
        const url = URL.createObjectURL(blob);
        const anchor = document.createElement('a');
        anchor.href = url;
        anchor.download = fileName;
        document.body.appendChild(anchor);
        anchor.click();
        anchor.remove();
        setTimeout(() => URL.revokeObjectURL(url), 5000);
    },

    pickJson(pickerTitle) {
        return new Promise((resolve) => {
            const input = document.createElement('input');
            input.type = 'file';
            input.accept = 'application/json,.json';
            if (pickerTitle) {
                input.setAttribute('data-title', pickerTitle);
            }
            input.onchange = async () => {
                const file = input.files && input.files[0];
                if (!file) {
                    resolve(null);
                    return;
                }
                resolve(await file.text());
            };
            input.click();
        });
    },
};
