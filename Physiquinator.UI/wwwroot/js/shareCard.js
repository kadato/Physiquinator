// DOM-to-PNG capture for sharing workout summary cards.
// Depends on html2canvas.min.js being loaded globally by the host shell.
const ShareCard = {};

ShareCard.capture = async function (selector) {
    const element = document.querySelector(selector);
    if (!element) {
        throw new Error('Share card element not found: ' + selector);
    }

    // Wait for webfonts (Outfit) so the captured text does not reflow.
    if (document.fonts && document.fonts.ready) {
        await document.fonts.ready;
    }

    const canvas = await window.html2canvas(element, {
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
