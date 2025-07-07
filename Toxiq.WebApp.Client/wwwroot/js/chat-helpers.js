// Toxiq.WebApp.Client/wwwroot/js/chat-helpers.js
// JavaScript helpers for chat functionality

window.scrollToBottom = (element) => {
    if (element) {
        //element.scrollIntoView({ behavior: 'smooth', block: 'end' });
    }
};

window.focusElement = (element) => {
    if (element) {
        element.focus();
    }
};

window.autoResizeTextarea = (textarea) => {
    if (textarea) {
        textarea.style.height = 'auto';
        textarea.style.height = Math.min(textarea.scrollHeight, 120) + 'px';
    }
};

window.playNotificationSound = () => {
    // Create and play notification sound
    const audio = new Audio('data:audio/wav;base64,UklGRnoGAABXQVZFZm10IBAAAAABAAEAQB8AAEAfAAABAAgAZGF0YQoGAACBhYqFbF1fdJivrJBhNjVgodDbq2EcBj+a2/LDciUFLIHO8tiJNwgZaLvt559NEAxQp+PwtmMcBjiR1/LMeSwFJHfH8N2QQAoUXrTp66hVFApGn+DyvmgbCDuO0/LNeSgGI3XB7d+WQwsUW7Lz7axYFgtNne3mvG0eccJiOXShpLDBqLZ0...'); // Notification sound data
    audio.volume = 0.3;
    audio.play().catch(() => {
        // Ignore audio play errors (user interaction required)
    });
};

window.requestNotificationPermission = async () => {
    if ('Notification' in window) {
        //const permission = await Notification.requestPermission();
        return permission === 'granted';
    }
    return false;
};

window.showBrowserNotification = (title, body, icon) => {
    if ('Notification' in window && Notification.permission === 'granted') {
        const notification = new Notification(title, {
            body: body,
            icon: icon || '/favicon.ico',
            badge: '/favicon.ico',
            tag: 'toxiq-chat',
            requireInteraction: false
        });

        // Auto close after 5 seconds
        setTimeout(() => notification.close(), 5000);

        return notification;
    }
    return null;
};

window.detectMobile = () => {
    return window.matchMedia('(max-width: 768px)').matches;
};

window.onResize = (dotNetHelper) => {
    const handleResize = () => {
        const isMobile = window.detectMobile();
        dotNetHelper.invokeMethodAsync('OnWindowResize', isMobile);
    };

    window.addEventListener('resize', handleResize);

    // Return cleanup function
    return () => {
        window.removeEventListener('resize', handleResize);
    };
};

// Chat-specific utilities
window.chatUtils = {
    // Format message timestamp
    formatMessageTime: (dateString) => {
        const date = new Date(dateString);
        const now = new Date();
        const diffTime = Math.abs(now - date);
        const diffDays = Math.ceil(diffTime / (1000 * 60 * 60 * 24));

        if (diffDays === 1) {
            return 'Today ' + date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
        } else if (diffDays === 2) {
            return 'Yesterday ' + date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
        } else if (diffDays <= 7) {
            return date.toLocaleDateString([], { weekday: 'short' }) + ' ' +
                date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
        } else {
            return date.toLocaleDateString() + ' ' +
                date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
        }
    },

    // Copy text to clipboard
    copyToClipboard: async (text) => {
        try {
            await navigator.clipboard.writeText(text);
            return true;
        } catch (err) {
            // Fallback for older browsers
            const textArea = document.createElement('textarea');
            textArea.value = text;
            document.body.appendChild(textArea);
            textArea.select();
            const success = document.execCommand('copy');
            document.body.removeChild(textArea);
            return success;
        }
    },

    // Download file from URL
    downloadFile: (url, filename) => {
        const link = document.createElement('a');
        link.href = url;
        link.download = filename;
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
    },

    // Check if element is in viewport
    isElementInViewport: (element) => {
        const rect = element.getBoundingClientRect();
        return (
            rect.top >= 0 &&
            rect.left >= 0 &&
            rect.bottom <= (window.innerHeight || document.documentElement.clientHeight) &&
            rect.right <= (window.innerWidth || document.documentElement.clientWidth)
        );
    },

    // Smooth scroll to element
    scrollToElement: (element, behavior = 'smooth') => {
        if (element) {
            element.scrollIntoView({ behavior, block: 'nearest' });
        }
    },

    // Get file preview info
    getFilePreview: (file) => {
        return new Promise((resolve) => {
            const reader = new FileReader();
            reader.onload = (e) => {
                resolve({
                    name: file.name,
                    size: file.size,
                    type: file.type,
                    dataUrl: e.target.result
                });
            };
            reader.readAsDataURL(file);
        });
    }
};

// Initialize chat features when DOM is loaded
document.addEventListener('DOMContentLoaded', () => {
    // Request notification permission on first visit
    if ('Notification' in window && Notification.permission === 'default') {
        // Don't request immediately, wait for user interaction
        document.addEventListener('click', () => {
            window.requestNotificationPermission();
        }, { once: true });
    }
});

// Export for module usage
if (typeof module !== 'undefined' && module.exports) {
    module.exports = window.chatUtils;
}