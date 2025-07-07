// Toxiq.WebApp.Client/wwwroot/js/share-menu.js
// JavaScript helper functions for share context menu

window.getBoundingClientRect = function (element) {
    if (!element) {
        console.warn('Element not found for getBoundingClientRect');
        return { left: 0, top: 0, width: 0, height: 0, right: 0, bottom: 0 };
    }

    const rect = element.getBoundingClientRect();
    return {
        left: rect.left,
        top: rect.top,
        width: rect.width,
        height: rect.height,
        right: rect.right,
        bottom: rect.bottom
    };
};

window.showToast = function (message, duration = 3000) {
    // Remove any existing toast
    const existingToast = document.querySelector('.toast-notification');
    if (existingToast) {
        existingToast.remove();
    }

    // Create toast element
    const toast = document.createElement('div');
    toast.className = 'toast-notification';
    toast.textContent = message;

    // Apply styles
    Object.assign(toast.style, {
        position: 'fixed',
        bottom: '20px',
        left: '50%',
        transform: 'translateX(-50%)',
        backgroundColor: 'var(--gray-800, #374151)',
        color: 'var(--white, #ffffff)',
        padding: '12px 20px',
        borderRadius: '8px',
        border: '1px solid var(--transparent-white, rgba(255, 255, 255, 0.1))',
        fontSize: '14px',
        fontWeight: '500',
        zIndex: '10000',
        maxWidth: '90vw',
        textAlign: 'center',
        backdropFilter: 'blur(10px)',
        boxShadow: '0 4px 12px rgba(0, 0, 0, 0.3)',
        opacity: '0',
        transition: 'opacity 0.3s ease-in-out'
    });

    // Add to page
    document.body.appendChild(toast);

    // Trigger animation
    requestAnimationFrame(() => {
        toast.style.opacity = '1';
    });

    // Remove after duration
    setTimeout(() => {
        toast.style.opacity = '0';
        setTimeout(() => {
            if (document.body.contains(toast)) {
                document.body.removeChild(toast);
            }
        }, 300);
    }, duration);
};

// Fallback copy function for older browsers (already exists but ensuring it's available)
window.fallbackCopyTextToClipboard = function (text) {
    const textArea = document.createElement('textarea');
    textArea.value = text;
    textArea.style.position = 'fixed';
    textArea.style.left = '-999999px';
    textArea.style.top = '-999999px';
    document.body.appendChild(textArea);
    textArea.focus();
    textArea.select();

    try {
        const successful = document.execCommand('copy');
        const msg = successful ? 'successful' : 'unsuccessful';
        console.log('Fallback: Copying text command was ' + msg);
        return successful;
    } catch (err) {
        console.error('Fallback: Unable to copy', err);
        return false;
    } finally {
        document.body.removeChild(textArea);
    }
};

// Hide share menu when clicking outside
document.addEventListener('click', function (event) {
    const shareMenus = document.querySelectorAll('.share-context-menu.visible');
    shareMenus.forEach(menu => {
        if (!menu.contains(event.target)) {
            // Trigger hide by dispatching custom event
            const hideEvent = new CustomEvent('hideShareMenu');
            menu.dispatchEvent(hideEvent);
        }
    });
});

// Handle escape key to close menu
document.addEventListener('keydown', function (event) {
    if (event.key === 'Escape') {
        const shareMenus = document.querySelectorAll('.share-context-menu.visible');
        shareMenus.forEach(menu => {
            const hideEvent = new CustomEvent('hideShareMenu');
            menu.dispatchEvent(hideEvent);
        });
    }
});