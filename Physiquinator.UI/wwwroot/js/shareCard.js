// DOM-to-PNG capture for sharing workout summary cards.
// html2canvas is lazy-loaded on demand to avoid blocking initial page load.
const ShareCard = {};

let html2canvasPromise = null;

function loadHtml2Canvas() {
    if (window.html2canvas) {
        return Promise.resolve(window.html2canvas);
    }
    if (!html2canvasPromise) {
        html2canvasPromise = new Promise((resolve, reject) => {
            const script = document.createElement('script');
            script.src = '_content/Physiquinator.UI/js/html2canvas.min.js';
            script.onload = () => resolve(window.html2canvas);
            script.onerror = () => reject(new Error('Failed to load html2canvas'));
            document.head.appendChild(script);
        });
    }
    return html2canvasPromise;
}

ShareCard.capture = async function (selector) {
    const element = document.querySelector(selector);
    if (!element) {
        throw new Error('Share card element not found: ' + selector);
    }

    // Wait for webfonts (Outfit) so the captured text does not reflow.
    await document.fonts?.ready;

    const html2canvas = await loadHtml2Canvas();
    const canvas = await html2canvas(element, {
        scale: 2,
        backgroundColor: null,
        logging: false,
        useCORS: true,
        windowWidth: element.scrollWidth,
        windowHeight: element.scrollHeight
    });

    const dataUrl = canvas.toDataURL('image/png');
    return dataUrl;
};

export { ShareCard };
